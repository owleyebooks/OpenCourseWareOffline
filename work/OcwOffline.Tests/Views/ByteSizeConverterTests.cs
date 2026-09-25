using System.Globalization;
using OcwOffline.Views;

namespace OcwOffline.Tests.Views;

public class ByteSizeConverterTests
{
    [Theory]
    [InlineData("zero bytes", 0L, "0 bytes")]
    [InlineData("under a kilobyte", 512L, "512 bytes")]
    [InlineData("just under a kilobyte", 1023L, "1023 bytes")]
    [InlineData("exactly one kilobyte", 1024L, "1 KB")]
    [InlineData("fractional kilobytes", 1536L, "1.5 KB")]
    [InlineData("whole megabytes hide decimals", 5242880L, "5 MB")]
    [InlineData("fractional gigabytes", 1932735283L, "1.8 GB")]
    [InlineData("exactly one gigabyte", 1073741824L, "1 GB")]
    public void Format_Boundaries_FormatsHumanReadable(string description, long bytes, string expected)
    {
        ByteSizeConverter.Format(bytes).Should().Be(expected, $"case: {description}");
    }

    [Fact]
    public void Convert_LongValue_UsesFormat()
    {
        var converter = new ByteSizeConverter();

        converter.Convert(2048L, typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be("2 KB", "the IValueConverter path formats like the static method");
    }

    [Fact]
    public void Convert_NonLongValue_ReturnsEmpty()
    {
        var converter = new ByteSizeConverter();

        converter.Convert("nope", typeof(string), null, CultureInfo.InvariantCulture)
            .Should().Be(string.Empty, "a non-size binding shows nothing rather than crashing");
    }
}
