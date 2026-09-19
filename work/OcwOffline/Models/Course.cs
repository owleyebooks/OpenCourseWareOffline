using SQLite;

namespace OcwOffline.Models;

public class Course
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty; // e.g. "hst-508-genomics-and-computational-biology-fall-2002"

    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }

    // Not in the original brief, but useful for a catalog UI
    public DateTime? LastScrapedUtc { get; set; }
    public string? Term { get; set; } // e.g. "Fall 2002"
}
