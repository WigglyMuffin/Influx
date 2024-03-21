using System;

namespace Influx.AllaganTools;

internal sealed class InventoryItem
{
    public InventoryItem(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        ItemId = (uint)@delegate.GetType().GetField("ItemId")!.GetValue(@delegate)!;
        Quantity = (uint)@delegate.GetType().GetField("Quantity")!.GetValue(@delegate)!;
    }

    public uint ItemId { get; }
    public uint Quantity { get; }
}
