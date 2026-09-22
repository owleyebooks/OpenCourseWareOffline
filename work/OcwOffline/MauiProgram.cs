using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using OcwOffline.Services;
using OcwOffline.ViewModels;
using OcwOffline.Views;

namespace OcwOffline;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // TEMPORARY diagnostic logging (screenshot-run branch only): pinpoints
        // where Android startup hangs. Removed before merging to main.
        System.Console.WriteLine("OCWSTARTUP: CreateMauiApp entry");
        try
        {
        var builder = MauiApp.CreateBuilder();
        System.Console.WriteLine("OCWSTARTUP: builder created");
        builder
            .UseMauiApp<App>()
            // isAndroidForegroundServiceEnabled: false. The foreground
            // service stays off because playback is local files only, one
            // lecture at a time, with no background or lock-screen playback
            // requirement to justify the extra Android foreground-service
            // surface.
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });
        System.Console.WriteLine("OCWSTARTUP: maui app + mediaelement + fonts done");

        // Core services registered as singletons: they own
        // long-lived state (db connection, active downloads).
        builder.Services.AddSingleton<CourseDatabase>();
        // ViewModels depend on the interface (mockable in tests); VideoPlayerPage
        // still takes the concrete type below, resolving to the same singleton.
        builder.Services.AddSingleton<ICourseDatabase>(sp => sp.GetRequiredService<CourseDatabase>());
        builder.Services.AddSingleton<OcwScraperService>();
        // ViewModel consumer depends on the interface (mockable in tests);
        // no Page/View reaches OcwScraperService directly, so unlike
        // ICourseDatabase there is no concrete-type consumer left; same
        // shape as IDownloadManager.
        builder.Services.AddSingleton<IOcwScraperService>(sp => sp.GetRequiredService<OcwScraperService>());
        builder.Services.AddSingleton<DownloadManager>();
        // Both ViewModel consumers depend on the interface (mockable in
        // tests); no Page/View reaches DownloadManager directly, so unlike
        // ICourseDatabase there's no concrete-type consumer left. Same
        // singleton either way. See AUDIT_TRAIL v34.
        builder.Services.AddSingleton<IDownloadManager>(sp => sp.GetRequiredService<DownloadManager>());
        builder.Services.AddSingleton<OcwCatalogService>();
        // ViewModel consumer depends on the interface (mockable in tests);
        // no Page/View reaches OcwCatalogService directly, same shape as
        // IDownloadManager/IOcwScraperService. See AUDIT_TRAIL v36.
        builder.Services.AddSingleton<IOcwCatalogService>(sp => sp.GetRequiredService<OcwCatalogService>());

        builder.Services.AddSingleton<IMainThreadDispatcher, MauiMainThreadDispatcher>();
        builder.Services.AddSingleton<IAppPaths, AppPathsProvider>();
        builder.Services.AddTransient<CourseViewModel>();
        builder.Services.AddTransient<CoursePage>();

        // ViewModels are Singleton so their state (SearchText, DisplayedCourses,
        // Courses) survives across pushes; Pages are Transient because a Page
        // cannot be pushed onto Navigation twice while still parented elsewhere
        // in the stack. Resolved via factory below, same shape as VideoPlayerPage.
        builder.Services.AddSingleton<DownloadsDashboardViewModel>();
        builder.Services.AddTransient<DownloadsDashboardPage>();
        builder.Services.AddSingleton<CatalogViewModel>();
        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<VideoPlayerPage>();
        builder.Services.AddTransient<ArtifactViewerPage>();
        // About page: static content, but still transient through the same
        // factory shape so the toolbar can push a fresh instance each tap.
        builder.Services.AddTransient<AboutPage>();

        // CatalogPage needs to construct a *new* CoursePage per course
        // tapped (see CatalogPage.xaml.cs's own comment on why), which
        // means it needs on-demand resolution rather than a single
        // injected instance. A factory delegate does that without
        // handing CatalogPage the whole IServiceProvider.
        builder.Services.AddTransient<Func<CoursePage>>(sp => () => sp.GetRequiredService<CoursePage>());

        // Same shape: CoursePage needs a fresh Page instance per navigation
        // (never the same pushed instance twice, see above) while the
        // ViewModel underneath stays the one shared Singleton.
        builder.Services.AddTransient<Func<DownloadsDashboardPage>>(sp => () => sp.GetRequiredService<DownloadsDashboardPage>());
        builder.Services.AddTransient<Func<CatalogPage>>(sp => () => sp.GetRequiredService<CatalogPage>());

        // Same shape, same reason: CoursePage needs a fresh VideoPlayerPage
        // per lecture tapped, not one shared instance fighting over which
        // lecture is currently loaded. See AUDIT_TRAIL v17.
        builder.Services.AddTransient<Func<VideoPlayerPage>>(sp => () => sp.GetRequiredService<VideoPlayerPage>());

        // Same shape again: CoursePage needs a fresh ArtifactViewerPage
        // per artifact tapped.
        builder.Services.AddTransient<Func<ArtifactViewerPage>>(sp => () => sp.GetRequiredService<ArtifactViewerPage>());

        // Same shape again: the About toolbar item pushes a fresh page per tap.
        builder.Services.AddTransient<Func<AboutPage>>(sp => () => sp.GetRequiredService<AboutPage>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        System.Console.WriteLine("OCWSTARTUP: services registered, calling Build()");
        var mauiApp = builder.Build();
        System.Console.WriteLine("OCWSTARTUP: Build() returned");
        return mauiApp;
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine("OCWSTARTUP: EXCEPTION in CreateMauiApp: " + ex);
            throw;
        }
    }
}
