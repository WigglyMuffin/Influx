namespace Influx.LocalStatistics;

public record LocalStats
{
    public ulong ContentId { get; init; }
    public byte GrandCompany { get; init; }
    public byte GcRank { get; init; }
    public bool SquadronUnlocked { get; init; }
}
