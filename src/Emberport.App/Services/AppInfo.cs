using System;
using System.Reflection;

namespace Emberport.Services;

/// <summary>Application identity read from the assembly, so the version lives only in the csproj.</summary>
public static class AppInfo
{
    public const string Name = "Emberport";
    public const string Author = "Hojjat Jahanpour";
    public const string RepositoryUrl = "https://github.com/hojjatjh/Emberport";

    /// <summary>Version without build metadata, for example "0.4.0".</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Version with a leading v, for example "v0.4.0".</summary>
    public static string DisplayVersion => $"v{Version}";

    /// <summary>Footer and about line, for example "Emberport v0.4.0 by Hojjat Jahanpour".</summary>
    public static string Signature => $"{Name} {DisplayVersion} by {Author}";

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip source revision metadata such as "0.4.0+abc1234".
            var plus = informational.IndexOf('+');

            return plus > 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;

        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}