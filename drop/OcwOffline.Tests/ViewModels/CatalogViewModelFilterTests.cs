using OcwOffline.Models;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class CatalogViewModelFilterTests
{
    private static List<CatalogEntry> SampleEntries() => new()
    {
        new CatalogEntry { Slug = "hst-508", Title = "Genomics and Computational Biology" },
        new CatalogEntry { Slug = "6-006", Title = "Introduction to Algorithms" },
        new CatalogEntry { Slug = "18-06", Title = "Linear Algebra" }
    };

    [Fact]
    public void FilterEntries_EmptySearchText_ReturnsAllEntries()
    {
        var result = CatalogViewModel.FilterEntries(SampleEntries(), string.Empty);

        Assert.Equal(3, result.Count());
    }

    [Fact]
    public void FilterEntries_WhitespaceSearchText_ReturnsAllEntries()
    {
        var result = CatalogViewModel.FilterEntries(SampleEntries(), "   ");

        Assert.Equal(3, result.Count());
    }

    [Fact]
    public void FilterEntries_MatchingSubstring_ReturnsOnlyMatches()
    {
        var result = CatalogViewModel.FilterEntries(SampleEntries(), "Algo").ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Slug == "6-006");
        Assert.Contains(result, e => e.Slug == "18-06");
    }

    [Fact]
    public void FilterEntries_MatchIsCaseInsensitive()
    {
        var result = CatalogViewModel.FilterEntries(SampleEntries(), "GENOMICS").ToList();

        Assert.Single(result);
        Assert.Equal("hst-508", result[0].Slug);
    }

    [Fact]
    public void FilterEntries_NoMatch_ReturnsEmpty()
    {
        var result = CatalogViewModel.FilterEntries(SampleEntries(), "Quantum Field Theory");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterEntries_EmptyAllLoadedList_ReturnsEmpty()
    {
        var result = CatalogViewModel.FilterEntries(new List<CatalogEntry>(), "anything");

        Assert.Empty(result);
    }
}
