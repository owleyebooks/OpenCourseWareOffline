using OcwOffline.Models;
using Xunit;

namespace OcwOffline.Tests.Models;

public class CatalogEntryTests
{
    [Fact]
    public void Defaults_AreEmptyStrings()
    {
        var entry = new CatalogEntry();

        Assert.Equal(string.Empty, entry.Slug);
        Assert.Equal(string.Empty, entry.Title);
        Assert.Equal(string.Empty, entry.Subtitle);
    }
}
