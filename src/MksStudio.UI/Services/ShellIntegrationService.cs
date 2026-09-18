using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MksStudio.UI.Services;

/// <summary>
/// Manages Windows Explorer shell integration such as the "Export Subtitle" context menu for .mkv files
/// and "Translate Subtitle" context menu for subtitle files.
/// </summary>
public static class ShellIntegrationService
{
    private const string MkvExportKey = @"Software\Classes\SystemFileAssociations\.mkv\shell\MksStudioExport";
    private const string MkvExportTitle = "Export Subtitle";

    private const string TranslateSubKeySuffix = @"\shell\MksStudioTranslate";
    private const string TranslateTitle = "Translate Subtitle";

    private static readonly string[] SubtitleExtensions =
    [
        ".srt", ".ass", ".ssa", ".vtt",
        ".ttml", ".dfxp", ".xml",
        ".smi", ".sami",
        ".sub",
        ".sbv",
        ".lrc"
    ];

    /// <summary>
    /// Registers or updates context menu items in HKCU so they work without administrator privileges.
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

            // 1. Context menu for .mkv (Export Subtitle)
            using (var key = Registry.CurrentUser.CreateSubKey(MkvExportKey))
            {
                if (key != null)
                {
                    key.SetValue(string.Empty, MkvExportTitle);
                    key.SetValue("Icon", $"\"{exePath}\",0");

                    using var cmdKey = key.CreateSubKey("command");
                    cmdKey?.SetValue(string.Empty, $"\"{exePath}\" --quick-export \"%1\"");
                }
            }

            // 2. Context menu for subtitle files (Translate Subtitle)
            foreach (var ext in SubtitleExtensions)
            {
                string regPath = $@"Software\Classes\SystemFileAssociations\{ext}{TranslateSubKeySuffix}";
                using var key = Registry.CurrentUser.CreateSubKey(regPath);
                if (key != null)
                {
                    key.SetValue(string.Empty, TranslateTitle);
                    key.SetValue("Icon", $"\"{exePath}\",0");

                    using var cmdKey = key.CreateSubKey("command");
                    cmdKey?.SetValue(string.Empty, $"\"{exePath}\" --quick-translate \"%1\"");
                }
            }
        }
        catch
        {
            // Silently ignore if registry write is restricted by group policy
        }
    }

    /// <summary>
    /// Removes context menu items from HKCU.
    /// </summary>
    public static void UnregisterContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(MkvExportKey, throwOnMissingSubKey: false);

            foreach (var ext in SubtitleExtensions)
            {
                string regPath = $@"Software\Classes\SystemFileAssociations\{ext}{TranslateSubKeySuffix}";
                Registry.CurrentUser.DeleteSubKeyTree(regPath, throwOnMissingSubKey: false);
            }
        }
        catch
        {
            // Silently ignore
        }
    }
}
