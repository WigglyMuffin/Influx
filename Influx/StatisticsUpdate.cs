using System.Collections.Generic;
using Influx.AllaganTools;

namespace Influx;

internal sealed class StatisticsUpdate
{
    public required IReadOnlyDictionary<Character, Currencies> Currencies { get; init; }
}
