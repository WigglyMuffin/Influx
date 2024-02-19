using System;

namespace Influx.SubmarineTracker;

public sealed class Submarine
{
    public Submarine(object @delegate)
    {
        Name = (string)@delegate.GetType().GetProperty("Name")!.GetValue(@delegate)!;
        Level = (ushort)@delegate.GetType().GetProperty("Rank")!.GetValue(@delegate)!;
        Build = new Build(@delegate.GetType().GetProperty("Build")!.GetValue(@delegate)!);

        try
        {
            (uint predictedLevel, double _) = ((uint, double))@delegate.GetType().GetMethod("PredictExpGrowth")!.Invoke(@delegate, Array.Empty<object?>())!;
            PredictedLevel = (ushort)predictedLevel;
        }
        catch (Exception)
        {
            PredictedLevel = Level;
        }
    }

    public string Name { get; }
    public ushort Level { get; }
    public ushort PredictedLevel { get; }
    public Build Build { get; }
}
