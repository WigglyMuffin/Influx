using System;

namespace Influx.SubmarineTracker;

internal sealed class SubmarineStats
{
    public required string Name { get; init; }
    public required int Id { get; init; }
    public required uint WorldId { get; init; }
    public bool Enabled { get; set; } = true;
    public required ushort Level { get; init; }
    public required ushort PredictedLevel { get; init; }

    public required string Hull { get; init; }
    public required string Stern { get; init; }
    public required string Bow { get; init; }
    public required string Bridge { get; init; }
    public required string Build { get; init; }
    public required EState State { get; init; }
    public required DateTime ReturnTime { get; init; }
}
