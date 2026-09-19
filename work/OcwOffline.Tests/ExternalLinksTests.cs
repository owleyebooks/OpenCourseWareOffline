using OcwOffline;

namespace OcwOffline.Tests;

public class ExternalLinksTests
{
    [Theory]
    [InlineData("https URL is configured", "https://github.com/example/ocw-offline", true)]
    [InlineData("uppercase scheme is configured", "HTTPS://example.com/policy", true)]
    [InlineData("TODO placeholder is not configured", "TODO: set the GitHub repository URL", false)]
    [InlineData("empty string is not configured", "", false)]
    [InlineData("plain http is not configured", "http://example.com", false)]
    public void IsConfigured_Url_ReturnsExpected(string description, string url, bool expected)
    {
        var result = ExternalLinks.IsConfigured(url);

        Assert.Equal(expected, result);
    }
}
