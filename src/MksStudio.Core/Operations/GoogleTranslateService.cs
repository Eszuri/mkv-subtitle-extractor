using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MksStudio.Core.Operations.Models;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Operations;

/// <summary>
/// Service for performing online machine translation using Google Translate.
/// Includes automatic subtitle tag protection, batching, and progress reporting.
/// </summary>
public class GoogleTranslateService
{
    private readonly HttpClient _httpClient;
    private const string ChromeEndpoint = "https://clients5.google.com/translate_a/t";
    private const string GoogleApisEndpoint = "https://translate.googleapis.com/translate_a/single";
    private const string BatchDelimiter = "\n@@@\n";

    public GoogleTranslateService(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(25)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }
    }

    /// <summary>
    /// Translates a single text string from source language to target language.
    /// </summary>
    public async Task<string> TranslateTextAsync(string text, string sourceLang = "auto", string targetLang = "id", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string sl = string.IsNullOrWhiteSpace(sourceLang) ? "auto" : sourceLang;
        string tl = string.IsNullOrWhiteSpace(targetLang) ? "id" : targetLang;

        // 1. Primary: Official Chrome Extension Endpoint via POST (Handles batches with no URI length limits)
        try
        {
            string url = $"{ChromeEndpoint}?client=dict-chrome-ex&sl={Uri.EscapeDataString(sl)}&tl={Uri.EscapeDataString(tl)}";
            using var form = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", text)]);
            using var response = await _httpClient.PostAsync(url, form, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var jsonBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonBytes);
                string parsed = ParseTranslationResponse(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(parsed))
                    return parsed;
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Fallback to GET
        }

        // 2. Secondary: Chrome Extension Endpoint via GET
        try
        {
            string url = $"{ChromeEndpoint}?client=dict-chrome-ex&sl={Uri.EscapeDataString(sl)}&tl={Uri.EscapeDataString(tl)}&q={Uri.EscapeDataString(text)}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var jsonBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonBytes);
                string parsed = ParseTranslationResponse(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(parsed))
                    return parsed;
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Fallback to GoogleApis
        }

        // 3. Tertiary: GoogleApis Endpoint with dict-chrome-ex client
        try
        {
            string url = $"{GoogleApisEndpoint}?client=dict-chrome-ex&sl={Uri.EscapeDataString(sl)}&tl={Uri.EscapeDataString(tl)}&dt=t&q={Uri.EscapeDataString(text)}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var jsonBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonBytes);
                string parsed = ParseTranslationResponse(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(parsed))
                    return parsed;
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Fallback to tw-ob
        }

        // 4. Quaternary: Alternate tw-ob client on Chrome endpoint
        try
        {
            string url = $"{ChromeEndpoint}?client=tw-ob&sl={Uri.EscapeDataString(sl)}&tl={Uri.EscapeDataString(tl)}&q={Uri.EscapeDataString(text)}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var jsonBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonBytes);
                string parsed = ParseTranslationResponse(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(parsed))
                    return parsed;
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // If all fail, return original text
        }

        return text;
    }

    /// <summary>
    /// Translates a list of SubtitleCues asynchronously with batching, tag protection, and progress reporting.
    /// </summary>
    public async Task<List<SubtitleCue>> TranslateCuesAsync(
        IList<SubtitleCue> cues,
        string sourceLang = "auto",
        string targetLang = "id",
        int batchSize = 10,
        IProgress<TranslationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new List<SubtitleCue>(cues.Count);
        if (cues.Count == 0) return result;

        int totalCount = cues.Count;
        int processedCount = 0;

        for (int i = 0; i < cues.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batch = cues.Skip(i).Take(batchSize).ToList();
            var protectedBatch = new List<(SubtitleCue Original, string ProtectedText, List<string> Tags)>();

            foreach (var cue in batch)
            {
                var (pText, tags) = SubtitleTagProtector.Protect(cue.RawText);
                protectedBatch.Add((cue, pText, tags));
            }

            try
            {
                // Combine into single batch query
                string joinedQuery = string.Join(BatchDelimiter, protectedBatch.Select(b => b.ProtectedText));
                string translatedBatch = await TranslateTextAsync(joinedQuery, sourceLang, targetLang, ct).ConfigureAwait(false);

                // Split result by delimiter
                string[] parts = Regex.Split(translatedBatch, @"\s*@\s*@\s*@\s*", RegexOptions.None);

                if (parts.Length == protectedBatch.Count)
                {
                    for (int j = 0; j < protectedBatch.Count; j++)
                    {
                        var item = protectedBatch[j];
                        string cleanedTranslated = parts[j].Trim('\r', '\n');
                        string restoredText = SubtitleTagProtector.Restore(cleanedTranslated, item.Tags);
                        if (string.IsNullOrWhiteSpace(restoredText))
                        {
                            restoredText = item.Original.RawText;
                        }

                        var newCue = item.Original.Clone();
                        newCue.RawText = restoredText;
                        result.Add(newCue);
                        processedCount++;

                        progress?.Report(new TranslationProgress
                        {
                            CueIndex = processedCount - 1,
                            ProcessedCount = processedCount,
                            TotalCount = totalCount,
                            StatusMessage = $"Menerjemahkan {processedCount} dari {totalCount} baris ({((double)processedCount / totalCount * 100):0.0}%)...",
                            CurrentText = item.Original.RawText,
                            TranslatedPreview = restoredText
                        });
                    }
                }
                else
                {
                    // Delimiter split mismatch: fallback to translating cues one-by-one in this batch
                    foreach (var item in protectedBatch)
                    {
                        ct.ThrowIfCancellationRequested();
                        string singleTrans = await TranslateTextAsync(item.ProtectedText, sourceLang, targetLang, ct).ConfigureAwait(false);
                        string restoredText = SubtitleTagProtector.Restore(singleTrans, item.Tags);
                        if (string.IsNullOrWhiteSpace(restoredText))
                        {
                            restoredText = item.Original.RawText;
                        }

                        var newCue = item.Original.Clone();
                        newCue.RawText = restoredText;
                        result.Add(newCue);
                        processedCount++;

                        progress?.Report(new TranslationProgress
                        {
                            CueIndex = processedCount - 1,
                            ProcessedCount = processedCount,
                            TotalCount = totalCount,
                            StatusMessage = $"Menerjemahkan {processedCount} dari {totalCount} baris ({((double)processedCount / totalCount * 100):0.0}%)...",
                            CurrentText = item.Original.RawText,
                            TranslatedPreview = restoredText
                        });
                    }
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Fallback for this batch to individual translation
                foreach (var item in protectedBatch)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        string singleTrans = await TranslateTextAsync(item.ProtectedText, sourceLang, targetLang, ct).ConfigureAwait(false);
                        string restoredText = SubtitleTagProtector.Restore(singleTrans, item.Tags);
                        if (string.IsNullOrWhiteSpace(restoredText))
                        {
                            restoredText = item.Original.RawText;
                        }

                        var newCue = item.Original.Clone();
                        newCue.RawText = restoredText;
                        result.Add(newCue);

                        progress?.Report(new TranslationProgress
                        {
                            CueIndex = processedCount,
                            ProcessedCount = processedCount + 1,
                            TotalCount = totalCount,
                            StatusMessage = $"Menerjemahkan {processedCount + 1} dari {totalCount} baris ({((double)(processedCount + 1) / totalCount * 100):0.0}%)...",
                            CurrentText = item.Original.RawText,
                            TranslatedPreview = restoredText
                        });
                    }
                    catch
                    {
                        // If single translation also fails, preserve original cue
                        result.Add(item.Original.Clone());
                        progress?.Report(new TranslationProgress
                        {
                            CueIndex = processedCount,
                            ProcessedCount = processedCount + 1,
                            TotalCount = totalCount,
                            StatusMessage = $"Menerjemahkan {processedCount + 1} dari {totalCount} baris ({((double)(processedCount + 1) / totalCount * 100):0.0}%)...",
                            CurrentText = item.Original.RawText,
                            TranslatedPreview = item.Original.RawText
                        });
                    }
                    processedCount++;
                }
            }

            // Brief polite pause between batches
            if (i + batchSize < cues.Count)
            {
                await Task.Delay(80, ct).ConfigureAwait(false);
            }
        }

        return result;
    }

    /// <summary>
    /// Universally parses Google Translate JSON response across all endpoint schemas:
    /// 1. Direct string: "text"
    /// 2. Simple array: ["translated text"]
    /// 3. Auto-detected array: [["translated text", "detected_lang"]]
    /// 4. Segmented multi-part array: [[["seg1", "orig1"], ["seg2", "orig2"]], null, "lang"]
    /// </summary>
    public static string ParseTranslationResponse(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.String)
        {
            return root.GetString() ?? string.Empty;
        }

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return string.Empty;

        var firstElem = root[0];

        // Format 2: ["translated text"]
        if (firstElem.ValueKind == JsonValueKind.String)
        {
            return firstElem.GetString() ?? string.Empty;
        }

        if (firstElem.ValueKind == JsonValueKind.Array && firstElem.GetArrayLength() > 0)
        {
            var nestedFirst = firstElem[0];

            // Format 3: [ ["translated text", "detected_lang"] ]
            if (nestedFirst.ValueKind == JsonValueKind.String)
            {
                return nestedFirst.GetString() ?? string.Empty;
            }

            // Format 4: [ [ ["segment1", ...], ["segment2", ...] ], ... ]
            if (nestedFirst.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (var seg in firstElem.EnumerateArray())
                {
                    if (seg.ValueKind == JsonValueKind.Array && seg.GetArrayLength() > 0)
                    {
                        var segPart = seg[0];
                        if (segPart.ValueKind == JsonValueKind.String)
                        {
                            var s = segPart.GetString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                sb.Append(s);
                            }
                        }
                    }
                }
                return sb.ToString();
            }
        }

        return string.Empty;
    }
}
