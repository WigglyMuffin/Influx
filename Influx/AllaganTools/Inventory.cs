using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class Inventory
{
    private readonly object _delegate;
    private readonly MethodInfo _getAllInventories;

    public Inventory(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        _delegate = @delegate;
        _getAllInventories = _delegate.GetType().GetMethod("GetAllInventories")!;
        CharacterId = (ulong)_delegate.GetType().GetProperty("CharacterId")!.GetValue(_delegate)!;
    }

    public ulong CharacterId { get; }

    public IEnumerable<InventoryItem> GetAllItems() =>
        ((IEnumerable)_getAllInventories.Invoke(_delegate, Array.Empty<object>())!)
        .Cast<IEnumerable>()
        .SelectMany(x => x.Cast<object?>())
        .Where(x => x != null)
        .Select(x => new InventoryItem(x!))
        .Where(x => x.ItemId != 0)
        .ToList();
}
