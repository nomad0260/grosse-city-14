using Robust.Shared.Utility;

namespace Content.Client._Grosse.AmbientMusic;

/// <summary>
///     Names of the city ambience tracks, keyed by their file name without the extension.
/// </summary>
/// <remarks>
///     The sound collection only knows file paths, so the readable names live here and are used by the music
///     player. Tracks without an entry are shown as a generic placeholder.
/// </remarks>
public static class AmbientTrackInfo
{
    public sealed record Track(string Artist, string Title, int? Year);

    private static readonly Dictionary<string, Track> Tracks = new()
    {
        ["ambient_1"] = new("James Newton Howard", "Stowaway", 2015),
        ["ambient_4"] = new("Atrium Carceri & Cities Last Broadcast", "House of Tinctures", 2017),
        ["ambient_5"] = new("Yaroslav Odrin", "Wild Island", 2025),
        ["ambient_6"] = new("MINNUS", "Lujuria", null),
        ["ambient_7"] = new("Poemme", "The Park at Night", 2017),
        ["ambient_8"] = new("IWICD", "The Silent Song", 2023),
        ["ambient_10"] = new("Atrium Carceri & Cities Last Broadcast", "Prepare the Stage of Night", 2017),
        ["ambient_11"] = new("Alphaxone", "Spellbound", 2019),
        ["ambient_13"] = new("Apocryphos, Kammarheit & Atrium Carceri", "Avenoir", 2015),
        ["ambient_14"] = new("Keosz", "Fate", 2013),
        ["ambient_16"] = new(
            "Hilversum Radio Philharmonic Orchestra & Leopold Stokowski",
            "Symphony in D Minor",
            1971),
        ["ambient_18"] = new("Mike VanPortfleet", "Deep In The Morning Sun", 2004),
        ["ambient_20"] = new("P.T. Adamczyk", "Outsider No More", 2020),
        ["ambient_22"] = new("Archean Nights", "Essence of Archean Darkness", 2018),
        ["ambient_23"] = new("Phragments", "Defiance", 2016),
        ["ambient_25"] = new("Meydän", "Illusion", 2020),
        ["ambient_26"] = new("Artur Kordas", "Road to Home", 2017),
        ["ambient_27"] = new("Fir3Music", "Johnny Speech", null),
        ["ambient_28"] = new("P.T. Adamczyk", "Not a Plan, a Man", 2023),
        ["ambient_29"] = new("P.T. Adamczyk", "Just Another Weapon", 2023),
        ["ambient_30"] = new("Kenji Kawai", "Nightstalker", 1995),
        ["ambient_31"] = new("CURSEDMANE", "Grim", null),
        ["ambient_32"] = new("Aperture Science Psychoacoustic Laboratories", "Self Esteem Fund", 2007),
        ["ambient_33"] = new("Valve", "Calabi-Yau Model", 2020),
        ["ambient_34"] = new("Showtek", "Slow Down (Extended Radio Edit)", null),
        ["ambient_35"] = new("Pivio & Aldo De Scalzi", "Shadowing", 2012),
        ["ambient_36"] = new("onoken", "Simple Strain", 2017),
        ["ambient_37"] = new("Versus", "Intro", 2012),
        ["ambient_38"] = new("Andrew Hulshult", "Embrace the Darkness", 2018),
        ["ambient_39"] = new("Takeshi Abo", "The Air Of Terror (Another)", 2016),
    };

    /// <summary>
    ///     Looks the name of <paramref name="path"/> up. Unknown tracks return false.
    /// </summary>
    public static bool TryGet(ResPath path, out Track track)
    {
        return Tracks.TryGetValue(path.FilenameWithoutExtension, out track!);
    }
}
