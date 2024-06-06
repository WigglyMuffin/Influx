using System;
using Dalamud.Logging;

namespace Influx.AllaganTools;

internal sealed class InventoryItem
{
    public InventoryItem(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        Category = (int)@delegate.GetType().GetField("SortedCategory")!.GetValue(@delegate)!;
        Container = (int)@delegate.GetType().GetField("SortedContainer")!.GetValue(@delegate)!;
        ItemId = (uint)@delegate.GetType().GetProperty("ItemId")!.GetValue(@delegate)!;
        Quantity = (uint)@delegate.GetType().GetField("Quantity")!.GetValue(@delegate)!;
    }

    public int Category { get; }
    public int Container { get; }
    public uint ItemId { get; }
    public uint Quantity { get; }
}
