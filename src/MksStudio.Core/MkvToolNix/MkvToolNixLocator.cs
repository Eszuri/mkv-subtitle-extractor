using System.IO;

namespace MksStudio.Core.MkvToolNix;

/// <summary>
/// Locates MKVToolNix executables (mkvmerge, mkvextract, mkvpropedit).
/// </summary>
public static class MkvToolNixLocator
{
    private static string? _customPath;

    public static string? CustomPath
    {
        get => _customPath;
        set => _customPath = value;
    }

    public static string? GetInstallationDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_customPath) && Directory.Exists(_customPath))
        {
            if (File.Exists(Path.Combine(_customPath, "mkvmerge.exe")))
                return _customPath;
        }

        string[] searchPaths = [
            @"C:\Program Files\MKVToolNix",
            @"C:\Program Files (x86)\MKVToolNix",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MKVToolNix"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "mkvtoolnix"),
            AppDomain.CurrentDomain.BaseDirectory
        ];

        foreach (var path in searchPaths)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "mkvmerge.exe")))
            {
                return path;
            }
        }

        // Check PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Directory.Exists(p) && File.Exists(Path.Combine(p, "mkvmerge.exe")))
                {
                    return p;
                }
            }
        }

        return null;
    }

    public static bool IsAvailable() => GetInstallationDirectory() != null;

    public static string? GetMkvMergePath()
    {
        var dir = GetInstallationDirectory();
        if (dir == null) return null;
        string exe = Path.Combine(dir, "mkvmerge.exe");
        return File.Exists(exe) ? exe : null;
    }

    public static string? GetMkvExtractPath()
    {
        var dir = GetInstallationDirectory();
        if (dir == null) return null;
        string exe = Path.Combine(dir, "mkvextract.exe");
        return File.Exists(exe) ? exe : null;
    }

    public static string? GetMkvPropEditPath()
    {
        var dir = GetInstallationDirectory();
        if (dir == null) return null;
        string exe = Path.Combine(dir, "mkvpropedit.exe");
        return File.Exists(exe) ? exe : null;
    }
}
