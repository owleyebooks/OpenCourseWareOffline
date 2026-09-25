namespace OcwOffline.Services;

/// <summary>
/// Point-in-time roll-up of every in-flight download, for the persistent
/// banner and the Downloads dashboard. Computed from per-key progress
/// snapshots; the weighted fraction keeps a 900 MB video from being
/// drowned out by a 40 KB syllabus.
/// </summary>
public record DownloadAggregate(int ActiveCount, double OverallFraction, long BytesReceived, long TotalBytes)
{
    public static DownloadAggregate None => new(0, 0, 0, 0);

    public static DownloadAggregate Compute(IEnumerable<DownloadProgress> snapshots)
    {
        var list = snapshots.ToList();
        if (list.Count == 0)
            return None;

        var known = list.Where(p => p.TotalBytes > 0).ToList();
        var received = known.Sum(p => p.BytesReceived);
        var total = known.Sum(p => p.TotalBytes);
        var fraction = total > 0 ? (double)received / total : 0;
        return new DownloadAggregate(list.Count, fraction, received, total);
    }
}
