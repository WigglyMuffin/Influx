using System;

namespace Influx.SubmarineTracker;

internal sealed class Submarine
{
    public Submarine(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        Type type = @delegate.GetType();
        Name = (string)type.GetProperty("Name")!.GetValue(@delegate)!;
        Level = (ushort)type.GetProperty("Rank")!.GetValue(@delegate)!;
        Build = new Build(type.GetProperty("Build")!.GetValue(@delegate)!);

        try
        {
            (uint predictedLevel, double _) = ((uint, double))type.GetMethod("PredictExpGrowth")!.Invoke(@delegate, Array.Empty<object?>())!;
            PredictedLevel = (ushort)predictedLevel;

            bool onVoyage = (bool)type.GetMethod("IsOnVoyage")!.Invoke(@delegate, Array.Empty<object>())!;
            bool returned = (bool)type.GetMethod("IsDone")!.Invoke(@delegate, Array.Empty<object>())!;
            if (onVoyage)
                State = returned ? EState.Returned : EState.Voyage;
            else
                State = EState.NoVoyage;
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
    public EState State { get; }
}
