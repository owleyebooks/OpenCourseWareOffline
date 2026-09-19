namespace OcwOffline;

// External URLs the app links to. Both are placeholders until the
// project owner fills them in: the GitHub repo does not exist yet and
// the privacy policy is not hosted yet. The About page only opens a
// link when IsConfigured says it points at a real address.
public static class ExternalLinks
{
    public const string GitHubRepositoryUrl = "https://github.com/owleyebooks/OpenCourseWareOffline";
    public const string PrivacyPolicyUrl = "TODO: set the hosted privacy policy URL";

    public static bool IsConfigured(string url) =>
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
