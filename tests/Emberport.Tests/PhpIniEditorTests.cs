using System;
using System.IO;
using System.Linq;
using Emberport.Services;
using Xunit;

namespace Emberport.Tests;

/// <summary>Every test works on a throwaway php.ini inside its own temp folder.</summary>
public sealed class PhpIniEditorTests : IDisposable
{
    private readonly string _folder;
    private readonly string _ini;

    public PhpIniEditorTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "emberport-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_folder);
        _ini = Path.Combine(_folder, "php.ini");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder must never fail a test run.
        }
    }

    private void Write(params string[] lines) => File.WriteAllLines(_ini, lines);

    private string[] Lines() => File.ReadAllLines(_ini);

    private static bool IsOn(System.Collections.Generic.IReadOnlyList<PhpExtension> list, string name) =>
        list.Single(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).IsEnabled;

    // ---------- Read ----------

    [Fact]
    public void Read_returns_empty_when_the_file_is_missing()
    {
        Assert.Empty(PhpIniEditor.Read(Path.Combine(_folder, "nothing.ini")));
    }

    [Fact]
    public void Read_reports_enabled_and_disabled_lines()
    {
        Write("extension=gd", ";extension=intl");

        var found = PhpIniEditor.Read(_ini);

        Assert.Equal(2, found.Count);
        Assert.True(IsOn(found, "gd"));
        Assert.False(IsOn(found, "intl"));
    }

    [Fact]
    public void Read_understands_quotes_and_the_dll_suffix()
    {
        Write("extension=\"php_zip.dll\"", "  extension = curl  ");

        var found = PhpIniEditor.Read(_ini);

        Assert.Contains(found, item => item.Name == "php_zip" && item.IsEnabled);
        Assert.Contains(found, item => item.Name == "curl" && item.IsEnabled);
    }

    [Fact]
    public void Read_sorts_by_name()
    {
        Write("extension=zip", "extension=curl", "extension=gd");

        var names = PhpIniEditor.Read(_ini).Select(item => item.Name).ToArray();

        Assert.Equal(new[] { "curl", "gd", "zip" }, names);
    }

    [Fact]
    public void Read_prefers_the_enabled_copy_of_a_duplicate()
    {
        Write(";extension=gd", "extension=gd");

        var found = PhpIniEditor.Read(_ini);

        Assert.Single(found);
        Assert.True(IsOn(found, "gd"));
    }

    // ---------- SetEnabled ----------

    [Fact]
    public void SetEnabled_uncomments_an_existing_line()
    {
        Write(";extension=mysqli");

        PhpIniEditor.SetEnabled(_ini, "mysqli", true);

        Assert.Contains("extension=mysqli", Lines());
        Assert.True(IsOn(PhpIniEditor.Read(_ini), "mysqli"));
    }

    [Fact]
    public void SetEnabled_comments_out_an_enabled_line()
    {
        Write("extension=mysqli");

        PhpIniEditor.SetEnabled(_ini, "mysqli", false);

        Assert.Contains(";extension=mysqli", Lines());
        Assert.False(IsOn(PhpIniEditor.Read(_ini), "mysqli"));
    }

    [Fact]
    public void SetEnabled_appends_an_extension_that_is_missing()
    {
        Write("memory_limit = 512M");

        PhpIniEditor.SetEnabled(_ini, "intl", true);

        Assert.Contains("extension=intl", Lines());
    }

    [Fact]
    public void SetEnabled_does_not_append_when_disabling_a_missing_extension()
    {
        Write("memory_limit = 512M");

        PhpIniEditor.SetEnabled(_ini, "intl", false);

        Assert.Equal(new[] { "memory_limit = 512M" }, Lines());
    }

    [Fact]
    public void SetEnabled_keeps_unrelated_lines_untouched()
    {
        Write("[PHP]", "memory_limit = 512M", ";extension=gd", "date.timezone = UTC");

        PhpIniEditor.SetEnabled(_ini, "gd", true);

        var lines = Lines();
        Assert.Contains("[PHP]", lines);
        Assert.Contains("memory_limit = 512M", lines);
        Assert.Contains("date.timezone = UTC", lines);
    }

    [Fact]
    public void SetEnabled_throws_when_the_file_is_missing()
    {
        Assert.Throws<FileNotFoundException>(
            () => PhpIniEditor.SetEnabled(Path.Combine(_folder, "nothing.ini"), "gd", true));
    }

    [Fact]
    public void SetEnabled_collapses_duplicates_while_writing()
    {
        // This is the regression guard for the "already loaded" warning.
        Write("extension=gd", "extension=gd", ";extension=curl");

        PhpIniEditor.SetEnabled(_ini, "curl", true);

        Assert.Equal(1, Lines().Count(line => line.Trim() == "extension=gd"));
    }

    // ---------- Deduplicate ----------

    [Fact]
    public void Deduplicate_keeps_one_line_and_prefers_enabled()
    {
        Write(";extension=gd", "extension=gd", "extension=gd");

        PhpIniEditor.Deduplicate(_ini);

        var lines = Lines();
        Assert.Single(lines);
        Assert.Equal("extension=gd", lines[0]);
    }

    [Fact]
    public void Deduplicate_leaves_a_clean_file_alone()
    {
        var original = new[] { "[PHP]", "extension=gd", ";extension=intl" };
        Write(original);

        PhpIniEditor.Deduplicate(_ini);

        Assert.Equal(original, Lines());
    }

    // ---------- EnsureDefaults ----------

    [Fact]
    public void EnsureDefaults_enables_the_recommended_extensions()
    {
        Write("[PHP]");

        PhpIniEditor.EnsureDefaults(_ini);

        var found = PhpIniEditor.Read(_ini);
        foreach (var name in new[] { "curl", "gd", "mbstring", "mysqli", "openssl", "pdo_mysql", "zip" })
        {
            Assert.True(IsOn(found, name), $"{name} should be enabled");
        }
    }

    [Fact]
    public void EnsureDefaults_is_idempotent()
    {
        Write("[PHP]");

        PhpIniEditor.EnsureDefaults(_ini);
        var first = Lines();

        PhpIniEditor.EnsureDefaults(_ini);
        var second = Lines();

        Assert.Equal(first, second);
        Assert.Equal(1, second.Count(line => line.Trim() == "extension=gd"));
    }

    [Fact]
    public void EnsureDefaults_does_not_re_enable_what_the_user_turned_off()
    {
        Write("[PHP]");
        PhpIniEditor.EnsureDefaults(_ini);

        PhpIniEditor.SetEnabled(_ini, "gd", false);
        PhpIniEditor.EnsureDefaults(_ini);

        Assert.False(IsOn(PhpIniEditor.Read(_ini), "gd"));
    }
}