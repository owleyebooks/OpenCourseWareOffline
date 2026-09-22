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
    // TEMPORARY diagnostic logging (screenshot-run branch only).
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        System.Console.WriteLine("OCWSTARTUP: MainActivity.OnCreate entry");
        try
        {
            base.OnCreate(savedInstanceState);
            System.Console.WriteLine("OCWSTARTUP: MainActivity.OnCreate base returned");
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine("OCWSTARTUP: EXCEPTION in MainActivity.OnCreate: " + ex);
            throw;
        }
    }
}
