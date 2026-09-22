namespace OcwOffline.Models;

/// <summary>
/// Transient API search row, deliberately not a <see cref="Course"/>:
/// selecting one just feeds <c>CourseViewModel.CourseSlug</c>.
/// </summary>
public class CatalogEntry
{
    /// <summary>Course slug parsed from the API's course URL, e.g. "hst-508-genomics-and-computational-biology-fall-2002". Same format CourseViewModel.CourseSlug/Course.Id already use.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>e.g. "Mechanical Engineering · Fall 2009". Built for display only, not stored anywhere.</summary>
    public string Subtitle { get; set; } = string.Empty;
}
