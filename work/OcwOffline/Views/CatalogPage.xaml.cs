using OcwOffline.Models;
using OcwOffline.ViewModels;

namespace OcwOffline.Views;

public partial class CatalogPage : ContentPage
{
    // Factory, not injected instance: each catalog selection needs its
    // own CourseViewModel/CourseSlug, not one shared instance overwritten
    // by whichever course was tapped last.
    private readonly Func<CoursePage> _coursePageFactory;

    public CatalogPage(CatalogViewModel vm, Func<CoursePage> coursePageFactory)
    {
        InitializeComponent();
        BindingContext = vm;
        _coursePageFactory = coursePageFactory;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Only auto-load on first visit. Repeat visits (navigating back
        // from a course) shouldn't silently re-page-in more results under
        // the user, since SearchText/DisplayedCourses state should still
        // reflect what they were browsing.
        if (BindingContext is CatalogViewModel { DisplayedCourses.Count: 0 } vm)
        {
            await vm.LoadMoreCommand.ExecuteAsync(null);
        }
    }

    private async void OnCourseSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CatalogEntry entry) return;

        // Clear selection immediately so the same row can be tapped again
        // later (e.g. after coming back from a course that failed to fetch).
        if (sender is CollectionView collectionView)
            collectionView.SelectedItem = null;

        var coursePage = _coursePageFactory();
        if (coursePage.BindingContext is CourseViewModel courseVm)
        {
            courseVm.CourseSlug = entry.Slug;
            await Navigation.PushAsync(coursePage);
            await courseVm.FetchCommand.ExecuteAsync(null);
        }
    }
}
