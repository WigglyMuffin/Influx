using System;

namespace Influx.SubmarineTracker;

internal sealed class Submarine
{
    public Submarine(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        Type type = @delegate.GetType();
        FreeCompanyId = (ulong)type.GetField("FreeCompanyId")!.GetValue(@delegate)!;
        Name = (string)type.GetField("Name")!.GetValue(@delegate)!;
        Level = (ushort)type.GetField("Rank")!.GetValue(@delegate)!;
        Build = new Build(type.GetProperty("Build")!.GetValue(@delegate)!);
        ReturnTime = (DateTime)type.GetField("ReturnTime")!.GetValue(@delegate)!;

        try
        {
            bool onVoyage = (bool)type.GetMethod("IsOnVoyage")!.Invoke(@delegate, Array.Empty<object>())!;
            bool returned = (bool)type.GetMethod("IsDone")!.Invoke(@delegate, Array.Empty<object>())!;
            if (onVoyage)
                State = returned ? EState.Returned : EState.Voyage;
            else
                State = EState.NoVoyage;

            if (State == EState.NoVoyage)
                PredictedLevel = Level;
            else
            {
                (uint predictedLevel, double _) = ((uint, double))type.GetMethod("PredictExpGrowth")!.Invoke(@delegate, Array.Empty<object?>())!;
                PredictedLevel = (ushort)predictedLevel;
            }
        }
        catch (Exception)
        {
            PredictedLevel = Level;
        }
    }

    public ulong FreeCompanyId { get; }
    public string Name { get; }
    public ushort Level { get; }
    public ushort PredictedLevel { get; }
    public Build Build { get; }
    public DateTime ReturnTime { get; }
    public EState State { get; }
}
