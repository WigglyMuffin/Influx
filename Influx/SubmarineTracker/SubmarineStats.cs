using System.Collections.Generic;

namespace Influx.SubmarineTracker;

internal sealed class SubmarineStats
{
    public List<SingleSubmarineStats> Submarines { get; init; } = new();
    public int FreeSlots { get; set; }
}
