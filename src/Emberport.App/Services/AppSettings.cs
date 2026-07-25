using System;
using System.IO;
using System.Text.Json;

namespace Emberport.Services;

/// <summary>User choices that must survive a restart, stored as config\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Current { get; } = Load();

    public static string FilePath => Path.Combine(AppPaths.ConfigRoot, "settings.json");

    public string? PhpVersion { get; set; }

    public int ApachePort { get; set; } = 80;

    public int MySqlPort { get; set; } = 3306;

    public int RedisPort { get; set; } = 6379;

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ConfigRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Options));
        }
        catch (Exception)
        {
            // Settings are a convenience, never a reason to crash the app.
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // A corrupt file falls back to defaults instead of blocking startup.
        }

        return new AppSettings();
    }
}