using System.Globalization;

namespace Content.Shared._Grosse.WorkPhase;

public enum WorkPhase : byte
{
    Free,
    Work,
    Curfew,
}

public static class WorkPhases
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 180;

    public static string Name(WorkPhase phase)
    {
        return phase switch
        {
            WorkPhase.Free => "work-phase-free",
            WorkPhase.Work => "work-phase-work",
            WorkPhase.Curfew => "work-phase-curfew",
            _ => "work-phase-free",
        };
    }

    public static string Announcement(WorkPhase phase)
    {
        return phase switch
        {
            WorkPhase.Free => "work-phase-free-announcement",
            WorkPhase.Work => "work-phase-work-announcement",
            WorkPhase.Curfew => "work-phase-curfew-announcement",
            _ => "work-phase-free-announcement",
        };
    }

    public static Color ColorOf(WorkPhase phase)
    {
        return phase switch
        {
            WorkPhase.Free => new Color(102, 255, 102),
            WorkPhase.Work => new Color(218, 165, 32),
            WorkPhase.Curfew => new Color(227, 51, 51),
            _ => Color.White,
        };
    }

    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return remaining.TotalHours >= 1
            ? remaining.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }
}
