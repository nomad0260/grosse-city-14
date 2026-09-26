using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Cars;

[Serializable, NetSerializable]
public enum GrosseTankVisuals : byte
{
    Chambered,
    Ammo,
    Gunner,
    Wrecked,
}

[Serializable, NetSerializable]
public enum GrosseTankVisualLayers : byte
{
    Base,
    Chambered,
    Ammo,
    Gunner,
    Wrecked,
}

[Serializable, NetSerializable]
public enum GrosseTankAmmoVisual : byte
{
    Empty,
    Partial,
    Full,
}
