using OcwOffline.Models;
using Xunit;

namespace OcwOffline.Tests.Models;

public class ArtifactTests
{
    [Theory]
    [InlineData("pdf is viewable", ArtifactFileType.Pdf)]
    [InlineData("html is viewable", ArtifactFileType.Html)]
    public void IsViewable_TrueWhenViewableTypeAndFileDownloaded(string description, ArtifactFileType fileType)
    {
        var artifact = new Artifact { FileType = fileType, LocalFilePath = "/docs/lecture-notes.pdf" };

        Assert.True(artifact.IsViewable);
    }

    [Theory]
    [InlineData("zip is not viewable", ArtifactFileType.Zip)]
    [InlineData("other is not viewable", ArtifactFileType.Other)]
    public void IsViewable_FalseForNonViewableTypesEvenWhenDownloaded(string description, ArtifactFileType fileType)
    {
        var artifact = new Artifact { FileType = fileType, LocalFilePath = "/docs/archive.zip" };

        Assert.False(artifact.IsViewable);
    }

    [Fact]
    public void IsViewable_FalseWhenLocalFilePathIsNull()
    {
        var artifact = new Artifact { FileType = ArtifactFileType.Pdf, LocalFilePath = null };

        Assert.False(artifact.IsViewable);
    }

    [Fact]
    public void IsViewable_FalseWhenLocalFilePathIsEmpty()
    {
        var artifact = new Artifact { FileType = ArtifactFileType.Html, LocalFilePath = string.Empty };

        Assert.False(artifact.IsViewable);
    }

    [Fact]
    public void DownloadStatus_DefaultsToNotStarted()
    {
        var artifact = new Artifact();

        Assert.Equal(DownloadStatus.NotStarted, artifact.DownloadStatus);
    }

    [Fact]
    public void SettingLocalFilePath_RaisesPropertyChangedForIsViewable()
    {
        var artifact = new Artifact { FileType = ArtifactFileType.Pdf };
        var raisedProperties = new List<string?>();
        artifact.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        artifact.LocalFilePath = "/docs/lecture-notes.pdf";

        Assert.Contains(nameof(Artifact.LocalFilePath), raisedProperties);
        Assert.Contains(nameof(Artifact.IsViewable), raisedProperties);
    }

    [Fact]
    public void SettingLocalFilePathToNull_StillRaisesPropertyChangedForIsViewable()
    {
        var artifact = new Artifact { FileType = ArtifactFileType.Pdf, LocalFilePath = "/docs/lecture-notes.pdf" };
        var raisedProperties = new List<string?>();
        artifact.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        artifact.LocalFilePath = null;

        Assert.Contains(nameof(Artifact.IsViewable), raisedProperties);
        Assert.False(artifact.IsViewable);
    }
}
