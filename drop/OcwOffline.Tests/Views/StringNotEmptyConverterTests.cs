using System.Globalization;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class StringNotEmptyConverterTests
{
    private readonly StringNotEmptyConverter _converter = new();

    [Fact]
    public void Convert_NonEmptyString_ReturnsTrue()
    {
        var result = _converter.Convert("/local/path.pdf", typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Convert_NullOrEmptyString_ReturnsFalse(string? value)
    {
        var result = _converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void Convert_NonStringValue_ReturnsFalse()
    {
        var result = _converter.Convert(42, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
