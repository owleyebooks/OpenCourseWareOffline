using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// Scrapes MIT OCW's public /download/ pages (no official API). Resource
/// links are two adjacent &lt;a&gt; tags (label + title); YouTube-only
/// lectures never match and are silently skipped. See AUDIT_TRAIL v1/v16.
/// </summary>
public class OcwScraperService : IOcwScraperService
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    // Matches the inline "type size" label MIT OCW renders next to each
    // download link, e.g. "pdf 101 kB", "video 83 MB", "file 2 MB".
    private static readonly Regex FileLabelPattern = new(
        @"^\s*(pdf|video|file|zip)\s+([\d.]+)\s*(kB|KB|MB|GB)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Loose net for FileLabelPattern near-misses (e.g. a unit MIT hasn't used
    // yet): flags instead of silently dropping.
    private static readonly Regex SizeUnitHint = new(
        @"\d\s*(kB|KB|MB|GB)", RegexOptions.IgnoreCase);

    public async Task<ScrapedCourse> ScrapeDownloadPageAsync(string courseId)
    {
        var url = $"https://ocw.mit.edu/courses/{courseId}/download/";
        var html = await _http.GetStringAsync(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var result = new ScrapedCourse
        {
            CourseId = courseId,
            SourceUrl = url,
            Title = ExtractTitle(doc)
        };

        result.ZipArchiveUrl = ExtractZipArchiveUrl(doc, courseId);

        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null)
            return result;

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var labelText = HtmlEntity.DeEntitize(anchors[i].InnerText).Trim();
            var match = FileLabelPattern.Match(labelText);
            if (!match.Success)
            {
                if (SizeUnitHint.IsMatch(labelText))
                    result.UnmatchedResourceLabels.Add(labelText);
                continue;
            }

            var fileHref = anchors[i].GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(fileHref)) continue;

            var titleAnchor = anchors[i + 1];
            var title = HtmlEntity.DeEntitize(titleAnchor.InnerText).Trim();

            var absoluteUrl = MakeAbsolute(fileHref);
            var sizeBytes = ParseSize(match.Groups[2].Value, match.Groups[3].Value);
            var typeLabel = match.Groups[1].Value.ToLowerInvariant();

            if (typeLabel == "video" || absoluteUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                result.Lectures.Add(new ScrapedLecture
                {
                    Title = title,
                    VideoUrl = absoluteUrl,
                    FileSizeBytes = sizeBytes
                });
            }
            else
            {
                result.Artifacts.Add(new ScrapedArtifact
                {
                    Title = title,
                    SourceUrl = absoluteUrl,
                    FileType = ClassifyArtifact(absoluteUrl, typeLabel),
                    FileSizeBytes = sizeBytes
                });
            }
        }

        return result;
    }

    // internal, not private: both take an HtmlDocument built from a literal
    // string (HtmlDocument.LoadHtml is itself I/O-free), no mock needed.
    internal static string ExtractTitle(HtmlDocument doc)
    {
        // The <title> tag is formatted "Resources | {Course Title} | ... | MIT OpenCourseWare"
        var raw = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "";
        var parts = raw.Split('|');
        return parts.Length >= 2 ? parts[1].Trim() : raw.Trim();
    }

    internal string? ExtractZipArchiveUrl(HtmlDocument doc, string courseId)
    {
        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null) return null;

        var zipAnchor = anchors.FirstOrDefault(a =>
            a.GetAttributeValue("href", "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        return zipAnchor is null ? null : MakeAbsolute(zipAnchor.GetAttributeValue("href", ""));
    }

    // internal, not private: pure BCL, no I/O.
    internal static string MakeAbsolute(string href)
    {
        if (href.StartsWith("http://") || href.StartsWith("https://"))
            return href;

        return "https://ocw.mit.edu" + (href.StartsWith("/") ? href : "/" + href);
    }

    internal static long ParseSize(string numeric, string unit)
    {
        if (!double.TryParse(numeric, out var value)) return 0;

        var multiplier = unit.ToUpperInvariant() switch
        {
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            _ => 1L
        };

        return (long)(value * multiplier);
    }

    internal static ArtifactFileType ClassifyArtifact(string url, string typeLabel)
    {
        if (typeLabel == "zip" || url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return ArtifactFileType.Zip;
        if (typeLabel == "pdf" || url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return ArtifactFileType.Pdf;
        if (url.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            return ArtifactFileType.Html;
        return ArtifactFileType.Other;
    }
}

public class ScrapedCourse
{
    public string CourseId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? ZipArchiveUrl { get; set; }
    public List<ScrapedArtifact> Artifacts { get; set; } = new();
    public List<ScrapedLecture> Lectures { get; set; } = new();
    public List<string> UnmatchedResourceLabels { get; set; } = new();
}

public class ScrapedArtifact
{
    public string Title { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public ArtifactFileType FileType { get; set; }
    public long FileSizeBytes { get; set; }
}

public class ScrapedLecture
{
    public string Title { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty; // empty if YouTube-only, no direct file found
    public long FileSizeBytes { get; set; }
}
