using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Assault;

[Serializable, NetSerializable]
public enum AssaultCaptureVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum AssaultCaptureState : byte
{
    Idle,
    Capturing,
    Contested,
    Captured,
}

[Serializable, NetSerializable]
public enum AssaultCaptureVisualLayers : byte
{
    Zone,
    Marker,
    BarBackground,
    BarFill,
}
