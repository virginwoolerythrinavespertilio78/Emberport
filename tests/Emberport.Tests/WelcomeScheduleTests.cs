using System;
using Emberport.Services;
using Xunit;

namespace Emberport.Tests;

/// <summary>
/// These tests mutate the in-memory settings singleton only. Save is never called,
/// and the original value is restored, so the real settings.json is left alone.
/// </summary>
public sealed class WelcomeScheduleTests : IDisposable
{
    private readonly DateTimeOffset? _original = AppSettings.Current.WelcomeShownAt;

    public void Dispose() => AppSettings.Current.WelcomeShownAt = _original;

    [Fact]
    public void Shows_on_a_fresh_install()
    {
        AppSettings.Current.WelcomeShownAt = null;

        Assert.True(WelcomeSchedule.ShouldShow());
    }

    [Fact]
    public void Stays_hidden_right_after_it_was_shown()
    {
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now;

        Assert.False(WelcomeSchedule.ShouldShow());
    }

    [Fact]
    public void Stays_hidden_one_day_later()
    {
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now.AddDays(-1);

        Assert.False(WelcomeSchedule.ShouldShow());
    }

    [Fact]
    public void Stays_hidden_just_before_the_interval_ends()
    {
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now.AddDays(-4).AddMinutes(5);

        Assert.False(WelcomeSchedule.ShouldShow());
    }

    [Fact]
    public void Shows_again_after_four_days()
    {
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now.AddDays(-4).AddMinutes(-1);

        Assert.True(WelcomeSchedule.ShouldShow());
    }

    [Fact]
    public void Shows_after_a_long_absence()
    {
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now.AddYears(-1);

        Assert.True(WelcomeSchedule.ShouldShow());
    }

    [Fact]
    public void Recovers_when_the_system_clock_moved_backwards()
    {
        // A timestamp from the future would otherwise hide the overlay forever.
        AppSettings.Current.WelcomeShownAt = DateTimeOffset.Now.AddDays(30);

        Assert.True(WelcomeSchedule.ShouldShow());
    }
}