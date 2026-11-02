using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Cars;

public sealed partial class GrosseCarExitEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class GrosseCarEnterDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string SlotId = string.Empty;

    public GrosseCarEnterDoAfterEvent()
    {
    }

    public GrosseCarEnterDoAfterEvent(string slotId)
    {
        SlotId = slotId;
    }

    public override DoAfterEvent Clone()
    {
        return new GrosseCarEnterDoAfterEvent(SlotId);
    }
}

[Serializable, NetSerializable]
public sealed partial class GrosseCarEjectDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string SlotId = string.Empty;

    public GrosseCarEjectDoAfterEvent()
    {
    }

    public GrosseCarEjectDoAfterEvent(string slotId)
    {
        SlotId = slotId;
    }

    public override DoAfterEvent Clone()
    {
        return new GrosseCarEjectDoAfterEvent(SlotId);
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is GrosseCarEjectDoAfterEvent eject && eject.SlotId == SlotId;
    }
}
