using System.Collections.Generic;

namespace Influx.AllaganTools;

internal interface IInventoryMonitor
{
    public IReadOnlyDictionary<ulong, Inventory> All { get; }
}
