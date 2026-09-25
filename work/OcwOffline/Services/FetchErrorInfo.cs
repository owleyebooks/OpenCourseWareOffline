using System.Net;

namespace OcwOffline.Services;

/// <summary>
/// What the course page's error panel shows when a fetch fails. The UI
/// never renders the raw exception; ClassifyFetchError maps it to one of
/// three plain-language cases. Positional so adding a case breaks every
/// construction site at compile time.
/// </summary>
public record FetchErrorInfo(string Title, string Detail, bool IsNetworkError)
{
    public static FetchErrorInfo ClassifyFetchError(Exception ex)
    {
        // OcwScraperService fetches via HttpClient.GetStringAsync, so a bad
        // address surfaces as HttpRequestException with a 404 status code.
        // Check it before the generic network case below.
        if (ex is HttpRequestException httpEx && httpEx.StatusCode == HttpStatusCode.NotFound)
            return new FetchErrorInfo(
                "We couldn't find a course at that address",
                "Double-check the link and try again.",
                false);

        // HttpRequestException covers DNS failures and refused connections;
        // TaskCanceledException is what HttpClient's own timeout surfaces as.
        if (ex is HttpRequestException || ex is TaskCanceledException)
            return new FetchErrorInfo(
                "Couldn't reach ocw.mit.edu",
                "Check your connection and try again.",
                true);

        return new FetchErrorInfo(
            "Something went wrong",
            "Please try again later.",
            false);
    }
}
