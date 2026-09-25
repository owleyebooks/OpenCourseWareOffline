using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class RowStateConverterTests
{
    [Theory]
    [InlineData("zero hides the size", 0L, false)]
    [InlineData("negative hides the size", -5L, false)]
    [InlineData("positive shows the size", 1024L, true)]
    public void LongIsPositiveConverter_MapsToVisibility(string description, long bytes, bool expected)
    {
        new LongIsPositiveConverter()
            .Convert(bytes, typeof(bool), null, CultureInfo.InvariantCulture)
            .Should().Be(expected, $"case: {description}");
    }

    [Theory]
    [InlineData("downloading", DownloadStatus.InProgress, true)]
    [InlineData("paused", DownloadStatus.Paused, true)]
    [InlineData("not started", DownloadStatus.NotStarted, false)]
    [InlineData("completed", DownloadStatus.Completed, false)]
    [InlineData("failed", DownloadStatus.Failed, false)]
    [InlineData("extracting", DownloadStatus.Extracting, false)]
    public void StatusIsCancellableConverter_MapsToVisibility(string description, DownloadStatus status, bool expected)
    {
        new StatusIsCancellableConverter()
            .Convert(status, typeof(bool), null, CultureInfo.InvariantCulture)
            .Should().Be(expected, $"Cancel shows when the row is {description}: {expected}");
    }

    [Theory]
    [InlineData("completed", DownloadStatus.Completed, true)]
    [InlineData("downloading", DownloadStatus.InProgress, false)]
    [InlineData("paused", DownloadStatus.Paused, false)]
    [InlineData("failed", DownloadStatus.Failed, false)]
    [InlineData("not started", DownloadStatus.NotStarted, false)]
    public void StatusIsCompletedConverter_MapsToVisibility(string description, DownloadStatus status, bool expected)
    {
        new StatusIsCompletedConverter()
            .Convert(status, typeof(bool), null, CultureInfo.InvariantCulture)
            .Should().Be(expected, $"Delete shows when the row is {description}: {expected}");
    }
}
