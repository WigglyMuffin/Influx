using System.Collections.Generic;
using Influx.AllaganTools;
using Influx.LocalStatistics;
using Influx.SubmarineTracker;

namespace Influx;

internal sealed class StatisticsUpdate
{
    public required IReadOnlyDictionary<Character, Currencies> Currencies { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<SortingResult>> InventoryItems { get; init; }
    public required IReadOnlyDictionary<Character, SubmarineStats> Submarines { get; init; }
    public required IReadOnlyDictionary<Character, LocalStats> LocalStats { get; init; }
    public required IReadOnlyDictionary<ulong, FcStats> FcStats { get; init; }
}
