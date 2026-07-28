using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Emberport.Services;

/// <summary>
/// Registers Emberport in the per user Run key so Windows launches it at sign in.
/// No administrator rights and no Windows service are involved.
/// </summary>
public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Emberport";

    /// <summary>Passed to the application so a startup launch can stay in the notification area.</summary>
    public const string TrayArgument = "--tray";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);

                return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    /// <summary>Writes the current executable path, so a moved or rebuilt app stays correct.</summary>
    public static bool Enable()
    {
        var executable = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);

            if (key is null)
            {
                return false;
            }

            key.SetValue(ValueName, $"\"{executable}\" {TrayArgument}", RegistryValueKind.String);

            return true;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);

            key?.DeleteValue(ValueName, throwOnMissingValue: false);

            return true;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Flips the setting. Returns the state that is actually stored afterwards.</summary>
    public static bool Toggle()
    {
        if (IsEnabled)
        {
            Disable();
        }
        else
        {
            Enable();
        }

        return IsEnabled;
    }
}