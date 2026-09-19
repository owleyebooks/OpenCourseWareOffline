using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class StatusIsActiveConverterTests
{
    private readonly StatusIsActiveConverter _converter = new();

    [Theory]
    [InlineData(DownloadStatus.InProgress)]
    [InlineData(DownloadStatus.Extracting)]
    public void Convert_ActiveStatuses_ReturnsTrue(DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Theory]
    [InlineData(DownloadStatus.NotStarted)]
    [InlineData(DownloadStatus.Paused)]
    [InlineData(DownloadStatus.Completed)]
    [InlineData(DownloadStatus.Failed)]
    public void Convert_InactiveStatuses_ReturnsFalse(DownloadStatus status)
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
