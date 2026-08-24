using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MksStudio.Core.MkvToolNix.Models;

namespace MksStudio.Core.MkvToolNix;

/// <summary>
/// Service wrapping mkvmerge CLI operations (identification and demuxing to .mks).
/// </summary>
public class MkvMergeService
{
    /// <summary>
    /// Identifies all tracks and attachments in an MKV file using mkvmerge -J.
    /// </summary>
    public async Task<MkvIdentifyResult> IdentifyAsync(string mkvPath)
    {
        string? exe = MkvToolNixLocator.GetMkvMergePath();
        if (exe == null)
            throw new FileNotFoundException("mkvmerge.exe was not found. Please ensure MKVToolNix is installed.");

        if (!File.Exists(mkvPath))
            throw new FileNotFoundException($"Input file not found: {mkvPath}");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"-J \"{mkvPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        string json = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"mkvmerge failed (Exit Code {process.ExitCode}): {error}");
        }

        var result = JsonSerializer.Deserialize<MkvIdentifyResult>(json);
        return result ?? new MkvIdentifyResult();
    }

    /// <summary>
    /// Remuxes MKV directly into a pure .mks container by dropping video and audio (-D -A).
    /// </summary>
    public async Task<bool> ExtractToMksAsync(
        string mkvPath,
        string outputMksPath,
        IEnumerable<int>? subtitleTrackIds = null,
        bool includeAttachments = true,
        IProgress<string>? logProgress = null)
    {
        string? exe = MkvToolNixLocator.GetMkvMergePath();
        if (exe == null)
            throw new FileNotFoundException("mkvmerge.exe was not found. Please ensure MKVToolNix is installed.");

        var argsBuilder = new StringBuilder();
        argsBuilder.Append($"-o \"{outputMksPath}\" -D -A ");

        if (subtitleTrackIds != null && subtitleTrackIds.Any())
        {
            string ids = string.Join(",", subtitleTrackIds);
            argsBuilder.Append($"--subtitle-tracks {ids} ");
        }

        if (!includeAttachments)
        {
            argsBuilder.Append("--no-attachments ");
        }

        argsBuilder.Append($"\"{mkvPath}\"");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = argsBuilder.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                logProgress?.Report(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();

        return process.ExitCode is 0 or 1; // 0 = ok, 1 = ok with warnings
    }
}
