using System.IO;
using System.Linq;

namespace Emberport.Services;

/// <summary>Resolves the workspace folders that Emberport operates on.</summary>
public static class AppPaths
{
    private static readonly string[] ServiceFolders = ["php", "apache", "mysql", "redis"];

    public static string WorkspaceRoot { get; } = ResolveWorkspaceRoot();

    public static string BinariesRoot => Path.Combine(WorkspaceRoot, "bin");

    public static string PhpRoot => Path.Combine(BinariesRoot, "php");

    public static string ToolsRoot => Path.Combine(WorkspaceRoot, "tools");

    public static string WwwRoot => Path.Combine(WorkspaceRoot, "www");

    public static string DataRoot => Path.Combine(WorkspaceRoot, "data");

    public static string ConfigRoot => Path.Combine(WorkspaceRoot, "config");

    // In a published build the workspace sits next to the executable. During development the
    // executable lives under src\...\bin\Debug, so walk up until the real workspace is found.
    private static string ResolveWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (LooksLikeWorkspace(directory.FullName))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static bool LooksLikeWorkspace(string path)
    {
        var binaries = Path.Combine(path, "bin");

        return Directory.Exists(binaries)
            && ServiceFolders.Any(folder => Directory.Exists(Path.Combine(binaries, folder)));
    }
}