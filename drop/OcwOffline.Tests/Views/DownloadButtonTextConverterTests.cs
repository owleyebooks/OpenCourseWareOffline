using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class DownloadButtonTextConverterTests
{
    private readonly DownloadButtonTextConverter _converter = new();

    [Theory]
    [InlineData(DownloadStatus.Completed)]
    [InlineData(DownloadStatus.Failed)]
    public void Convert_CompletedOrFailed_ReturnsRedownloadLabel(DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Re-download", result);
    }

    [Theory]
    [InlineData(DownloadStatus.NotStarted)]
    [InlineData(DownloadStatus.InProgress)]
    [InlineData(DownloadStatus.Paused)]
    [InlineData(DownloadStatus.Extracting)]
    public void Convert_OtherStatuses_ReturnsStatusToString(DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(status.ToString(), result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsEmptyString()
    {
        var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack("Re-download", typeof(DownloadStatus), null, CultureInfo.InvariantCulture));
    }
}
