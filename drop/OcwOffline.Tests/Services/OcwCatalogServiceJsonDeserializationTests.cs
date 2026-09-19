using System.Text.Json;
using OcwOffline.Services;
using Xunit;

namespace OcwOffline.Tests.Services;

/// <summary>
/// First test that parses real JSON — every other OcwCatalogService test
/// hand-builds the DTOs in C#, bypassing JsonSerializer entirely. Same
/// shape captured live this round. See AUDIT_TRAIL v38.
/// </summary>
public class OcwCatalogServiceJsonDeserializationTests
{
    // Lowercase keys, matching the real API's casing, so case-insensitive
    // matching is actually exercised. Escaped string, not a raw string
    // literal — csharp_lint.py's brace-stripper can't parse those. v38.
    private const string SampleCoursePageJson =
        "{" +
        "\"count\":2883," +
        "\"next\":\"http://api.rc.learn.mit.edu/api/v1/courses/?limit=10&offset=10&platform=ocw\"," +
        "\"results\":[{" +
        "\"title\":\"Power: Interpersonal, Organizational and Global Dimensions\"," +
        "\"url\":\"https://ocw.mit.edu/courses/21a-245j-power-interpersonal-organizational-and-global-dimensions-fall-2005/\"," +
        "\"departments\":[{\"name\":\"Anthropology\"},{\"name\":\"Political Science\"}]," +
        "\"runs\":[{\"semester\":\"Fall\",\"year\":2005}]" +
        "}]" +
        "}";

    [Fact]
    public void DeserializesRealApiShape_TopLevelFields()
    {
        var page = JsonSerializer.Deserialize<OcwCatalogService.CatalogPageResponse>(SampleCoursePageJson, OcwCatalogService.JsonOptions);

        Assert.NotNull(page);
        Assert.Equal(2883, page!.Count);
        Assert.Equal("http://api.rc.learn.mit.edu/api/v1/courses/?limit=10&offset=10&platform=ocw", page.Next);
        Assert.Single(page.Results);
    }

    [Fact]
    public void DeserializesRealApiShape_CourseFields()
    {
        var page = JsonSerializer.Deserialize<OcwCatalogService.CatalogPageResponse>(SampleCoursePageJson, OcwCatalogService.JsonOptions)!;
        var course = page.Results[0];

        Assert.Equal("Power: Interpersonal, Organizational and Global Dimensions", course.Title);
        Assert.Equal("https://ocw.mit.edu/courses/21a-245j-power-interpersonal-organizational-and-global-dimensions-fall-2005/", course.Url);
    }

    [Fact]
    public void DeserializesRealApiShape_DepartmentsAndRuns()
    {
        var page = JsonSerializer.Deserialize<OcwCatalogService.CatalogPageResponse>(SampleCoursePageJson, OcwCatalogService.JsonOptions)!;
        var course = page.Results[0];

        Assert.NotNull(course.Departments);
        Assert.Equal(2, course.Departments!.Count);
        Assert.Equal("Anthropology", course.Departments[0].Name);

        Assert.NotNull(course.Runs);
        var run = Assert.Single(course.Runs!);
        Assert.Equal("Fall", run.Semester);
        Assert.Equal(2005, run.Year);
    }

    [Fact]
    public void DeserializedCourse_FeedsToCatalogEntry_EndToEnd()
    {
        // Real JSON in, through the actual deserializer, through the
        // actual ToCatalogEntry mapping — not two separately-tested halves.
        var page = JsonSerializer.Deserialize<OcwCatalogService.CatalogPageResponse>(SampleCoursePageJson, OcwCatalogService.JsonOptions)!;

        var entry = OcwCatalogService.ToCatalogEntry(page.Results[0]);

        Assert.NotNull(entry);
        Assert.Equal("21a-245j-power-interpersonal-organizational-and-global-dimensions-fall-2005", entry!.Slug);
        Assert.Equal("Power: Interpersonal, Organizational and Global Dimensions", entry.Title);
        Assert.Equal("Anthropology · Fall 2005", entry.Subtitle);
    }

    [Fact]
    public void MissingOptionalFields_DeserializeWithoutThrowing()
    {
        const string minimalJson = "{\"count\":0,\"next\":null,\"results\":[{\"title\":\"No Extras\"}]}";

        var page = JsonSerializer.Deserialize<OcwCatalogService.CatalogPageResponse>(minimalJson, OcwCatalogService.JsonOptions)!;
        var course = page.Results[0];

        Assert.Null(page.Next);
        Assert.Equal("No Extras", course.Title);
        Assert.Null(course.Url);
        Assert.Null(course.Departments);
        Assert.Null(course.Runs);
    }
}
