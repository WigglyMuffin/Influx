namespace Influx.AllaganTools;

internal sealed class InventoryItem
{
    private readonly object _delegate;

    public InventoryItem(object @delegate)
    {
        _delegate = @delegate;
        ItemId = (uint)_delegate.GetType().GetField("ItemId")!.GetValue(_delegate)!;
        Quantity = (uint)_delegate.GetType().GetField("Quantity")!.GetValue(_delegate)!;
    }

    public uint ItemId { get; }
    public uint Quantity { get; }
}
