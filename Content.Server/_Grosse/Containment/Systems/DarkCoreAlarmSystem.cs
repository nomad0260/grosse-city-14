using System;
using System.Collections.Generic;
using Content.Server.Chat.Systems;
using Content.Shared._Grosse.Containment;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Grosse.Containment;

/// <summary>
/// Тревога центральной консоли РТМ: сирену и протокольное оповещение получают только
/// реальные угрозы установке — расширение, начинающийся коллапс и потеря сдерживания.
/// Перегрузка сети и просадка стабильности остаются предупреждениями на самой консоли.
/// Сирена, голосовые объявления и сообщения в чат слышны только в радиусе консоли.
/// </summary>
public sealed partial class DarkCoreAlarmSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ContainmentCoreSystem _core = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5f);

    /// <summary>Сколько состояние должно продержаться, прежде чем включать сирену.</summary>
    private static readonly TimeSpan AlarmConfirmationDelay = TimeSpan.FromSeconds(1.5f);

    /// <summary>Температура, после которой эмиттеры начинают получать повреждения.</summary>
    private const float OverheatThreshold = 700f;

    /// <summary>Температура, после которой сдерживание разрушается само по себе.</summary>
    private const float CriticalTemperatureThreshold = 900f;

    /// <summary>Ниже этой температуры предупреждения снимаются, чтобы не мигали на границе.</summary>
    private const float OverheatResetThreshold = 650f;

    private static readonly Color AlarmColor = Color.Red;
    private static readonly Color WarningColor = Color.FromHex("#FFB347");
    private static readonly Color InfoColor = Color.FromHex("#8FD8FF");

    private readonly Dictionary<EntityUid, AlarmState> _alarms = new();

    private sealed class AlarmState
    {
        public ContainmentCoreState LastState = ContainmentCoreState.Offline;
        public ContainmentCoreState PendingState = ContainmentCoreState.Offline;
        public ContainmentCoreState ActiveAlarmState = ContainmentCoreState.Offline;
        public (EntityUid Entity, AudioComponent Component)? Stream;
        public (EntityUid Entity, AudioComponent Component)? Voice;
        public bool Critical;
        public bool ReportSent;
        public bool OverheatWarned;
        public bool CriticalTemperatureWarned;
        public bool WasRunning;
        public TimeSpan PendingSince;
        public TimeSpan NextCheck;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ContainmentConsoleComponent>();

        while (query.MoveNext(out var uid, out var console))
        {
            // Тревогу объявляет только центральная консоль. Остальные терминалы
            // повторяют её интерфейс и не должны дублировать сирену и сообщения.
            if (console.Mode != ReactorConsoleMode.Full)
                continue;

            if (!_alarms.TryGetValue(uid, out var alarm))
            {
                alarm = new AlarmState();
                _alarms[uid] = alarm;
            }

            if (now < alarm.NextCheck)
                continue;

            alarm.NextCheck = now + CheckInterval;

            if (console.Core is not { } core || Deleted(core) ||
                !TryComp<ContainmentCoreComponent>(core, out var coreComp))
            {
                alarm.LastState = ContainmentCoreState.Offline;
                alarm.WasRunning = false;
                StopAlarm(uid, console, alarm, false);
                continue;
            }

            var state = coreComp.State;

            // Один раз за пуск сообщаем, какие пары «эмиттер — охладитель» собраны
            // и хватает ли жидкости. Без этого игрок узнаёт о слабой паре только
            // по растущей температуре.
            if (!coreComp.IsRunning)
            {
                alarm.ReportSent = false;
            }
            else if (!alarm.ReportSent && state == ContainmentCoreState.Nominal)
            {
                alarm.ReportSent = true;
                Announce(uid, console,
                    $"Сборка РТМ: {_core.BuildAssemblySummary(core, coreComp)}",
                    InfoColor);
            }

            // Остановка в этом реакторе всегда аварийная: другой команды нет.
            // Незаконченный разгон отличается тем, что ядро не успело выйти на режим.
            if (alarm.WasRunning && !coreComp.IsRunning)
            {
                var aborted = coreComp.StartupTimer > 0f &&
                              coreComp.StartupTimer < coreComp.StartupDelay;

                Announce(uid, console,
                    aborted
                        ? "РТМ. Срыв разгона: условия пуска нарушены. Реактор остановлен, стержни сброшены."
                        : "РТМ. Аварийный сброс. Реактор остановлен, подача энергии в сеть прекращена.",
                    WarningColor);

                PlayVoice(uid, console, alarm, console.EmergencyStopSound);
            }

            alarm.WasRunning = coreComp.IsRunning;

            UpdateTemperatureWarning(uid, console, alarm, coreComp);

            // Не дёргаем сирену на каждый кратковременный переход состояния:
            // тревога должна продержаться несколько проверок подряд.
            if (state != alarm.LastState)
            {
                alarm.LastState = state;
                alarm.PendingState = state;
                alarm.PendingSince = now;
            }

            if (!IsAlarmState(state))
            {
                // Отбой объявляем только при возврате в рабочий режим, а не при
                // удалении ядра или консоли.
                var cleared = state is ContainmentCoreState.Nominal
                    or ContainmentCoreState.Overload
                    or ContainmentCoreState.Unstable
                    or ContainmentCoreState.Offline;

                StopAlarm(uid, console, alarm, cleared);
                alarm.ActiveAlarmState = ContainmentCoreState.Offline;
                continue;
            }

            if (state != alarm.PendingState)
            {
                alarm.PendingState = state;
                alarm.PendingSince = now;
            }

            if (now - alarm.PendingSince < AlarmConfirmationDelay)
                continue;

            if (alarm.ActiveAlarmState == state && alarm.Stream != null)
                continue;

            StartAlarm(uid, console, alarm, state, core, coreComp);
            alarm.ActiveAlarmState = state;
        }
    }

    private static bool IsAlarmState(ContainmentCoreState state)
    {
        // Сирена — только для реальных угроз установке. Перегрузка сети и просадка
        // стабильности остаются предупреждениями на консоли, иначе реактор орёт
        // постоянно ещё до того, как начинает разрушаться.
        return state is ContainmentCoreState.Expanding
            or ContainmentCoreState.Compressing
            or ContainmentCoreState.ContainmentFailure;
    }

    private void StartAlarm(EntityUid uid, ContainmentConsoleComponent console, AlarmState alarm,
        ContainmentCoreState state, EntityUid core, ContainmentCoreComponent coreComp)
    {
        var filter = BuildFilter(uid, console);

        _chat.DispatchFilteredAnnouncement(
            filter,
            GetMessage(state, core, coreComp),
            source: uid,
            sender: "РТМ",
            playSound: false,
            colorOverride: AlarmColor);

        // Голосовое объявление соответствует реальной задержке до катастрофы.
        PlayVoice(uid, console, alarm, state switch
        {
            ContainmentCoreState.Expanding => console.ExplosionCountdownSound,
            ContainmentCoreState.Compressing => console.CollapseCountdownSound,
            ContainmentCoreState.ContainmentFailure => console.MeltdownCountdownSound,
            _ => null,
        });

        var critical = IsAlarmState(state);
        var sound = critical ? console.CriticalAlarmSound : console.AlarmSound;

        if (alarm.Stream is null || alarm.Critical != critical)
        {
            if (alarm.Stream is { } oldStream)
                _audio.Stop(oldStream.Entity);

            // Сирена играет только тем, кто попал в фильтр радиуса консоли.
            alarm.Stream = _audio.PlayGlobal(
                sound,
                filter,
                false,
                AudioParams.Default.WithLoop(true).WithVolume(-1f));

            alarm.Critical = critical;
        }

    }

    private void StopAlarm(EntityUid uid, ContainmentConsoleComponent console, AlarmState alarm, bool announceClear)
    {
        var wasAlarming = alarm.ActiveAlarmState != ContainmentCoreState.Offline;

        if (alarm.Stream is { } stream && !Deleted(stream.Entity))
            _audio.Stop(stream.Entity);

        alarm.Stream = null;
        alarm.Critical = false;
        StopVoice(alarm);
        alarm.ActiveAlarmState = ContainmentCoreState.Offline;

        if (!wasAlarming || !announceClear)
            return;

        Announce(uid, console, "РТМ. Авария устранена. Реактор в штатном режиме, угрозы установке нет.", InfoColor);
        PlayVoice(uid, console, alarm, console.ClearSound);
    }

    /// <summary>
    /// Предупреждения по температуре. Это не сирена: ядро ещё работает штатно,
    /// но эмиттеры уже получают повреждения, и персоналу нужно успеть вмешаться.
    /// </summary>
    private void UpdateTemperatureWarning(EntityUid uid, ContainmentConsoleComponent console,
        AlarmState alarm, ContainmentCoreComponent core)
    {
        if (!core.IsRunning || core.Temperature < OverheatResetThreshold)
        {
            alarm.OverheatWarned = false;
            alarm.CriticalTemperatureWarned = false;
            return;
        }

        if (!alarm.CriticalTemperatureWarned && core.Temperature >= CriticalTemperatureThreshold)
        {
            alarm.CriticalTemperatureWarned = true;
            alarm.OverheatWarned = true;

            Announce(uid, console,
                $"РТМ. Критическая температура {core.Temperature:0} K. Оборудование разрушается, " +
                "сдерживание теряется само по себе. Немедленно остановите реактор.",
                AlarmColor);

            PlayVoice(uid, console, alarm, console.CriticalTemperatureSound);
            return;
        }

        if (alarm.OverheatWarned || core.Temperature < OverheatThreshold)
            return;

        alarm.OverheatWarned = true;

        Announce(uid, console,
            $"РТМ. Температура {core.Temperature:0} K выше 700 K. " +
            "Эмиттеры и вспомогательные системы получают повреждения. Добавьте охлаждение или снизьте интенсивность.",
            WarningColor);

        PlayVoice(uid, console, alarm, console.OverheatSound);
    }

    private Filter BuildFilter(EntityUid uid, ContainmentConsoleComponent console)
    {
        return Filter.Empty().AddInRange(
            _transform.GetMapCoordinates(uid),
            console.AlarmRange,
            _playerManager,
            EntityManager);
    }

    private void Announce(EntityUid uid, ContainmentConsoleComponent console, string message, Color color)
    {
        _chat.DispatchFilteredAnnouncement(
            BuildFilter(uid, console),
            message,
            source: uid,
            sender: "РТМ",
            playSound: false,
            colorOverride: color);
    }

    /// <summary>
    /// Голосовые объявления идут одним каналом: новое объявление обрывает
    /// предыдущее, иначе отсчёты разных аварий и приказы накладываются друг на друга.
    /// </summary>
    private void PlayVoice(EntityUid uid, ContainmentConsoleComponent console, AlarmState alarm,
        SoundSpecifier? sound)
    {
        if (sound is null)
            return;

        StopVoice(alarm);

        alarm.Voice = _audio.PlayGlobal(sound, BuildFilter(uid, console), false,
            AudioParams.Default.WithVolume(-1f));
    }

    private void StopVoice(AlarmState alarm)
    {
        if (alarm.Voice is { } voice && !Deleted(voice.Entity))
            _audio.Stop(voice.Entity);

        alarm.Voice = null;
    }

    /// <summary>
    /// Сухое протокольное оповещение с текущей телеметрией ядра.
    /// </summary>
    private string GetMessage(ContainmentCoreState state, EntityUid core, ContainmentCoreComponent coreComp)
    {
        var stability = coreComp.Stability;
        var integrity = coreComp.ContainmentIntegrity;
        var expansion = coreComp.Expansion;

        var active = 0;
        var total = 0;

        foreach (var projectorUid in _core.GetSortedStabilizers(core))
        {
            if (!TryComp<ContainmentStabilizerComponent>(projectorUid, out var projector))
                continue;

            total++;

            if (projector.Enabled && projector.Integrity > 0.05f && projector.HasCartridge && projector.CartridgeResource > 0f)
                active++;
        }

        return state switch
        {
            ContainmentCoreState.Overload =>
                $"РТМ. Перегрузка энергосети. Выработка {coreComp.EnergyOutput:0} кВт, нагрузка {coreComp.EnergyLoad:0} кВт. " +
                "Требуется снижение нагрузки или повышение мощности ядра.",

            ContainmentCoreState.Unstable =>
                $"РТМ. Стабильность {stability:0}/100, сдерживание {integrity:0}/100. " +
                $"Активных эмиттеров {active} из {total}. Требуется проверка патронов, лучей и охлаждения.",

            ContainmentCoreState.Expanding =>
                $"РТМ. Расширение ядра {expansion:0}/200, стабильность {stability:0}/100. " +
                $"До распада установки {coreComp.ExplodeDelay:0} с. " +
                (coreComp.IsRunning
                    ? "Требуется повышение стабилизации."
                    : "Стержни сброшены, но ядро не остыло: охладите контур."),

            ContainmentCoreState.Compressing =>
                $"РТМ. Сжатие {coreComp.Compression:0}, плотность {coreComp.Density:0}/200, " +
                $"До коллапса {coreComp.CollapseDelay:0} с. Требуется снижение сжатия или интенсивности эмиттеров.",

            ContainmentCoreState.ContainmentFailure =>
                $"РТМ. Потеря сдерживания. Сдерживание {integrity:0}/100. " +
                $"До расплавления {coreComp.ContainmentFailureDelay:0} с. " +
                (coreComp.IsRunning
                    ? "Персоналу покинуть отсек."
                    : "Стержни сброшены, но сдерживание не восстановилось. Персоналу покинуть отсек."),

            _ => "РТМ. Параметры реактора вне допуска.",
        };
    }

    private void OnConsoleShutdown(EntityUid uid, ContainmentConsoleComponent comp, ComponentShutdown args)
    {
        if (!_alarms.Remove(uid, out var alarm))
            return;

        StopAlarm(uid, comp, alarm, false);
    }
}
