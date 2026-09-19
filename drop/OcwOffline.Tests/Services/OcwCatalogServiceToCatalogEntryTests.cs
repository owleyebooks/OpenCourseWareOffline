using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class OcwCatalogServiceToCatalogEntryTests
{
    [Fact]
    public void ToCatalogEntry_NoUsableUrl_ReturnsNull()
    {
        var dto = new OcwCatalogService.CatalogCourseDto { Title = "Foo", Url = null };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Null(result);
    }

    [Fact]
    public void ToCatalogEntry_DepartmentAndTermPresent_JoinsBothInSubtitle()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Genomics",
            Url = "https://ocw.mit.edu/courses/hst-508-genomics-fall-2002/",
            Departments = new() { new() { Name = "Biology" } },
            Runs = new() { new() { Semester = "Fall", Year = 2002 } }
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.NotNull(result);
        Assert.Equal("hst-508-genomics-fall-2002", result!.Slug);
        Assert.Equal("Genomics", result.Title);
        Assert.Equal("Biology · Fall 2002", result.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_DepartmentOnly_SubtitleOmitsTerm()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = new() { new() { Name = "Mathematics" } },
            Runs = null
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal("Mathematics", result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_TermOnly_SubtitleOmitsDepartment()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = null,
            Runs = new() { new() { Semester = "Spring", Year = 2020 } }
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal("Spring 2020", result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_NoDepartmentOrRun_SubtitleIsEmpty()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = null,
            Runs = null
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal(string.Empty, result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_RunMissingSemester_TreatsTermAsAbsent()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = new() { new() { Name = "Physics" } },
            Runs = new() { new() { Semester = null, Year = 2020 } }
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal("Physics", result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_RunMissingYear_TreatsTermAsAbsent()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = new() { new() { Name = "Physics" } },
            Runs = new() { new() { Semester = "Fall", Year = null } }
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal("Physics", result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_EmptyDepartmentsAndRunsLists_SubtitleIsEmpty()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = new(),
            Runs = new()
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal(string.Empty, result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_UsesFirstDepartmentAndFirstRunOnly()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Foo",
            Url = "https://ocw.mit.edu/courses/foo-bar/",
            Departments = new() { new() { Name = "Biology" }, new() { Name = "Chemistry" } },
            Runs = new()
            {
                new() { Semester = "Fall", Year = 2002 },
                new() { Semester = "Spring", Year = 2003 }
            }
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal("Biology · Fall 2002", result!.Subtitle);
    }

    [Fact]
    public void ToCatalogEntry_TitlePassedThroughUnchanged()
    {
        var dto = new OcwCatalogService.CatalogCourseDto
        {
            Title = "Genomics and Computational Biology",
            Url = "https://ocw.mit.edu/courses/foo-bar/"
        };

        var result = OcwCatalogService.ToCatalogEntry(dto);

        Assert.Equal("Genomics and Computational Biology", result!.Title);
    }
}
