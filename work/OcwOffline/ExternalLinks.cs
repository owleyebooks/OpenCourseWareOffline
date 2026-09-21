namespace OcwOffline;

// External URLs the app links to. The About page only opens a
// link when IsConfigured says it points at a real address.
public static class ExternalLinks
{
    public const string GitHubRepositoryUrl = "https://github.com/owleyebooks/OpenCourseWareOffline";
    public const string PrivacyPolicyUrl = "https://owleyebooks.github.io/OpenCourseWareOffline/privacy-policy.html";

    public static bool IsConfigured(string url) =>
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
