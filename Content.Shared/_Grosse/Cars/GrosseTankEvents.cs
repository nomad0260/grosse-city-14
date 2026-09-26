using Content.Shared.Actions;
using Content.Shared.DoAfter;

namespace Content.Shared._Grosse.Cars;

public sealed partial class GrosseTankReloadEvent : InstantActionEvent;

public sealed partial class GrosseTankReloadDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class GrosseTankGunnerEnterDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class GrosseTankGunnerEjectDoAfterEvent : SimpleDoAfterEvent;
