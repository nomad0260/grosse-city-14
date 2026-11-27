using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Folder with the city ambience tracks (<c>ambient_1.ogg</c> and so on) on the server machine.
    /// </summary>
    /// <remarks>
    ///     The tracks are not shipped with the client: the server sends them to the player at the start of the round.
    ///     A relative path is resolved against the server data folder (so the default is the
    ///     <c>data/ambient_music</c> folder next to the server binary), an empty value turns the city ambience off.
    ///     The file names must match the <c>GrosseCityAmbient</c> sound collection.
    /// </remarks>
    public static readonly CVarDef<string> GrosseAmbientMusicDirectory =
        CVarDef.Create("grosse.ambient_music.directory", "ambient_music", CVar.SERVER | CVar.ARCHIVE);
}
