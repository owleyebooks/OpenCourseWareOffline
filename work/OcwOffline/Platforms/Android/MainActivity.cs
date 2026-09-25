using Android.App;
using Android.Content.PM;
using Android.OS;

namespace OcwOffline;

// ResizeableActivity + LaunchMode.SingleTask are required by
// CommunityToolkit.Maui.MediaElement's Android platform setup, not
// optional hardening. ConfigurationChanges is unrelated to
// MediaElement and left as-is.
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask,
    ResizeableActivity = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
