namespace Influx.LocalStatistics;

public sealed record FcStats
{
    public ulong ContentId { get; init; }
    public int FcCredits { get; init; }
}
