namespace Influx.SubmarineTracker;

public sealed class SubmarineStats
{
    public required string Name { get; init; }
    public required int Id { get; init; }
    public required ushort Level { get; init; }
    public required ushort PredictedLevel { get; init; }

    public required string Hull { get; init; }
    public required string Stern { get; init; }
    public required string Bow { get; init; }
    public required string Bridge { get; init; }
    public required string Build { get; init; }
    public required EState State { get; init; }
}
