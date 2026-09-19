using OcwOffline.Models;
using Xunit;

namespace OcwOffline.Tests.Models;

public class CourseTests
{
    [Fact]
    public void Defaults_AreEmptyStringsAndNotDownloaded()
    {
        var course = new Course();

        Assert.Equal(string.Empty, course.Id);
        Assert.Equal(string.Empty, course.Title);
        Assert.Equal(string.Empty, course.Department);
        Assert.Equal(string.Empty, course.Url);
        Assert.False(course.IsDownloaded);
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        var course = new Course();

        Assert.Null(course.LastScrapedUtc);
        Assert.Null(course.Term);
    }
}
