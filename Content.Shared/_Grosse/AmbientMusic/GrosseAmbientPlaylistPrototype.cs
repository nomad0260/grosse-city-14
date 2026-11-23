using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Grosse.AmbientMusic;

/// <summary>
///     List of city ambience track paths streamed from the server rather than shipped in client resources.
/// </summary>
/// <remarks>
///     Uses strings instead of <c>ResPath</c> so YAML validation does not require the copyrighted audio files to
///     exist under <c>Resources/</c>.
/// </remarks>
[Prototype]
public sealed partial class GrosseAmbientPlaylistPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("files")]
    public List<string> Files { get; private set; } = new();
}
