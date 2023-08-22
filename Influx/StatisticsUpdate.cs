using System.Collections.Generic;
using Influx.AllaganTools;
using Influx.SubmarineTracker;

namespace Influx;

internal sealed class StatisticsUpdate
{
    public required IReadOnlyDictionary<Character, Currencies> Currencies { get; init; }
    public required Dictionary<Character, List<SubmarineStats>> Submarines { get; init; }
}
