using System.Text.Json;
using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// Catalog data source — MIT Learn's public courses API (learn.mit.edu),
/// not OCW's own client-rendered listing pages. Verified live against RC
/// host only; no confirmed search param. Full detail: AUDIT_TRAIL v14/v23.
/// </summary>
public class OcwCatalogService : IOcwCatalogService
{
    private const string BaseUrl = "https://api.learn.mit.edu";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30) // plain JSON call, not a resumable download — fail fast instead of hanging
    };

    /// <summary>
    /// Fetches one page of OCW courses. Returns (entries, hasMore) —
    /// hasMore mirrors whether the API's own "next" field was non-null,
    /// so paging logic doesn't have to guess from a short/full page.
    /// </summary>
    public async Task<(List<CatalogEntry> Entries, bool HasMore)> GetCoursePageAsync(int offset, int limit = 20, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/v1/courses/?platform=ocw&limit={limit}&offset={offset}";
        var json = await _http.GetStringAsync(url, ct);
        var page = JsonSerializer.Deserialize<CatalogPageResponse>(json, JsonOptions)
                   ?? new CatalogPageResponse();

        var entries = page.Results
            .Select(ToCatalogEntry)
            .Where(e => e is not null)
            .Cast<CatalogEntry>()
            .ToList();

        return (entries, !string.IsNullOrEmpty(page.Next));
    }

    /// <remarks>internal, not private, since v30 — same file-linked-into-Tests
    /// mechanism as SlugFromUrl (v29); behavior unchanged. See AUDIT_TRAIL v30.</remarks>
    internal static CatalogEntry? ToCatalogEntry(CatalogCourseDto dto)
    {
        var slug = SlugFromUrl(dto.Url);
        if (slug is null) return null; // no usable course URL — skip rather than show a dead entry

        var department = dto.Departments?.FirstOrDefault()?.Name;
        var run = dto.Runs?.FirstOrDefault();
        var term = run is { Semester: not null, Year: not null } ? $"{run.Semester} {run.Year}" : null;
        var subtitle = string.Join(" · ", new[] { department, term }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new CatalogEntry
        {
            Slug = slug,
            Title = dto.Title,
            Subtitle = subtitle
        };
    }

    /// <summary>Extracts "hst-508-genomics-..." from ".../courses/hst-508-genomics-.../" — same slug shape Course.Id and CourseViewModel.CourseSlug already use everywhere else.</summary>
    /// <remarks>internal, not private, since v29 — file-linked directly into
    /// OcwOffline.Tests (same compiled assembly there, no InternalsVisibleTo
    /// needed); behavior unchanged. See AUDIT_TRAIL v29.</remarks>
    internal static string? SlugFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var coursesIndex = Array.IndexOf(segments, "courses");
        if (coursesIndex < 0 || coursesIndex + 1 >= segments.Length)
            return null;

        return segments[coursesIndex + 1];
    }

    // internal, not private, since this round — the deserialization test
    // needs the app's real options, not a re-guessed copy. See AUDIT_TRAIL v38.
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Minimal DTOs — only the fields this app uses, not the full API shape. See header comment above.

    // internal, not private, since this round — same file-linked-into-Tests
    // treatment CatalogCourseDto already got at v30. See AUDIT_TRAIL v38.
    internal class CatalogPageResponse
    {
        public int Count { get; set; }
        public string? Next { get; set; }
        public List<CatalogCourseDto> Results { get; set; } = new();
    }

    // internal, not private, since v30 — ToCatalogEntry's params/return need
    // to be constructible from Tests. See AUDIT_TRAIL v30.
    internal class CatalogCourseDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Url { get; set; }
        public List<CatalogDepartmentDto>? Departments { get; set; }
        public List<CatalogRunDto>? Runs { get; set; }
    }

    internal class CatalogDepartmentDto
    {
        public string? Name { get; set; }
    }

    internal class CatalogRunDto
    {
        public string? Semester { get; set; }
        public int? Year { get; set; }
    }
}
