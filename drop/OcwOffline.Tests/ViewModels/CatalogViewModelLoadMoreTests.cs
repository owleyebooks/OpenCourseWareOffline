using OcwOffline.Models;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

/// <summary>
/// LoadMoreAsync was blocked on OcwCatalogService's own HttpClient seam
/// (open since v32). Unblocked by IOcwCatalogService, same shape as
/// v36's IOcwScraperService unblock of FetchAsync. See AUDIT_TRAIL v36.
/// </summary>
public class CatalogViewModelLoadMoreTests
{
    private static CatalogViewModel CreateViewModel(out FakeOcwCatalogService catalog)
    {
        catalog = new FakeOcwCatalogService();
        return new CatalogViewModel(catalog);
    }

    [Fact]
    public async Task LoadMoreAsync_Success_AppendsEntriesAndAdvancesOffset()
    {
        var vm = CreateViewModel(out var catalog);
        catalog.QueueResult(0, new List<CatalogEntry>
        {
            new() { Slug = "6-006", Title = "Introduction to Algorithms" },
            new() { Slug = "18-06", Title = "Linear Algebra" }
        }, hasMore: true);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(new[] { 0 }, catalog.RequestedOffsets);
        Assert.Equal(2, vm.DisplayedCourses.Count);
        Assert.Contains(vm.DisplayedCourses, c => c.Slug == "6-006");
        Assert.Equal("2 courses loaded — scroll or tap Load More for more.", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task LoadMoreAsync_SecondPage_UsesAdvancedOffsetAndAppends()
    {
        var vm = CreateViewModel(out var catalog);
        catalog.QueueResult(0, new List<CatalogEntry> { new() { Slug = "a", Title = "A" }, new() { Slug = "b", Title = "B" } }, hasMore: true);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        catalog.QueueResult(2, new List<CatalogEntry> { new() { Slug = "c", Title = "C" } }, hasMore: false);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(new[] { 0, 2 }, catalog.RequestedOffsets);
        Assert.Equal(3, vm.DisplayedCourses.Count);
        Assert.Equal("3 courses loaded (that's all of them).", vm.StatusText);
    }

    [Fact]
    public async Task LoadMoreAsync_NoMoreData_NoOp()
    {
        var vm = CreateViewModel(out var catalog);
        catalog.QueueResult(0, new List<CatalogEntry> { new() { Slug = "a", Title = "A" } }, hasMore: false);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(new[] { 0 }, catalog.RequestedOffsets); // second call never reached the catalog
    }

    [Fact]
    public async Task LoadMoreAsync_AlreadyBusy_NoOp()
    {
        var vm = CreateViewModel(out var catalog);
        vm.IsBusy = true;

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Empty(catalog.RequestedOffsets);
    }

    [Fact]
    public async Task LoadMoreAsync_CatalogThrows_SetsStatusTextAndClearsBusy()
    {
        var vm = CreateViewModel(out var catalog);
        catalog.QueueFailure(0, new HttpRequestException("timeout"));

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't load the catalog: timeout", vm.StatusText);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.DisplayedCourses);
    }

    [Fact]
    public async Task LoadMoreAsync_RespectsSearchTextAlreadySet()
    {
        var vm = CreateViewModel(out var catalog);
        vm.SearchText = "Algo";
        catalog.QueueResult(0, new List<CatalogEntry>
        {
            new() { Slug = "6-006", Title = "Introduction to Algorithms" },
            new() { Slug = "18-06", Title = "Linear Algebra" }
        }, hasMore: false);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Single(vm.DisplayedCourses);
        Assert.Equal("6-006", vm.DisplayedCourses[0].Slug);
    }
}
