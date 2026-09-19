using HtmlAgilityPack;
using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class OcwScraperServiceHtmlHelpersTests
{
    private static HtmlDocument Load(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    [Fact]
    public void ExtractTitle_StandardOcwFormat_ReturnsSecondSegment()
    {
        var doc = Load("<html><head><title>Resources | 18.06 Linear Algebra | Mathematics | MIT OpenCourseWare</title></head></html>");

        var result = OcwScraperService.ExtractTitle(doc);

        Assert.Equal("18.06 Linear Algebra", result);
    }

    [Fact]
    public void ExtractTitle_NoPipes_ReturnsWholeTrimmedTitle()
    {
        var doc = Load("<html><head><title>  Just A Title  </title></head></html>");

        var result = OcwScraperService.ExtractTitle(doc);

        Assert.Equal("Just A Title", result);
    }

    [Fact]
    public void ExtractTitle_NoTitleTag_ReturnsEmptyString()
    {
        var doc = Load("<html><head></head><body>no title here</body></html>");

        var result = OcwScraperService.ExtractTitle(doc);

        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractTitle_SegmentHasWhitespace_IsTrimmed()
    {
        var doc = Load("<html><head><title>Resources |   Padded Course Title   | MIT OpenCourseWare</title></head></html>");

        var result = OcwScraperService.ExtractTitle(doc);

        Assert.Equal("Padded Course Title", result);
    }

    [Fact]
    public void ExtractZipArchiveUrl_ZipHrefPresent_ReturnsAbsoluteUrl()
    {
        var doc = Load("<html><body><a href=\"/courses/18-06/download/18-06-archive.zip\">zip 200 MB</a></body></html>");

        var result = new OcwScraperService().ExtractZipArchiveUrl(doc, "18-06");

        Assert.Equal("https://ocw.mit.edu/courses/18-06/download/18-06-archive.zip", result);
    }

    [Fact]
    public void ExtractZipArchiveUrl_AlreadyAbsoluteHref_ReturnedUnchanged()
    {
        var doc = Load("<html><body><a href=\"https://cdn.example.com/archive.zip\">zip 50 MB</a></body></html>");

        var result = new OcwScraperService().ExtractZipArchiveUrl(doc, "18-06");

        Assert.Equal("https://cdn.example.com/archive.zip", result);
    }

    [Fact]
    public void ExtractZipArchiveUrl_NoZipHref_ReturnsNull()
    {
        var doc = Load("<html><body><a href=\"/courses/18-06/download/notes.pdf\">pdf 100 kB</a></body></html>");

        var result = new OcwScraperService().ExtractZipArchiveUrl(doc, "18-06");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractZipArchiveUrl_NoAnchorsAtAll_ReturnsNull()
    {
        var doc = Load("<html><body><p>nothing to see here</p></body></html>");

        var result = new OcwScraperService().ExtractZipArchiveUrl(doc, "18-06");

        Assert.Null(result);
    }

    [Fact]
    public void ExtractZipArchiveUrl_MultipleAnchorsFirstZipWins()
    {
        var doc = Load("<html><body>" +
            "<a href=\"/courses/18-06/download/first.zip\">zip 1</a>" +
            "<a href=\"/courses/18-06/download/second.zip\">zip 2</a>" +
            "</body></html>");

        var result = new OcwScraperService().ExtractZipArchiveUrl(doc, "18-06");

        Assert.Equal("https://ocw.mit.edu/courses/18-06/download/first.zip", result);
    }
}
