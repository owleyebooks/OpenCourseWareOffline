using CommunityToolkit.Maui.MediaElement;
using Microsoft.Extensions.Logging;
using OcwOffline.Services;
using OcwOffline.ViewModels;
using OcwOffline.Views;

namespace OcwOffline;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // enableForegroundService: false — this app plays local, already-
            // downloaded files for one lecture at a time; no background/
            // lock-screen playback requirement to justify the extra Android
            // foreground-service surface. See AUDIT_TRAIL v17.
            .UseMauiCommunityToolkitMediaElement(enableForegroundService: false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Core services — registered as singletons since they own
        // long-lived state (db connection, active downloads).
        builder.Services.AddSingleton<CourseDatabase>();
        // ViewModels depend on the interface (mockable in tests); VideoPlayerPage
        // still takes the concrete type below — same singleton either way.
        // See AUDIT_TRAIL v33.
        builder.Services.AddSingleton<ICourseDatabase>(sp => sp.GetRequiredService<CourseDatabase>());
        builder.Services.AddSingleton<OcwScraperService>();
        // ViewModel consumer depends on the interface (mockable in tests);
        // no Page/View reaches OcwScraperService directly, so unlike
        // ICourseDatabase there's no concrete-type consumer left — same
        // shape as IDownloadManager. See AUDIT_TRAIL v36.
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

        builder.Services.AddTransient<CourseViewModel>();
        builder.Services.AddTransient<CoursePage>();

        // ViewModels Singleton (state — SearchText, DisplayedCourses, Courses —
        // must survive across pushes), Pages Transient (a Page can't be pushed
        // onto Navigation twice while still parented elsewhere in the stack —
        // see AUDIT_TRAIL v27 for the crash this caused when both were
        // Singleton). Resolved via factory below, same shape as VideoPlayerPage.
        builder.Services.AddSingleton<DownloadsDashboardViewModel>();
        builder.Services.AddTransient<DownloadsDashboardPage>();
        builder.Services.AddSingleton<CatalogViewModel>();
        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<VideoPlayerPage>();
        builder.Services.AddTransient<ArtifactViewerPage>();

        // CatalogPage needs to construct a *new* CoursePage per course
        // tapped (see CatalogPage.xaml.cs's own comment on why), which
        // means it needs on-demand resolution rather than a single
        // injected instance — a factory delegate does that without
        // handing CatalogPage the whole IServiceProvider.
        builder.Services.AddTransient<Func<CoursePage>>(sp => () => sp.GetRequiredService<CoursePage>());

        // Same shape: CoursePage needs a fresh Page instance per navigation
        // (never the same pushed instance twice — see above), while the
        // ViewModel underneath stays the one shared Singleton. See AUDIT_TRAIL v27.
        builder.Services.AddTransient<Func<DownloadsDashboardPage>>(sp => () => sp.GetRequiredService<DownloadsDashboardPage>());
        builder.Services.AddTransient<Func<CatalogPage>>(sp => () => sp.GetRequiredService<CatalogPage>());

        // Same shape, same reason: CoursePage needs a fresh VideoPlayerPage
        // per lecture tapped, not one shared instance fighting over which
        // lecture is currently loaded. See AUDIT_TRAIL v17.
        builder.Services.AddTransient<Func<VideoPlayerPage>>(sp => () => sp.GetRequiredService<VideoPlayerPage>());

        // Same shape again: CoursePage needs a fresh ArtifactViewerPage
        // per artifact tapped. Added v18 for the PDF/HTML viewer.
        builder.Services.AddTransient<Func<ArtifactViewerPage>>(sp => () => sp.GetRequiredService<ArtifactViewerPage>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
