using OcwOffline.Models;
using Xunit;

namespace OcwOffline.Tests.Models;

public class LectureTests
{
    [Fact]
    public void DownloadStatus_DefaultsToNotStarted()
    {
        var lecture = new Lecture();

        Assert.Equal(DownloadStatus.NotStarted, lecture.DownloadStatus);
    }

    [Fact]
    public void Progress_DefaultsToZero()
    {
        var lecture = new Lecture();

        Assert.Equal(0.0, lecture.Progress);
    }

    [Fact]
    public void BytesDownloaded_DefaultsToZero()
    {
        var lecture = new Lecture();

        Assert.Equal(0L, lecture.BytesDownloaded);
    }

    [Fact]
    public void IsCompleted_DefaultsToFalse()
    {
        var lecture = new Lecture();

        Assert.False(lecture.IsCompleted);
    }

    [Fact]
    public void LastWatchedPositionSeconds_DefaultsToZero()
    {
        var lecture = new Lecture();

        Assert.Equal(0, lecture.LastWatchedPositionSeconds);
    }

    [Fact]
    public void SettingLocalVideoPath_RaisesPropertyChanged()
    {
        var lecture = new Lecture();
        var raisedProperties = new List<string?>();
        lecture.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        lecture.LocalVideoPath = "/videos/lecture-01.mp4";

        Assert.Contains(nameof(Lecture.LocalVideoPath), raisedProperties);
    }

    [Fact]
    public void SettingProgress_RaisesPropertyChanged()
    {
        var lecture = new Lecture();
        var raisedProperties = new List<string?>();
        lecture.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        lecture.Progress = 0.5;

        Assert.Contains(nameof(Lecture.Progress), raisedProperties);
    }
}
