using OcwOffline.Models;
using OcwOffline.Services;
using Xunit;

namespace OcwOffline.Tests.Services;

public class VideoPlaybackLogicTests
{
    [Fact]
    public void DetermineLoadStatus_NoLocalVideoPath_ReturnsNoFileMessage()
    {
        var lecture = new Lecture { LocalVideoPath = null };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: false);

        Assert.Equal("No downloaded file for this lecture.", status);
    }

    [Fact]
    public void DetermineLoadStatus_LocalVideoPathSetButFileMissing_ReturnsMissingMessage()
    {
        var lecture = new Lecture { LocalVideoPath = "lectures/1.mp4" };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: false);

        Assert.Equal("Downloaded file is missing on disk.", status);
    }

    [Fact]
    public void DetermineLoadStatus_FileExistsWithSavedPosition_ReturnsResumingMessage()
    {
        var lecture = new Lecture
        {
            LocalVideoPath = "lectures/1.mp4",
            LastWatchedPositionSeconds = 125,
            IsCompleted = false
        };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: true);

        Assert.Equal("Resuming from 02:05.", status);
    }

    [Fact]
    public void DetermineLoadStatus_FileExistsButAlreadyCompleted_ReturnsEmpty()
    {
        var lecture = new Lecture
        {
            LocalVideoPath = "lectures/1.mp4",
            LastWatchedPositionSeconds = 500,
            IsCompleted = true
        };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: true);

        Assert.Equal(string.Empty, status);
    }

    [Fact]
    public void DetermineLoadStatus_FileExistsWithNoSavedPosition_ReturnsEmpty()
    {
        var lecture = new Lecture
        {
            LocalVideoPath = "lectures/1.mp4",
            LastWatchedPositionSeconds = 0,
            IsCompleted = false
        };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: true);

        Assert.Equal(string.Empty, status);
    }

    [Theory]
    [InlineData("one second under the threshold", 29)]
    [InlineData("a few seconds in", 5)]
    public void DetermineLoadStatus_SavedPositionUnderThreshold_ReturnsEmpty(string description, int positionSeconds)
    {
        var lecture = new Lecture
        {
            LocalVideoPath = "lectures/1.mp4",
            LastWatchedPositionSeconds = positionSeconds,
            IsCompleted = false
        };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: true);

        status.Should().BeEmpty(because: $"{description}: the resume banner only appears from 30 seconds on");
    }

    [Fact]
    public void DetermineLoadStatus_SavedPositionAtThreshold_ReturnsResumingMessage()
    {
        var lecture = new Lecture
        {
            LocalVideoPath = "lectures/1.mp4",
            LastWatchedPositionSeconds = 30,
            IsCompleted = false
        };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: true);

        status.Should().Be("Resuming from 00:30.",
            because: "30 seconds is the first position that earns the banner");
    }

    [Fact]
    public void DetermineLoadStatus_CompletedWithPositionAtThreshold_ReturnsEmpty()
    {
        var lecture = new Lecture
        {
            LocalVideoPath = "lectures/1.mp4",
            LastWatchedPositionSeconds = 30,
            IsCompleted = true
        };

        var status = VideoPlaybackLogic.DetermineLoadStatus(lecture, fileExists: true);

        status.Should().BeEmpty(because: "a finished lecture has nothing to resume");
    }

    [Fact]
    public void DetermineCompletionState_NoDurationYet_ReturnsNulls()
    {
        var (duration, isCompleted) = VideoPlaybackLogic.DetermineCompletionState(positionSeconds: 30, durationSeconds: 0);

        Assert.Null(duration);
        Assert.Null(isCompleted);
    }

    [Fact]
    public void DetermineCompletionState_PositionWithinFiveSecondsOfEnd_MarksCompleted()
    {
        var (duration, isCompleted) = VideoPlaybackLogic.DetermineCompletionState(positionSeconds: 596, durationSeconds: 600);

        Assert.Equal(600, duration);
        Assert.True(isCompleted);
    }

    [Fact]
    public void DetermineCompletionState_PositionWellBeforeEnd_NotCompleted()
    {
        var (duration, isCompleted) = VideoPlaybackLogic.DetermineCompletionState(positionSeconds: 100, durationSeconds: 600);

        Assert.Equal(600, duration);
        Assert.False(isCompleted);
    }
}
