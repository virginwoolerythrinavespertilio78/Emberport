using System.IO;
using System.Linq;

namespace Emberport.Services;

/// <summary>Resolves the workspace folders that Emberport operates on.</summary>
public static class AppPaths
{
	private static readonly string[] ServiceFolders = ["php", "apache", "mysql", "redis"];

	private static readonly string[] RequiredFolders =
	[
		"bin",
		Path.Combine("bin", "apache"),
		Path.Combine("bin", "php"),
		Path.Combine("bin", "mysql"),
		Path.Combine("bin", "redis"),
		"tools",
		"config",
		"data",
		"backups",
		"www",
	];

	static AppPaths()
	{
		EnsureFolders();
	}

	public static string WorkspaceRoot { get; } = ResolveWorkspaceRoot();

	/// <summary>True when the workspace was found next to the executable, as it is after an install.</summary>
	public static bool IsInstalled { get; private set; }

	public static string BinariesRoot => Path.Combine(WorkspaceRoot, "bin");

	public static string PhpRoot => Path.Combine(BinariesRoot, "php");

	public static string ToolsRoot => Path.Combine(WorkspaceRoot, "tools");

	/// <summary>The folder shipped with Emberport, used when no custom root is set.</summary>
	public static string DefaultWwwRoot => Path.Combine(WorkspaceRoot, "www");

	// A custom root lets projects live on any drive, the way Laragon allows.
	public static string WwwRoot
	{
		get
		{
			var configured = AppSettings.Current.DocumentRoot;

			return string.IsNullOrWhiteSpace(configured) ? DefaultWwwRoot : configured;
		}
	}

	public static string DataRoot => Path.Combine(WorkspaceRoot, "data");

	public static string ConfigRoot => Path.Combine(WorkspaceRoot, "config");

	public static string BackupsRoot => Path.Combine(WorkspaceRoot, "backups");

	// An installed copy always owns the folder it sits in, so the executable's own
	// directory wins before anything else is considered. Only a development build,
	// which runs from src\...\bin\Debug, is allowed to walk up and look for the
	// repository workspace. Without that rule an unrelated parent folder could
	// hijack the installation.
	private static string ResolveWorkspaceRoot()
	{
		var baseDirectory = TrimSeparator(AppContext.BaseDirectory);

		if (LooksLikeWorkspace(baseDirectory))
		{
			IsInstalled = true;

			return baseDirectory;
		}

		var directory = new DirectoryInfo(baseDirectory).Parent;

		while (directory is not null)
		{
			if (LooksLikeWorkspace(directory.FullName))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		// Nothing recognisable above us: treat this as a fresh portable copy and
		// build the workspace right here rather than borrowing someone else's.
		IsInstalled = true;

		return baseDirectory;
	}

	private static bool LooksLikeWorkspace(string path)
	{
		var binaries = Path.Combine(path, "bin");

		return Directory.Exists(binaries)
			&& ServiceFolders.Any(folder => Directory.Exists(Path.Combine(binaries, folder)));
	}

	// A fresh install, a moved folder or a user who deleted something by hand should
	// never crash the app. Missing folders are simply recreated, and a folder we are
	// not allowed to write to is left to the feature that actually needs it, so the
	// error surfaces with real context instead of at startup.
	private static void EnsureFolders()
	{
		foreach (var folder in RequiredFolders)
		{
			try
			{
				Directory.CreateDirectory(Path.Combine(WorkspaceRoot, folder));
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}

	private static string TrimSeparator(string path)
		=> path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
