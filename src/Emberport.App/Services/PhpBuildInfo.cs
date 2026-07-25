using System.IO;
using System.Linq;
using Emberport.Models;

namespace Emberport.Services;

/// <summary>Tells a Thread Safe PHP build apart from a Non Thread Safe one.</summary>
public static class PhpBuildInfo
{
    // Only Thread Safe builds ship the Apache module, so its presence is the test.
    public static bool IsThreadSafe(BinaryInstallation installation)
    {
        if (!Directory.Exists(installation.DirectoryPath))
        {
            return false;
        }

        return Directory
            .EnumerateFiles(installation.DirectoryPath, "php*apache2_4.dll")
            .Any();
    }
}