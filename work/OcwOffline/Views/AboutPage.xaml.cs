namespace OcwOffline.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        VersionLabel.Text = $"Version {AppInfo.VersionString}";
    }

    private async void OnPrivacyPolicyClicked(object sender, EventArgs e) =>
        await OpenExternalLinkAsync(ExternalLinks.PrivacyPolicyUrl);

    private async void OnContributeClicked(object sender, EventArgs e) =>
        await OpenExternalLinkAsync(ExternalLinks.GitHubRepositoryUrl);

    private async Task OpenExternalLinkAsync(string url)
    {
        if (!ExternalLinks.IsConfigured(url))
        {
            await DisplayAlertAsync("Not available", "This link has not been set up yet.", "OK");
            return;
        }

        await Launcher.OpenAsync(url);
    }
}
