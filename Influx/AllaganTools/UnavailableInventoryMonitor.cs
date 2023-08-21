using System.Collections.Generic;

namespace Influx.AllaganTools;

internal sealed class UnavailableInventoryMonitor : IInventoryMonitor
{
    public IReadOnlyDictionary<ulong, Inventory> All => new Dictionary<ulong, Inventory>();
}
