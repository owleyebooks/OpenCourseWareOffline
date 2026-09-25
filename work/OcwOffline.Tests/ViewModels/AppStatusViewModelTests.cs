using OcwOffline.Services;
using OcwOffline.Tests.Fakes;
using OcwOffline.ViewModels;

namespace OcwOffline.Tests.ViewModels;

public class AppStatusViewModelTests
{
    private readonly FakeDownloadManager _downloads = new();
    private readonly FakeConnectivityService _connectivity = new();
    private readonly FakeMainThreadDispatcher _mainThread = new();

    private AppStatusViewModel CreateViewModel() => new(_downloads, _connectivity, _mainThread);

    [Fact]
    public void Constructor_NoActiveDownloads_BannerHidden()
    {
        var vm = CreateViewModel();

        vm.HasActiveDownloads.Should().BeFalse(because: "no download has started");
        vm.ActiveDownloadText.Should().BeEmpty(because: "the banner has nothing to show");
    }

    [Fact]
    public void AggregateChanged_TwoActiveDownloads_ShowsCountAndRoundedPercent()
    {
        var vm = CreateViewModel();

        _downloads.RaiseAggregate(new DownloadAggregate(2, 0.43, 430, 1000));

        vm.HasActiveDownloads.Should().BeTrue(because: "two downloads are in flight");
        vm.ActiveDownloadText.Should().Be("Downloading 2 items, 43%",
            because: "the banner names the count and the rounded overall percent");
    }

    [Fact]
    public void AggregateChanged_OneActiveDownload_UsesSingularItem()
    {
        var vm = CreateViewModel();

        _downloads.RaiseAggregate(new DownloadAggregate(1, 0.07, 70, 1000));

        vm.ActiveDownloadText.Should().Be("Downloading 1 item, 7%",
            because: "a single download reads as '1 item'");
    }

    [Fact]
    public void AggregateChanged_BackToEmpty_HidesBanner()
    {
        var vm = CreateViewModel();
        _downloads.RaiseAggregate(new DownloadAggregate(1, 0.5, 500, 1000));

        _downloads.RaiseAggregate(DownloadAggregate.None);

        vm.HasActiveDownloads.Should().BeFalse(because: "the last download finished");
        vm.ActiveDownloadText.Should().BeEmpty(because: "the banner has nothing left to show");
    }

    [Fact]
    public void Constructor_OfflineAtStartup_StartsOffline()
    {
        _connectivity.SetConnected(false);

        var vm = CreateViewModel();

        vm.IsOffline.Should().BeTrue(because: "the device is already offline at startup");
    }

    [Fact]
    public void ConnectivityChanged_GoingOffline_SetsIsOffline()
    {
        var vm = CreateViewModel();

        _connectivity.SetConnected(false);

        vm.IsOffline.Should().BeTrue(because: "the device just lost its connection");
    }

    [Fact]
    public void ConnectivityChanged_BackOnline_ClearsIsOffline()
    {
        var vm = CreateViewModel();
        _connectivity.SetConnected(false);

        _connectivity.SetConnected(true);

        vm.IsOffline.Should().BeFalse(because: "the device is connected again");
    }

    [Fact]
    public void ShowActiveDownloadsCommand_RaisesNavigateToDownloadsRequested()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.NavigateToDownloadsRequested += () => raised = true;

        vm.ShowActiveDownloadsCommand.Execute(null);

        raised.Should().BeTrue(because: "tapping the banner must take the user to the downloads dashboard");
    }
}
