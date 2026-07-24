namespace Emberport.Models;

/// <summary>A single service installation discovered inside the binaries folder.</summary>
public sealed record BinaryInstallation
{
    public required ServiceKind Kind { get; init; }

    /// <summary>Version parsed from the folder name, for example "8.3.4".</summary>
    public required string Version { get; init; }

    public required string DirectoryPath { get; init; }

    public required string ExecutablePath { get; init; }

    public string DisplayName => $"{Kind.ToDisplayName()} {Version}";
}