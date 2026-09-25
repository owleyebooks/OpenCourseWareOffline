namespace OcwOffline.Services;

/// <summary>
/// One in-flight download's latest progress snapshot, for the Downloads
/// dashboard's active section. Keys follow the established
/// "artifact-{id}" / "lecture-{id}" convention.
/// </summary>
public record ActiveDownload(string Key, long BytesReceived, long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? (double)BytesReceived / TotalBytes : 0;
}
