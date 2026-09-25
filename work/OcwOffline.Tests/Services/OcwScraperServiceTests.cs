using OcwOffline.Models;
using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class OcwScraperServiceTests
{
    [Fact]
    public void MakeAbsolute_AlreadyHttps_ReturnsUnchanged()
    {
        var result = OcwScraperService.MakeAbsolute("https://ocw.mit.edu/foo/bar.pdf");

        Assert.Equal("https://ocw.mit.edu/foo/bar.pdf", result);
    }

    [Fact]
    public void MakeAbsolute_AlreadyHttp_ReturnsUnchanged()
    {
        var result = OcwScraperService.MakeAbsolute("http://example.com/foo.pdf");

        Assert.Equal("http://example.com/foo.pdf", result);
    }

    [Fact]
    public void MakeAbsolute_RootRelative_PrependsHost()
    {
        var result = OcwScraperService.MakeAbsolute("/courses/foo/bar.pdf");

        Assert.Equal("https://ocw.mit.edu/courses/foo/bar.pdf", result);
    }

    [Fact]
    public void MakeAbsolute_RelativeWithoutLeadingSlash_PrependsHostAndSlash()
    {
        var result = OcwScraperService.MakeAbsolute("courses/foo/bar.pdf");

        Assert.Equal("https://ocw.mit.edu/courses/foo/bar.pdf", result);
    }

    [Theory]
    [InlineData("kilobytes convert to bytes", "10", "KB", 10L * 1024)]
    [InlineData("unit matching is case-insensitive", "10", "kB", 10L * 1024)]
    [InlineData("fractional megabytes convert to bytes", "1.5", "MB", (long)(1.5 * 1024 * 1024))]
    [InlineData("gigabytes convert to bytes", "2", "GB", 2L * 1024 * 1024 * 1024)]
    public void ParseSize_KnownUnits_ConvertsToBytes(string description, string numeric, string unit, long expected)
    {
        var result = OcwScraperService.ParseSize(numeric, unit);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseSize_UnrecognizedUnit_TreatsAsBytes()
    {
        var result = OcwScraperService.ParseSize("42", "TB");

        Assert.Equal(42L, result);
    }

    [Fact]
    public void ParseSize_NonNumericValue_ReturnsZero()
    {
        var result = OcwScraperService.ParseSize("not-a-number", "MB");

        Assert.Equal(0L, result);
    }

    [Fact]
    public void ClassifyArtifact_ZipLabel_ReturnsZip()
    {
        var result = OcwScraperService.ClassifyArtifact("https://ocw.mit.edu/x", "zip");

        Assert.Equal(ArtifactFileType.Zip, result);
    }

    [Fact]
    public void ClassifyArtifact_ZipExtensionRegardlessOfLabel_ReturnsZip()
    {
        var result = OcwScraperService.ClassifyArtifact("https://ocw.mit.edu/x.ZIP", "file");

        Assert.Equal(ArtifactFileType.Zip, result);
    }

    [Fact]
    public void ClassifyArtifact_PdfLabel_ReturnsPdf()
    {
        var result = OcwScraperService.ClassifyArtifact("https://ocw.mit.edu/x", "pdf");

        Assert.Equal(ArtifactFileType.Pdf, result);
    }

    [Fact]
    public void ClassifyArtifact_HtmlExtension_ReturnsHtml()
    {
        var result = OcwScraperService.ClassifyArtifact("https://ocw.mit.edu/x.html", "file");

        Assert.Equal(ArtifactFileType.Html, result);
    }

    [Fact]
    public void ClassifyArtifact_HtmExtension_ReturnsHtml()
    {
        var result = OcwScraperService.ClassifyArtifact("https://ocw.mit.edu/x.htm", "file");

        Assert.Equal(ArtifactFileType.Html, result);
    }

    [Fact]
    public void ClassifyArtifact_NoMatchingLabelOrExtension_ReturnsOther()
    {
        var result = OcwScraperService.ClassifyArtifact("https://ocw.mit.edu/x.docx", "file");

        Assert.Equal(ArtifactFileType.Other, result);
    }
}

public class OcwScraperServiceNormalizeCourseInputTests
{
    [Fact]
    public void NormalizeCourseInput_FullCourseUrl_ReturnsSlug()
    {
        var result = OcwScraperService.NormalizeCourseInput(
            "https://ocw.mit.edu/courses/hst-508-genomics-and-computational-biology-fall-2002/");

        Assert.Equal("hst-508-genomics-and-computational-biology-fall-2002", result);
    }

    [Fact]
    public void NormalizeCourseInput_DeepCourseUrl_ReturnsSlug()
    {
        var result = OcwScraperService.NormalizeCourseInput(
            "https://ocw.mit.edu/courses/hst-508-genomics-and-computational-biology-fall-2002/pages/syllabus/");

        Assert.Equal("hst-508-genomics-and-computational-biology-fall-2002", result);
    }

    [Fact]
    public void NormalizeCourseInput_BareSlug_ReturnsTrimmedSlug()
    {
        var result = OcwScraperService.NormalizeCourseInput("  hst-508-genomics-and-computational-biology-fall-2002  ");

        Assert.Equal("hst-508-genomics-and-computational-biology-fall-2002", result);
    }

    [Theory]
    [InlineData("null returns empty", null)]
    [InlineData("empty returns empty", "")]
    [InlineData("whitespace returns empty", "   ")]
    public void NormalizeCourseInput_NullOrWhitespace_ReturnsEmpty(string description, string? input)
    {
        var result = OcwScraperService.NormalizeCourseInput(input);

        Assert.Equal(string.Empty, result);
    }
}
