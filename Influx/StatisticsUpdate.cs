using System.Collections.Generic;

namespace Influx;

internal sealed class StatisticsUpdate
{
    public IReadOnlyDictionary<AllaganToolsIPC.Character, AllaganToolsIPC.Currencies> Currencies { get; init; }
}
