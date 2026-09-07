using Robust.Shared.Serialization;

namespace Content.Shared._Grosse.Pvp;

[Serializable, NetSerializable]
public sealed class PvpClassInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Cost;
    public int MaxCount;
}
