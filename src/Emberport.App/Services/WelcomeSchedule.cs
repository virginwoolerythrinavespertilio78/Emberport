using System;

namespace Emberport.Services;

/// <summary>Keeps the support overlay welcome rather than nagging.</summary>
public static class WelcomeSchedule
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(4);

    public static bool ShouldShow()
    {
        var last = AppSettings.Current.WelcomeShownAt;

        if (last is null)
        {
            return true;
        }

        // A clock moved backwards should not lock the overlay out forever.
        if (last > DateTimeOffset.Now)
        {
            return true;
        }

        return DateTimeOffset.Now - last.Value >= Interval;
    }

    public static void MarkShown()
    {
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now;
        AppSettings.Save();
    }
}