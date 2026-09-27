using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Cars;

public sealed partial class GrosseTankReloadEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class GrosseTankReloadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class GrosseTankGunnerEnterDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class GrosseTankGunnerEjectDoAfterEvent : SimpleDoAfterEvent;
