using System.Globalization;
using OcwOffline.Models;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class DownloadButtonTextConverterTests
{
    private readonly DownloadButtonTextConverter _converter = new();

    [Theory]
    [InlineData("completed offers re-download", DownloadStatus.Completed)]
    [InlineData("failed offers re-download", DownloadStatus.Failed)]
    public void Convert_CompletedOrFailed_ReturnsRedownloadLabel(string description, DownloadStatus status)
    {
        var result = _converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Re-download", result);
    }

    [Theory]
    [InlineData("not started shows its own name", DownloadStatus.NotStarted)]
    [InlineData("in progress shows its own name", DownloadStatus.InProgress)]
    [InlineData("paused shows its own name", DownloadStatus.Paused)]
    [InlineData("extracting shows its own name", DownloadStatus.Extracting)]
    public void Convert_OtherStatuses_ReturnsStatusToString(string description, DownloadStatus status)
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
