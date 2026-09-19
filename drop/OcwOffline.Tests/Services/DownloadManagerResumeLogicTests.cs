using System.Net;
using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class DownloadManagerResumeLogicTests
{
    [Fact]
    public void ShouldRetryWithoutRange_ExistingBytesAndRangeNotSatisfiable_ReturnsTrue()
    {
        var result = DownloadManager.ShouldRetryWithoutRange(1000, HttpStatusCode.RequestedRangeNotSatisfiable);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryWithoutRange_NoExistingBytes_ReturnsFalseEvenIf416()
    {
        var result = DownloadManager.ShouldRetryWithoutRange(0, HttpStatusCode.RequestedRangeNotSatisfiable);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryWithoutRange_ExistingBytesButNot416_ReturnsFalse()
    {
        var result = DownloadManager.ShouldRetryWithoutRange(1000, HttpStatusCode.PartialContent);

        Assert.False(result);
    }

    [Fact]
    public void DetermineResumeStrategy_ExistingBytesWith206_ResumesAndKeepsBytes()
    {
        var (existingBytes, resuming) = DownloadManager.DetermineResumeStrategy(1000, HttpStatusCode.PartialContent);

        Assert.True(resuming);
        Assert.Equal(1000, existingBytes);
    }

    [Fact]
    public void DetermineResumeStrategy_ExistingBytesWith200_RestartsFromZero()
    {
        var (existingBytes, resuming) = DownloadManager.DetermineResumeStrategy(1000, HttpStatusCode.OK);

        Assert.False(resuming);
        Assert.Equal(0, existingBytes);
    }

    [Fact]
    public void DetermineResumeStrategy_NoExistingBytes_NeverResumesRegardlessOfStatus()
    {
        var (existingBytes, resuming) = DownloadManager.DetermineResumeStrategy(0, HttpStatusCode.PartialContent);

        Assert.False(resuming);
        Assert.Equal(0, existingBytes);
    }
}
