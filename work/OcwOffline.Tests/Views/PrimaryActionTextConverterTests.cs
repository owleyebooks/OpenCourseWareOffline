using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class PrimaryActionTextConverterTests
{
    private static string ConvertText(object item, DownloadStatus status) =>
        (string)new PrimaryActionTextConverter().Convert(
            new object?[] { item, status }, typeof(string), null, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("not started", DownloadStatus.NotStarted, "Download")]
    [InlineData("downloading", DownloadStatus.InProgress, "Pause")]
    [InlineData("paused", DownloadStatus.Paused, "Resume")]
    [InlineData("failed", DownloadStatus.Failed, "Retry")]
    [InlineData("completed", DownloadStatus.Completed, "Open")]
    [InlineData("extracting", DownloadStatus.Extracting, "Working…")]
    public void Convert_Artifact_StateMatrix_MapsToLabel(string description, DownloadStatus status, string expected)
    {
        var artifact = new Artifact { DownloadStatus = status };

        ConvertText(artifact, status).Should().Be(expected, $"artifact case: {description}");
    }

    [Theory]
    [InlineData("not started", DownloadStatus.NotStarted, "Download")]
    [InlineData("downloading", DownloadStatus.InProgress, "Pause")]
    [InlineData("paused", DownloadStatus.Paused, "Resume")]
    [InlineData("failed", DownloadStatus.Failed, "Retry")]
    [InlineData("completed unwatched", DownloadStatus.Completed, "Watch")]
    [InlineData("extracting", DownloadStatus.Extracting, "Working…")]
    public void Convert_Lecture_StateMatrix_MapsToLabel(string description, DownloadStatus status, string expected)
    {
        var lecture = new Lecture { DownloadStatus = status };

        ConvertText(lecture, status).Should().Be(expected, $"lecture case: {description}");
    }

    [Fact]
    public void Convert_Lecture_PartiallyWatched_CompletedReadsResume()
    {
        var lecture = new Lecture
        {
            DownloadStatus = DownloadStatus.Completed,
            LastWatchedPositionSeconds = 120,
            IsCompleted = false
        };

        ConvertText(lecture, lecture.DownloadStatus).Should().Be("Resume", "a partially watched lecture resumes playback");
    }

    [Theory]
    [InlineData("fully watched", 120, true, "Watch")]
    [InlineData("under the resume threshold", 29, false, "Watch")]
    [InlineData("exactly at the resume threshold", 30, false, "Resume")]
    public void Convert_Lecture_WatchState_RespectsThresholdAndCompletion(
        string description, int positionSeconds, bool isCompleted, string expected)
    {
        var lecture = new Lecture
        {
            DownloadStatus = DownloadStatus.Completed,
            LastWatchedPositionSeconds = positionSeconds,
            IsCompleted = isCompleted
        };

        ConvertText(lecture, lecture.DownloadStatus).Should().Be(expected, $"lecture case: {description}");
    }

    [Fact]
    public void Convert_UnknownValue_ReturnsEmpty()
    {
        new PrimaryActionTextConverter()
            .Convert(new object?[] { "nope", DownloadStatus.NotStarted }, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(string.Empty, "a non-item binding shows nothing rather than crashing");
    }
}
