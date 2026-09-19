using OcwOffline.Models;
using OcwOffline.Services;
using Xunit;

namespace OcwOffline.Tests.Services;

public class ArtifactViewingLogicTests
{
    [Fact]
    public void DetermineLoadStatus_NoLocalFilePath_ReturnsNoFileMessage()
    {
        var artifact = new Artifact { LocalFilePath = null };

        var status = ArtifactViewingLogic.DetermineLoadStatus(artifact, fileExists: false);

        Assert.Equal("No downloaded file for this resource.", status);
    }

    [Fact]
    public void DetermineLoadStatus_LocalFilePathSetButFileMissing_ReturnsMissingMessage()
    {
        var artifact = new Artifact { LocalFilePath = "artifacts/1.pdf" };

        var status = ArtifactViewingLogic.DetermineLoadStatus(artifact, fileExists: false);

        Assert.Equal("Downloaded file is missing on disk.", status);
    }

    [Fact]
    public void DetermineLoadStatus_FileExists_ReturnsEmpty()
    {
        var artifact = new Artifact { LocalFilePath = "artifacts/1.pdf" };

        var status = ArtifactViewingLogic.DetermineLoadStatus(artifact, fileExists: true);

        Assert.Equal(string.Empty, status);
    }

    [Theory]
    [InlineData("html embeds on iOS", ArtifactFileType.Html, true)]
    [InlineData("html embeds off iOS", ArtifactFileType.Html, false)]
    public void CanEmbed_Html_TrueOnEveryPlatform(string description, ArtifactFileType fileType, bool isIOS)
    {
        Assert.True(ArtifactViewingLogic.CanEmbed(fileType, isIOS));
    }

    [Fact]
    public void CanEmbed_PdfOnIOS_ReturnsTrue()
    {
        Assert.True(ArtifactViewingLogic.CanEmbed(ArtifactFileType.Pdf, isIOS: true));
    }

    [Fact]
    public void CanEmbed_PdfOffIOS_ReturnsFalse()
    {
        Assert.False(ArtifactViewingLogic.CanEmbed(ArtifactFileType.Pdf, isIOS: false));
    }

    [Theory]
    [InlineData("zip never embeds", ArtifactFileType.Zip)]
    [InlineData("other never embeds", ArtifactFileType.Other)]
    public void CanEmbed_NonViewableTypes_AlwaysFalse(string description, ArtifactFileType fileType)
    {
        Assert.False(ArtifactViewingLogic.CanEmbed(fileType, isIOS: true));
        Assert.False(ArtifactViewingLogic.CanEmbed(fileType, isIOS: false));
    }
}
