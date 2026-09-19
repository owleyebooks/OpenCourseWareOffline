using OcwOffline.Models;

namespace OcwOffline.Services;

/// <summary>
/// Pure decision logic pulled out of ArtifactViewerPage.xaml.cs, with the
/// same motivation and file-link limitation as VideoPlaybackLogic.
/// </summary>
public static class ArtifactViewingLogic
{
    /// <summary>
    /// Mirrors LoadArtifact's two early-return branches. Not called on
    /// the success path; that status text depends on CanEmbed instead.
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
    /// Mirrors LoadArtifact's #if IOS split: iOS embeds PDF+HTML,
    /// everywhere else embeds HTML only.
    /// </summary>
    public static bool CanEmbed(ArtifactFileType fileType, bool isIOS)
    {
        return isIOS
            ? fileType is ArtifactFileType.Html or ArtifactFileType.Pdf
            : fileType == ArtifactFileType.Html;
    }
}
