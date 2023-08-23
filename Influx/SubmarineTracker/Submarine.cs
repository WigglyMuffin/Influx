namespace Influx.SubmarineTracker;

public sealed class Submarine
{
    private readonly object _delegate;

    public Submarine(object @delegate)
    {
        _delegate = @delegate;
        Name = (string)_delegate.GetType().GetProperty("Name")!.GetValue(_delegate)!;
        Level = (ushort)_delegate.GetType().GetProperty("Rank")!.GetValue(_delegate)!;
    }

    public string Name { get; set; }
    public ushort Level { get; }
}
