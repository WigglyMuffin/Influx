namespace Influx.SubmarineTracker;

public sealed class Submarine
{
    public Submarine(object @delegate)
    {
        Name = (string)@delegate.GetType().GetProperty("Name")!.GetValue(@delegate)!;
        Level = (ushort)@delegate.GetType().GetProperty("Rank")!.GetValue(@delegate)!;
        Build = new Build(@delegate.GetType().GetProperty("Build")!.GetValue(@delegate)!);
    }

    public string Name { get; }
    public ushort Level { get; }
    public Build Build { get; }
}
