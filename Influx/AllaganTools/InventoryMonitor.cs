using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class InventoryMonitor : IInventoryMonitor
{
    private readonly object _delegate;
    private readonly PropertyInfo _inventories;

    public InventoryMonitor(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        _delegate = @delegate;
        _inventories = _delegate.GetType().GetProperty("Inventories")!;
    }

    public IReadOnlyDictionary<ulong, Inventory> All =>
        ((IEnumerable)_inventories.GetValue(_delegate)!)
        .Cast<object>()
        .Select(x => x.GetType().GetProperty("Value")!.GetValue(x)!)
        .Select(x => new Inventory(x))
        .ToDictionary(x => x.CharacterId, x => x);
}
