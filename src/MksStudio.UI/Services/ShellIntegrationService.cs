using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MksStudio.UI.Services;

/// <summary>
/// Manages Windows Explorer shell integration such as the "Export Subtitle" context menu for .mkv files.
/// </summary>
public static class ShellIntegrationService
{
    private const string ContextMenuKey = @"Software\Classes\SystemFileAssociations\.mkv\shell\MksStudioExport";
    private const string MenuTitle = "Export Subtitle";

    /// <summary>
    /// Registers or updates the context menu item in HKCU so it works without administrator privileges.
    /// </summary>
    public static void RegisterContextMenu()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? Environment.ProcessPath
                ?? string.Empty;

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            using var key = Registry.CurrentUser.CreateSubKey(ContextMenuKey);
            if (key == null) return;

            key.SetValue(string.Empty, MenuTitle);
            key.SetValue("Icon", $"\"{exePath}\",0");

            using var cmdKey = key.CreateSubKey("command");
            cmdKey?.SetValue(string.Empty, $"\"{exePath}\" --quick-export \"%1\"");
        }
        catch
        {
            // Silently ignore if registry write is restricted by group policy
        }
    }

    /// <summary>
    /// Removes the context menu from HKCU.
    /// </summary>
    public static void UnregisterContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ContextMenuKey, throwOnMissingSubKey: false);
        }
        catch
        {
            // Silently ignore
        }
    }
}
