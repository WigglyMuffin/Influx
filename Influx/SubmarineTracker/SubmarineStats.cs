namespace Influx.SubmarineTracker;

public sealed class SubmarineStats
{
    public required string Name { get; init; }
    public required int Id { get; init; }
    public required ushort Level { get; init; }
}
