using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class StatusIsActiveConverterTests
{
    private readonly StatusIsActiveConverter _converter = new();

    [Theory]
    [InlineData("in progress is active", DownloadStatus.InProgress)]
    [InlineData("extracting is active", DownloadStatus.Extracting)]
    public void Convert_ActiveStatuses_ReturnsTrue(string description, DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Theory]
    [InlineData("not started is inactive", DownloadStatus.NotStarted)]
    [InlineData("paused is inactive", DownloadStatus.Paused)]
    [InlineData("completed is inactive", DownloadStatus.Completed)]
    [InlineData("failed is inactive", DownloadStatus.Failed)]
    public void Convert_InactiveStatuses_ReturnsFalse(string description, DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_NonStatusValue_ReturnsFalse()
    {
        var result = _converter.Convert("not a status", typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(true, typeof(DownloadStatus), null, CultureInfo.InvariantCulture));
    }
}
