using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.MkvToolNix.Models;

namespace MksStudio.Core.MkvToolNix;

/// <summary>
/// Service wrapping mkvextract CLI operations to extract subtitle tracks and attachments.
/// </summary>
public class MkvExtractService
{
    private static readonly Regex ProgressRegex = new(@"Progress:\s*(\d+)%", RegexOptions.Compiled);

    public async Task<bool> ExtractTracksAsync(
        string mkvPath,
        IDictionary<int, string> trackOutputs,
        IProgress<int>? progress = null,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        if (trackOutputs.Count == 0) return true;

        string? exe = MkvToolNixLocator.GetMkvExtractPath();
        if (exe == null)
            throw new FileNotFoundException("mkvextract.exe was not found. Please ensure MKVToolNix is installed.");

        var argsBuilder = new StringBuilder();
        argsBuilder.Append($"tracks \"{mkvPath}\"");

        foreach (var kvp in trackOutputs)
        {
            // Ensure parent directory exists
            string? dir = Path.GetDirectoryName(kvp.Value);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            argsBuilder.Append($" {kvp.Key}:\"{kvp.Value}\"");
        }

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
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            log?.Report(e.Data);
            var m = ProgressRegex.Match(e.Data);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int percent))
            {
                progress?.Report(percent);
            }
        };

        process.Start();
        process.BeginOutputReadLine();

        await process.WaitForExitAsync(ct);
        progress?.Report(100);

        return process.ExitCode is 0 or 1;
    }

    public async Task<bool> ExtractAttachmentsAsync(
        string mkvPath,
        IDictionary<int, string> attachmentOutputs,
        IProgress<int>? progress = null,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        if (attachmentOutputs.Count == 0) return true;

        string? exe = MkvToolNixLocator.GetMkvExtractPath();
        if (exe == null)
            throw new FileNotFoundException("mkvextract.exe was not found. Please ensure MKVToolNix is installed.");

        var argsBuilder = new StringBuilder();
        argsBuilder.Append($"attachments \"{mkvPath}\"");

        foreach (var kvp in attachmentOutputs)
        {
            string? dir = Path.GetDirectoryName(kvp.Value);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            argsBuilder.Append($" {kvp.Key}:\"{kvp.Value}\"");
        }

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
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            log?.Report(e.Data);
            var m = ProgressRegex.Match(e.Data);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int percent))
            {
                progress?.Report(percent);
            }
        };

        process.Start();
        process.BeginOutputReadLine();

        await process.WaitForExitAsync(ct);
        progress?.Report(100);

        return process.ExitCode is 0 or 1;
    }

    /// <summary>
    /// Extracts all or selected subtitle tracks using the MKV base name.
    /// </summary>
    public async Task<List<string>> ExtractSubtitlesAutoAsync(
        string mkvPath,
        string outputFolder,
        IEnumerable<int>? selectedTrackIds = null,
        bool extractFonts = true,
        IProgress<int>? progress = null,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        var mergeService = new MkvMergeService();
        var info = await mergeService.IdentifyAsync(mkvPath);
        var subTracks = info.SubtitleTracks.ToList();

        if (selectedTrackIds != null && selectedTrackIds.Any())
        {
            var idSet = selectedTrackIds.ToHashSet();
            subTracks = subTracks.Where(t => idSet.Contains(t.Id)).ToList();
        }

        if (subTracks.Count == 0)
        {
            log?.Report("No subtitle tracks found matching criteria.");
            return [];
        }

        string baseName = Path.GetFileNameWithoutExtension(mkvPath);
        var trackMap = new Dictionary<int, string>();
        var outputFiles = new List<string>();

        bool singleTrack = subTracks.Count == 1;
        foreach (var track in subTracks)
        {
            string ext = track.DefaultExtension;
            string langSuffix = !string.IsNullOrWhiteSpace(track.Language) && track.Language != "und"
                ? $".{track.Language}"
                : "";

            string outFileName = singleTrack
                ? $"{baseName}{ext}"
                : $"{baseName}{langSuffix}{ext}";

            // If file already mapped or exists with exact name, add track ID
            string outPath = Path.Combine(outputFolder, outFileName);
            if (trackMap.Values.Contains(outPath))
            {
                outPath = Path.Combine(outputFolder, $"{baseName}_track{track.Id}{langSuffix}{ext}");
            }

            trackMap[track.Id] = outPath;
            outputFiles.Add(outPath);
        }

        log?.Report($"Extracting {trackMap.Count} subtitle track(s) from '{Path.GetFileName(mkvPath)}'...");
        await ExtractTracksAsync(mkvPath, trackMap, progress, log, ct);

        // Extract Fonts if ASS subtitles are present
        if (extractFonts && info.Attachments.Count > 0)
        {
            string fontsDir = Path.Combine(outputFolder, "fonts");
            var attMap = new Dictionary<int, string>();
            foreach (var att in info.Attachments)
            {
                string safeName = Path.GetFileName(att.FileName);
                if (string.IsNullOrWhiteSpace(safeName)) safeName = $"attachment_{att.Id}";
                attMap[att.Id] = Path.Combine(fontsDir, safeName);
            }

            log?.Report($"Extracting {attMap.Count} font attachment(s)...");
            await ExtractAttachmentsAsync(mkvPath, attMap, null, log, ct);
        }

        return outputFiles;
    }
}
