using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.ViewModels;

public partial class CatalogViewModel : ObservableObject
{
    private readonly IOcwCatalogService _catalog;

    // Everything paged in so far, unfiltered. DisplayedCourses (bound in
    // XAML) is the filtered view over this, kept separate so re-filtering
    // on every keystroke doesn't require re-fetching from the network.
    private readonly List<CatalogEntry> _allLoaded = new();

    private int _nextOffset;
    private bool _hasMore = true;

    public CatalogViewModel(IOcwCatalogService catalog)
    {
        _catalog = catalog;
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "Loading courses…";

    public ObservableCollection<CatalogEntry> DisplayedCourses { get; } = new();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !_hasMore) return;

        IsBusy = true;
        try
        {
            var (entries, hasMore) = await _catalog.GetCoursePageAsync(_nextOffset);
            _allLoaded.AddRange(entries);
            _nextOffset += entries.Count;
            _hasMore = hasMore;

            ApplyFilter();
            StatusText = _hasMore
                ? $"{_allLoaded.Count} courses loaded: scroll or tap Load More for more."
                : $"{_allLoaded.Count} courses loaded (that's all of them).";
        }
        catch (Exception ex)
        {
            // No confirmed offline/empty-catalog UX yet. Same "surface it
            // plainly, don't hide it" approach CourseViewModel.FetchAsync
            // uses for scrape failures.
            StatusText = $"Couldn't load the catalog: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        DisplayedCourses.Clear();
        foreach (var entry in FilterEntries(_allLoaded, SearchText))
            DisplayedCourses.Add(entry);
    }

    // Extracted from ApplyFilter; pure filtering logic with no
    // ObservableCollection/instance state, so it is directly testable
    // without constructing a live OcwCatalogService call chain.
    internal static IEnumerable<CatalogEntry> FilterEntries(IReadOnlyList<CatalogEntry> allLoaded, string searchText) =>
        string.IsNullOrWhiteSpace(searchText)
            ? allLoaded
            : allLoaded.Where(c => c.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));
}
