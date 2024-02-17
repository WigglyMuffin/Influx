using System.Collections.Generic;

namespace Influx.LocalStatistics;

public sealed record LocalStats
{
    public ulong ContentId { get; init; }
    public byte GrandCompany { get; init; }
    public byte GcRank { get; init; }
    public bool SquadronUnlocked { get; init; }
    public byte MaxLevel { get; init; } = 90;
    public List<short> ClassJobLevels { get; set; } = new();
    public byte StartingTown { get; init; }
    public int MsqCount { get; set; } = -1;
    public string? MsqName { get; set; }
    public uint MsqGenre { get; set; }
    public int? Gil { get; set; }
}
