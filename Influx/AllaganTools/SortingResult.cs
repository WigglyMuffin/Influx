using System;

namespace Influx.AllaganTools;

using ItemFlags = FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags;
internal sealed class SortingResult
{
    public SortingResult(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);

        LocalContentId = (ulong)@delegate.GetType().GetProperty("SourceRetainerId")!.GetValue(@delegate)!;
        Quantity = (int)@delegate.GetType().GetProperty("Quantity")!.GetValue(@delegate)!;

        var inventoryItem = @delegate.GetType().GetProperty("InventoryItem")!.GetValue(@delegate)!;
        ItemId = (uint)inventoryItem.GetType().GetField("ItemId")!.GetValue(inventoryItem)!;
        Flags = (ItemFlags)inventoryItem.GetType().GetField("Flags")!.GetValue(inventoryItem)!;
    }

    public ulong LocalContentId { get; }
    public uint ItemId { get; }
    public ItemFlags Flags { get; }
    public int Quantity { get; }

    public bool IsHq => Flags.HasFlag(ItemFlags.HQ);

    public override string ToString()
    {
        return $"{LocalContentId}, {ItemId}, {Quantity}";
    }
}
