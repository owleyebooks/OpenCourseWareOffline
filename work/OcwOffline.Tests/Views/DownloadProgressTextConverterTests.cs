using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class DownloadProgressTextConverterTests
{
    private static string ConvertText(DownloadStatus status, double progress, long bytesDownloaded, long fileSizeBytes) =>
        (string)new DownloadProgressTextConverter().Convert(
            new object?[] { status, progress, bytesDownloaded, fileSizeBytes },
            typeof(string), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_InProgress_FormatsPercentAndSizes()
    {
        ConvertText(DownloadStatus.InProgress, 0.38, 340L * 1024 * 1024, 900L * 1024 * 1024)
            .Should().Be("Downloading… 38%, 340 MB of 900 MB");
    }

    [Fact]
    public void Convert_Paused_UsesPausedWording()
    {
        ConvertText(DownloadStatus.Paused, 0.5, 450L * 1024 * 1024, 900L * 1024 * 1024)
            .Should().Be("Paused… 50%, 450 MB of 900 MB");
    }

    [Theory]
    [InlineData("not started", DownloadStatus.NotStarted)]
    [InlineData("completed", DownloadStatus.Completed)]
    [InlineData("failed", DownloadStatus.Failed)]
    [InlineData("extracting", DownloadStatus.Extracting)]
    public void Convert_InactiveState_ReturnsEmpty(string description, DownloadStatus status)
    {
        ConvertText(status, 0.5, 100, 200)
            .Should().BeEmpty($"no detail line shows when the row is {description}");
    }

    [Fact]
    public void Convert_UnknownTotalSize_ReturnsEmpty()
    {
        ConvertText(DownloadStatus.InProgress, 0.5, 100, 0)
            .Should().BeEmpty("no detail line shows when the total size is unknown");
    }

    [Fact]
    public void Convert_UnexpectedValues_ReturnsEmpty()
    {
        new DownloadProgressTextConverter()
            .Convert(new object?[] { "nope" }, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(string.Empty, "a malformed binding shows nothing rather than crashing");
    }
}
