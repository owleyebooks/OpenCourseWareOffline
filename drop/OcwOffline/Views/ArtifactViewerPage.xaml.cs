using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Views;

// HTML embeds via WebView (absolute file:// URI — see dotnet/maui
// #10674). PDF: iOS embeds too (WKWebView), Android hands off to the
// OS's PDF app via Launcher instead. Full reasoning: AUDIT_TRAIL v18.
// Unbuilt — PROCESS Rule 1.
public partial class ArtifactViewerPage : ContentPage
{
    private Artifact? _artifact;

    public ArtifactViewerPage()
    {
        InitializeComponent();
    }

    // Called by the caller right after resolving this page from its
    // factory, before Navigation.PushAsync — same hand-off shape as
    // VideoPlayerPage.LoadLecture.
    public void LoadArtifact(Artifact artifact)
    {
        _artifact = artifact;
        Title = artifact.Title;

        if (string.IsNullOrEmpty(artifact.LocalFilePath))
        {
            StatusLabel.Text = ArtifactViewingLogic.DetermineLoadStatus(artifact, fileExists: false);
            return;
        }

        var fullPath = Path.Combine(AppPaths.Root, artifact.LocalFilePath);
        var fileExists = File.Exists(fullPath);
        if (!fileExists)
        {
            // Same "flag it, don't fabricate a fix" posture as
            // VideoPlayerPage.LoadLecture for a missing local file.
            StatusLabel.Text = ArtifactViewingLogic.DetermineLoadStatus(artifact, fileExists);
            return;
        }

#if IOS
        var isIOS = true;
#else
        var isIOS = false;
#endif
        var canEmbed = ArtifactViewingLogic.CanEmbed(artifact.FileType, isIOS);

        if (canEmbed)
        {
            Viewer.IsVisible = true;
            Viewer.Source = new UrlWebViewSource { Url = new Uri(fullPath).AbsoluteUri };
        }
        else
        {
            PdfPanel.IsVisible = true;
            StatusLabel.Text = "Tap Open PDF to view it in your device's PDF app.";
        }
    }

    private async void OnOpenPdfClicked(object sender, EventArgs e)
    {
        if (_artifact is null || string.IsNullOrEmpty(_artifact.LocalFilePath))
        {
            return;
        }

        try
        {
            var sourcePath = Path.Combine(AppPaths.Root, _artifact.LocalFilePath);

            // Copied into Essentials' default FileProvider sharing root
            // first — see AUDIT_TRAIL v18 for why, over a custom provider.
            var sharingDir = Path.Combine(FileSystem.CacheDirectory, "sharing-root");
            Directory.CreateDirectory(sharingDir);
            var sharePath = Path.Combine(sharingDir, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, sharePath, overwrite: true);

            await Launcher.Default.OpenAsync(new OpenFileRequest(_artifact.Title, new ReadOnlyFile(sharePath)));
        }
        catch (Exception ex)
        {
            // Mirrors VideoPlayerPage's OnMediaFailed posture — surface
            // the failure on the page rather than throwing past it.
            StatusLabel.Text = $"Couldn't open PDF: {ex.Message}";
        }
    }
}
