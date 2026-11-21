using System;
using Content.Shared._Grosse.Containment;
using Content.Shared._Grosse.Cooling;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._Grosse.Cooling;

/// <summary>
/// Расходует смесь хладагентов внутри охладителя, рассчитывает её свойства
/// и превращает использованную часть в отработанную жидкость.
/// Охладитель без хладагента, с переполненным отстойником или вне
/// температурного диапазона жидкости не отводит тепло.
/// </summary>
public sealed partial class CoolingBlockCoolantSystem : EntitySystem
{
    private const float CelsiusToKelvin = 273.15f;

    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SolutionTransferSystem _transfer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CoolingBlockCoolantComponent, GetVerbsEvent<AlternativeVerb>>(AddDrainVerb);
        SubscribeLocalEvent<CoolingBlockCoolantComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CoolingBlockCoolantComponent, ReactorSupportComponent>();

        while (query.MoveNext(out var uid, out var coolant, out var support))
        {
            coolant.Accumulator += frameTime;
            if (coolant.Accumulator < 1f)
                continue;

            var elapsed = coolant.Accumulator;
            coolant.Accumulator = 0f;

            if (!_solutions.TryGetSolution(uid, coolant.Solution, out var solutionEnt, out var solution))
                continue;

            var mixture = GetMixture(coolant, solution);
            if (mixture == null)
            {
                support.CoolingCapacity = 0f;
                support.CoolantReagent = null;
                continue;
            }

            // Ядро применяет профиль жидкости к патрону обслуживаемого эмиттера.
            support.CoolantReagent = mixture.DominantReagent;

            var waste = solution.GetTotalPrototypeQuantity(coolant.WasteReagent).Float();
            var total = mixture.Total + waste;
            var contamination = total > 0.01f ? Math.Clamp(waste / total, 0f, 1f) : 1f;

            if (waste >= coolant.MaxWaste)
            {
                support.CoolingCapacity = 0f;
                continue;
            }

            // Рабочий диапазон задан в градусах Цельсия, а ThermalEquipment хранит
            // температуру в кельвинах. Раствор сам по себе не связан с нагревом
            // охладителя, поэтому берём температуру его корпуса.
            var temperature = solution.Temperature;
            if (TryComp<ThermalEquipmentComponent>(uid, out var thermal))
                temperature = Math.Max(temperature, thermal.Temperature);

            var minTemperature = mixture.MinTemperatureCelsius + CelsiusToKelvin;
            var maxTemperature = mixture.MaxTemperatureCelsius + CelsiusToKelvin;

            if (temperature < minTemperature || temperature > maxTemperature)
            {
                support.CoolingCapacity = 0f;
                continue;
            }

            // Формулы живут в DarkCoreCoolantMath: те же числа использует справочник.
            var flow = DarkCoreCoolantMath.CalculateFlow(mixture.Flow, mixture.Viscosity);
            var capacityFactor = DarkCoreCoolantMath.CalculateCapacityFactor(
                mixture.HeatCapacity,
                mixture.HeatTransfer,
                flow,
                contamination,
                mixture.Components,
                coolant.MixturePenalty);

            var capacity = Math.Max(0f, coolant.BaseCoolingCapacity * capacityFactor);
            support.CoolingCapacity = capacity;

            // Форсаж обслуживаемого эмиттера требует больше прокачки,
            // поэтому бак расходуется быстрее номинала.
            var use = DarkCoreCoolantMath.CalculateConsumptionPerSecond(
                          coolant.CoolantUsePerSecond,
                          mixture.ConsumptionMultiplier,
                          flow,
                          contamination) *
                      elapsed *
                      Math.Max(1f, support.CoolantDemand);

            var removed = _solutions.RemoveReagent(
                solutionEnt.Value,
                mixture.DominantReagent,
                FixedPoint2.New(use));

            var removedFraction = use > 0.0001f
                ? Math.Clamp(removed.Float() / use, 0f, 1f)
                : 0f;

            if (removedFraction <= 0f)
            {
                support.CoolingCapacity = 0f;
                continue;
            }

            // Недостаток хладагента должен снижать и охлаждение, и количество
            // отработки: иначе почти пустой бак продолжал бы работать как полный.
            support.CoolingCapacity = capacity * removedFraction;

            var wasteRate = coolant.WastePerSecond *
                            mixture.WasteMultiplier *
                            mixture.Fouling *
                            elapsed /
                            Math.Max(0.25f, flow);
            wasteRate *= 1f + contamination * 2f;
            wasteRate *= Math.Max(0.2f, 1f - Math.Clamp(mixture.RadiationResistance / 2f, 0f, 0.8f));
            // Нестабильный состав загрязняется быстрее: стабильность больше не
            // уменьшает теплоотвод, иначе дигамма-флюид проигрывал бы воде.
            wasteRate *= DarkCoreCoolantMath.CalculateStabilityFactor(mixture.Stability);
            wasteRate *= removedFraction;

            if (mixture.Components > 1)
                wasteRate *= 1.2f;

            if (wasteRate > 0f)
            {
                _solutions.TryAddReagent(
                    solutionEnt.Value,
                    coolant.WasteReagent.Id,
                    FixedPoint2.New(wasteRate),
                    out _);
            }

            if (mixture.Hazardous && contamination >= coolant.DangerContamination)
            {
                // Радиогель и Дигамма-флюид при сильной деградации повреждают контур.
                support.CoolingCapacity = 0f;
                support.Integrity = Math.Clamp(support.Integrity - elapsed * 0.002f, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Доминирующий теплоноситель в баке: нужен интерфейсу охладителя,
    /// чтобы показать то же, что учитывает расчёт.
    /// </summary>
    public string? GetDominantCoolant(EntityUid uid)
    {
        if (!TryComp<CoolingBlockCoolantComponent>(uid, out var coolant) ||
            !_solutions.TryGetSolution(uid, coolant.Solution, out _, out var solution))
        {
            return null;
        }

        return GetMixture(coolant, solution)?.DominantReagent;
    }

    private static CoolantMixture? GetMixture(CoolingBlockCoolantComponent coolant, Solution solution)
    {
        return CoolantMixtureMath.Compute(coolant.Coolants, solution);
    }

    /// <summary>
    /// Экзамайн бака: что залито, сколько осталось, насколько загрязнено
    /// и сколько тепла блок отводит прямо сейчас.
    /// </summary>
    private void OnExamined(Entity<CoolingBlockCoolantComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return;

        var mixture = GetMixture(ent.Comp, solution);

        if (mixture == null)
        {
            args.PushMarkup("[color=orange]Бак пуст: залейте хладагент канистрой.[/color]");
            return;
        }

        var waste = solution.GetTotalPrototypeQuantity(ent.Comp.WasteReagent).Float();
        var total = mixture.Total + waste;
        var contamination = total > 0.01f ? waste / total : 0f;

        var name = _prototype.TryIndex(mixture.DominantReagent, out ReagentPrototype? reagent)
            ? reagent.LocalizedName
            : mixture.DominantReagent;

        args.PushMarkup($"Хладагент: {name}, {mixture.Total:0} ед., загрязнение {contamination:P0}.");

        if (mixture.Components > 1)
            args.PushMarkup("[color=orange]В баке смесь жидкостей: отвод снижен.[/color]");

        var support = CompOrNull<ReactorSupportComponent>(ent.Owner);

        if (support != null)
            args.PushMarkup($"Теплоотвод: {support.CoolingCapacity:0} ед. тепла/с.");

        if (waste >= ent.Comp.MaxWaste)
            args.PushMarkup("[color=red]Отстойник полон: слейте отработку, блок не охлаждает.[/color]");
        else if (support is { CoolingCapacity: <= 0.01f })
            args.PushMarkup("[color=red]Блок не отводит тепло: проверьте жидкость, температуру бака и загрязнение.[/color]");
        else
            args.PushMarkup("Один блок обслуживает один эмиттер — держите его ближе к своему эмиттеру.");
    }

    private void AddDrainVerb(Entity<CoolingBlockCoolantComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } held ||
            !TryComp<RefillableSolutionComponent>(held, out _))
        {
            return;
        }

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var coolerSolution) ||
            coolerSolution.Volume <= FixedPoint2.Zero ||
            !_solutions.TryGetRefillableSolution(held, out _, out var targetSolution) ||
            targetSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Слить хладагент в ёмкость",
            Priority = 10,
            Act = () => DrainIntoHeld(ent, held, user),
        });
    }

    private void DrainIntoHeld(Entity<CoolingBlockCoolantComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var coolerSolutionEnt, out var coolerSolution) ||
            coolerSolution.Volume <= FixedPoint2.Zero ||
            !_solutions.TryGetRefillableSolution(target, out var targetSolutionEnt, out var targetSolution) ||
            targetSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            return;
        }

        var amount = FixedPoint2.Min(coolerSolution.Volume, targetSolution.AvailableVolume);
        var transferred = _transfer.Transfer(new SolutionTransferData(
            user,
            ent.Owner,
            coolerSolutionEnt.Value,
            target,
            targetSolutionEnt.Value,
            amount));

        if (transferred > FixedPoint2.Zero)
            _popup.PopupEntity($"Слито {transferred:0} единиц хладагента.", ent.Owner, user);
    }
}
