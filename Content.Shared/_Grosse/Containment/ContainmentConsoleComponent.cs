using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Grosse.Containment;

/// <summary>
/// Режим терминала управления реактором.
/// </summary>
public enum ReactorConsoleMode : byte
{
    Full,
    Diagnostic,
    Manual,
    Containment,
    Power,
}

/// <summary>
/// Центральная консоль управления реактором РТМ.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ContainmentConsoleComponent : Component
{
    [DataField]
    public ReactorConsoleMode Mode = ReactorConsoleMode.Full;

    /// <summary>
    /// Радиус привязки к ядру. Центральная консоль РТМ управляет реактором
    /// с любого расстояния в пределах карты.
    /// </summary>
    [DataField]
    public float LinkRange = float.MaxValue;

    [DataField]
    public TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// Радиус, в котором терминал объявляет тревогу: сирена, голосовые объявления
    /// и сообщения в чат слышны только в нём.
    /// </summary>
    [DataField]
    public float AlarmRange = 60f;

    /// <summary>Зацикленная сирена обычной тревоги.</summary>
    [DataField]
    public SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/core_near_uncontained_loop2.wav");

    /// <summary>Зацикленная сирена критической тревоги.</summary>
    [DataField]
    public SoundSpecifier CriticalAlarmSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/core_far_uncontained_loop1.wav");

    /// <summary>Голосовое предупреждение о нагреве эмиттеров выше 700 K.</summary>
    [DataField]
    public SoundSpecifier OverheatSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/over700.ogg");

    /// <summary>Голосовое предупреждение о критической температуре.</summary>
    [DataField]
    public SoundSpecifier CriticalTemperatureSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/criticaltemp.ogg");

    /// <summary>Отсчёт до распада установки при расширении ядра.</summary>
    [DataField]
    public SoundSpecifier ExplosionCountdownSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/45seconds.ogg");

    /// <summary>Отсчёт до расплавления при потере сдерживания.</summary>
    [DataField]
    public SoundSpecifier MeltdownCountdownSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/30sec.ogg");

    /// <summary>Отсчёт до коллапса при критическом сжатии.</summary>
    [DataField]
    public SoundSpecifier CollapseCountdownSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/collapse.ogg");

    /// <summary>Отбой тревоги.</summary>
    [DataField]
    public SoundSpecifier ClearSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/noemergency.ogg");

    /// <summary>Объявление аварийной остановки.</summary>
    [DataField]
    public SoundSpecifier EmergencyStopSound = new SoundPathSpecifier("/Audio/_Grosse/Machines/darkcore/emergencystop.ogg");

    /// <summary>
    /// Ядро этой консоли погибло (взрыв или коллапс). Интерфейс показывает
    /// последствия: помехи, срыв кадров и искажения. Сбрасывается, когда
    /// к консоли привязывается новое живое ядро.
    /// </summary>
    [ViewVariables] public bool Aftermath;

    [ViewVariables] public EntityUid? Core;
    [ViewVariables] public TimeSpan NextRefresh;
}
