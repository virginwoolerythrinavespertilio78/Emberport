using Emberport.Services;
using Xunit;

namespace Emberport.Tests;

public class FormattingTests
{
    [Fact]
    public void Human_uses_bytes_for_small_values()
    {
        Assert.Contains("B", MySqlBackup.Human(512));
        Assert.DoesNotContain("KB", MySqlBackup.Human(512));
    }

    [Theory]
    [InlineData(4L * 1024, "KB")]
    [InlineData(7L * 1024 * 1024, "MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "GB")]
    public void Human_scales_to_the_right_unit(long bytes, string unit)
    {
        Assert.Contains(unit, MySqlBackup.Human(bytes));
    }

    [Fact]
    public void Human_never_returns_an_empty_string()
    {
        Assert.False(string.IsNullOrWhiteSpace(MySqlBackup.Human(0)));
    }

    [Fact]
    public void App_identity_is_filled_in()
    {
        Assert.Equal("Emberport", AppInfo.Name);
        Assert.Equal("Hojjat Jahanpour", AppInfo.Author);
        Assert.StartsWith("v", AppInfo.DisplayVersion);
        Assert.Contains("github.com/hojjatjh", AppInfo.RepositoryUrl);
    }

    [Fact]
    public void Tray_argument_is_stable()
    {
        // The registry value written at startup depends on this exact string.
        Assert.Equal("--tray", StartupRegistration.TrayArgument);
    }
}