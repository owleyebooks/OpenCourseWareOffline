using OcwOffline.Services;

namespace OcwOffline.Tests.Services;

public class DownloadAggregateTests
{
    [Fact]
    public void Compute_NoSnapshots_ReturnsNone()
    {
        var result = DownloadAggregate.Compute(Enumerable.Empty<DownloadProgress>());

        result.Should().Be(DownloadAggregate.None, "no snapshots means nothing is downloading");
    }

    [Theory]
    [InlineData("a download at half", 500, 1000, 0.5)]
    [InlineData("a finished download", 1000, 1000, 1.0)]
    public void Compute_SingleSnapshot_FractionIsReceivedOverTotal(
        string description, long received, long total, double fraction)
    {
        var result = DownloadAggregate.Compute(new[] { new DownloadProgress("k", received, total) });

        result.ActiveCount.Should().Be(1);
        result.OverallFraction.Should().BeApproximately(
            fraction, 1e-9, $"one download stands alone ({description})");
        result.BytesReceived.Should().Be(received);
        result.TotalBytes.Should().Be(total);
    }

    [Fact]
    public void Compute_TwoDownloads_WeightsByTotalBytesNotByCount()
    {
        var snapshots = new[]
        {
            new DownloadProgress("big", 450_000_000, 900_000_000), // 50% of 900 MB
            new DownloadProgress("small", 100_000_000, 100_000_000), // 100% of 100 MB
        };

        var result = DownloadAggregate.Compute(snapshots);

        result.ActiveCount.Should().Be(2);
        result.OverallFraction.Should().BeApproximately(
            0.55, 1e-9,
            "a naive per-download average would say 75%; the byte-weighted roll-up is 550 MB of 1000 MB");
        result.BytesReceived.Should().Be(550_000_000);
        result.TotalBytes.Should().Be(1_000_000_000);
    }

    [Fact]
    public void Compute_UnknownTotal_CountsTowardActiveCountButNotTheFraction()
    {
        var snapshots = new[]
        {
            new DownloadProgress("unknown", 100, 0),
            new DownloadProgress("known", 50, 100),
        };

        var result = DownloadAggregate.Compute(snapshots);

        result.ActiveCount.Should().Be(2, "a download with no total yet is still an active download");
        result.OverallFraction.Should().BeApproximately(
            0.5, 1e-9, "only the snapshot with a known total feeds the fraction");
        result.BytesReceived.Should().Be(50, "bytes from the unknown-total snapshot are excluded too");
        result.TotalBytes.Should().Be(100);
    }
}
