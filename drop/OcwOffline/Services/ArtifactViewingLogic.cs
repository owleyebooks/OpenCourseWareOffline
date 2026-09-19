using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// Pure decision logic pulled out of ArtifactViewerPage.xaml.cs — same
/// motivation and same file-link limitation as VideoPlaybackLogic. See
/// HANDOFF v38's Rule-4 finding and AUDIT_TRAIL v38.
/// </summary>
public static class ArtifactViewingLogic
{
    /// <summary>
    /// Mirrors LoadArtifact's two early-return branches. Not called on
    /// the success path — that status text depends on CanEmbed instead.
    /// </summary>
    public static string DetermineLoadStatus(Artifact artifact, bool fileExists)
    {
        if (string.IsNullOrEmpty(artifact.LocalFilePath))
        {
            return "No downloaded file for this resource.";
        }

        return fileExists ? string.Empty : "Downloaded file is missing on disk.";
    }

    /// <summary>
    /// Mirrors LoadArtifact's #if IOS split — iOS embeds PDF+HTML,
    /// everywhere else embeds HTML only. Why: AUDIT_TRAIL v18.
    /// </summary>
    public static bool CanEmbed(ArtifactFileType fileType, bool isIOS)
    {
        return isIOS
            ? fileType is ArtifactFileType.Html or ArtifactFileType.Pdf
            : fileType == ArtifactFileType.Html;
    }
}
