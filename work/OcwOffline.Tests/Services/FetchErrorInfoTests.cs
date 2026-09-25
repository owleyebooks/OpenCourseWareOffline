using System.Net;
using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class FetchErrorInfoTests
{
    [Fact]
    public void ClassifyFetchError_PlainHttpRequestException_IsNetworkError()
    {
        var info = FetchErrorInfo.ClassifyFetchError(new HttpRequestException("no such host"));

        info.Title.Should().Be("Couldn't reach ocw.mit.edu", "a connection failure names the site");
        info.Detail.Should().Be("Check your connection and try again.", "a connection failure suggests the remedy");
        info.IsNetworkError.Should().BeTrue("a connection failure is a network error");
    }

    [Fact]
    public void ClassifyFetchError_Timeout_IsNetworkError()
    {
        var info = FetchErrorInfo.ClassifyFetchError(new TaskCanceledException("a task was canceled"));

        info.Title.Should().Be("Couldn't reach ocw.mit.edu", "a timeout names the site");
        info.Detail.Should().Be("Check your connection and try again.", "a timeout suggests the remedy");
        info.IsNetworkError.Should().BeTrue("HttpClient surfaces its own timeout as TaskCanceledException");
    }

    [Fact]
    public void ClassifyFetchError_NotFoundStatus_IsAddressError()
    {
        var info = FetchErrorInfo.ClassifyFetchError(
            new HttpRequestException("404", null, HttpStatusCode.NotFound));

        info.Title.Should().Be("We couldn't find a course at that address", "a 404 names the address problem");
        info.Detail.Should().Be("Double-check the link and try again.", "a 404 suggests fixing the link");
        info.IsNetworkError.Should().BeFalse("a bad address is not a network error");
    }

    [Fact]
    public void ClassifyFetchError_ServerErrorStatus_IsNetworkError()
    {
        var info = FetchErrorInfo.ClassifyFetchError(
            new HttpRequestException("500", null, HttpStatusCode.InternalServerError));

        info.IsNetworkError.Should().BeTrue("a non-404 HTTP failure is still a reachability problem");
        info.Title.Should().Be("Couldn't reach ocw.mit.edu");
    }

    [Fact]
    public void ClassifyFetchError_UnexpectedException_IsGenericError()
    {
        var info = FetchErrorInfo.ClassifyFetchError(new InvalidOperationException("boom"));

        info.Title.Should().Be("Something went wrong", "an unexpected failure stays vague");
        info.Detail.Should().Be("Please try again later.", "an unexpected failure suggests retrying later");
        info.IsNetworkError.Should().BeFalse("an unexpected failure is not a network error");
    }
}
