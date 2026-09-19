using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class OcwCatalogServiceSlugFromUrlTests
{
    [Fact]
    public void SlugFromUrl_StandardCourseUrl_ReturnsSlug()
    {
        var result = OcwCatalogService.SlugFromUrl(
            "https://ocw.mit.edu/courses/hst-508-genomics-and-computational-biology-fall-2002/");

        Assert.Equal("hst-508-genomics-and-computational-biology-fall-2002", result);
    }

    [Fact]
    public void SlugFromUrl_NoTrailingSlash_ReturnsSlug()
    {
        var result = OcwCatalogService.SlugFromUrl("https://ocw.mit.edu/courses/foo-bar-2020");

        Assert.Equal("foo-bar-2020", result);
    }

    [Fact]
    public void SlugFromUrl_NoCoursesSegment_ReturnsNull()
    {
        var result = OcwCatalogService.SlugFromUrl("https://ocw.mit.edu/departments/mathematics/");

        Assert.Null(result);
    }

    [Fact]
    public void SlugFromUrl_CoursesIsLastSegment_ReturnsNull()
    {
        var result = OcwCatalogService.SlugFromUrl("https://ocw.mit.edu/courses");

        Assert.Null(result);
    }

    [Fact]
    public void SlugFromUrl_CoursesIsLastSegmentWithTrailingSlash_ReturnsNull()
    {
        var result = OcwCatalogService.SlugFromUrl("https://ocw.mit.edu/courses/");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("null returns null", null)]
    [InlineData("empty string returns null", "")]
    [InlineData("whitespace returns null", "   ")]
    public void SlugFromUrl_NullOrWhitespace_ReturnsNull(string description, string? url)
    {
        var result = OcwCatalogService.SlugFromUrl(url);

        Assert.Null(result);
    }

    [Fact]
    public void SlugFromUrl_RelativeUrl_ReturnsNull()
    {
        var result = OcwCatalogService.SlugFromUrl("courses/foo-bar");

        Assert.Null(result);
    }

    [Fact]
    public void SlugFromUrl_NotAUrl_ReturnsNull()
    {
        var result = OcwCatalogService.SlugFromUrl("not a url at all");

        Assert.Null(result);
    }

    [Fact]
    public void SlugFromUrl_CaseSensitive_UppercaseCoursesSegmentDoesNotMatch()
    {
        var result = OcwCatalogService.SlugFromUrl("https://ocw.mit.edu/Courses/foo-bar");

        Assert.Null(result);
    }

    [Fact]
    public void SlugFromUrl_RepeatedCoursesSegment_UsesFirstMatch()
    {
        // Documents actual behavior: Array.IndexOf finds the first "courses"
        // segment, so the segment immediately after *that* one is returned.
        // Here, the second literal "courses" segment is used, not "foo".
        var result = OcwCatalogService.SlugFromUrl("https://ocw.mit.edu/api/courses/courses/foo");

        Assert.Equal("courses", result);
    }
}
