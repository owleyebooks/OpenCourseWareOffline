using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class DownloadProgressTests
{
    [Fact]
    public void Fraction_ZeroTotalBytes_ReturnsZeroNotDivideByZero()
    {
        var progress = new DownloadProgress("key", 0, 0);

        Assert.Equal(0, progress.Fraction);
    }

    [Fact]
    public void Fraction_ZeroTotalBytesWithReceivedBytes_ReturnsZero()
    {
        var progress = new DownloadProgress("key", 500, 0);

        Assert.Equal(0, progress.Fraction);
    }

    [Fact]
    public void Fraction_PartialDownload_ReturnsRatio()
    {
        var progress = new DownloadProgress("key", 250, 1000);

        Assert.Equal(0.25, progress.Fraction);
    }

    [Fact]
    public void Fraction_CompleteDownload_ReturnsOne()
    {
        var progress = new DownloadProgress("key", 1000, 1000);

        Assert.Equal(1.0, progress.Fraction);
    }

    [Fact]
    public void Fraction_BytesReceivedExceedsTotal_ExceedsOneUnclamped()
    {
        // Documented behavior, not a fix: the property has no clamp,
        // so an over-count exceeds 1.0 rather than capping at it.
        var progress = new DownloadProgress("key", 1200, 1000);

        Assert.Equal(1.2, progress.Fraction);
    }
}
