using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class StatusIsInProgressConverterTests
{
    private readonly StatusIsInProgressConverter _converter = new();

    [Fact]
    public void Convert_InProgress_ReturnsTrue()
    {
        var result = _converter.Convert(DownloadStatus.InProgress, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Theory]
    [InlineData("not started is not in progress", DownloadStatus.NotStarted)]
    [InlineData("paused is not in progress", DownloadStatus.Paused)]
    [InlineData("extracting is not in progress", DownloadStatus.Extracting)]
    [InlineData("completed is not in progress", DownloadStatus.Completed)]
    [InlineData("failed is not in progress", DownloadStatus.Failed)]
    public void Convert_AnyOtherStatus_ReturnsFalse(string description, DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_NonStatusValue_ReturnsFalse()
    {
        var result = _converter.Convert(42, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(true, typeof(DownloadStatus), null, CultureInfo.InvariantCulture));
    }
}
