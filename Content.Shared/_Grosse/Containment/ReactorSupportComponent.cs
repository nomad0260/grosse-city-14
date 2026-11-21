using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Вспомогательная система реактора: сдерживающий контур, радиационный экран или охлаждающий блок.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReactorSupportComponent : Component
{
    [DataField, AutoNetworkedField]
    public ReactorSupportKind Kind = ReactorSupportKind.ContainmentLoop;

    /// <summary>Эффективность контура или экрана.</summary>
    [DataField, AutoNetworkedField]
    public float Efficiency = 1f;

    [DataField, AutoNetworkedField]
    public float Integrity = 1f;

    /// <summary>Прибавка к запасу сдерживания ядра, пока контур исправен.</summary>
    [DataField]
    public float ContainmentBonus = 10f;

    /// <summary>Прибавка к стабилизации ядра, пока контур исправен.</summary>
    [DataField]
    public float StabilizationBonus;

    /// <summary>Мощность отвода тепла для охладителей, единиц тепла в секунду.</summary>
    [DataField]
    public float CoolingCapacity = 50f;

    /// <summary>
    /// Во сколько раз охладитель тратит больше хладагента: форсаж обслуживаемого
    /// эмиттера сверх 100% требует большей прокачки. Считает ядро при распределении.
    /// </summary>
    [ViewVariables]
    public float CoolantDemand = 1f;

    /// <summary>Радиус работы контура/экрана или обслуживания эмиттера охладителем.</summary>
    [DataField]
    public float Range = 12f;

    /// <summary>
    /// Доминирующий хладагент в баке охладителя. Нужен ядру, чтобы применить
    /// профиль жидкости к патрону обслуживаемого эмиттера.
    /// </summary>
    [ViewVariables]
    public string? CoolantReagent;
}
