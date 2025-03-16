using System;
using System.Collections.Immutable;

namespace Influx.Remote;

internal sealed record StatisticsValues
{
    private StatisticsValues(string name)
    {
        Name = name;
        Tags = ImmutableSortedDictionary<string, string>.Empty;
        BoolFields = ImmutableSortedDictionary<string, bool>.Empty;
        LongFields = ImmutableSortedDictionary<string, long>.Empty;
        UlongFields = ImmutableSortedDictionary<string, ulong>.Empty;
        Time = DateTime.Now;
    }

    public string Name { get; }
    public ImmutableSortedDictionary<string, string> Tags { get; private init; }
    public ImmutableSortedDictionary<string, bool> BoolFields { get; private init; }
    public ImmutableSortedDictionary<string, long> LongFields { get; private init; }
    public ImmutableSortedDictionary<string, ulong> UlongFields { get; private init; }
    public DateTime Time { get; private init; }

    public static StatisticsValues Measurement(string measurement)
    {
        return new StatisticsValues(measurement);
    }

    public StatisticsValues Tag(string name, string? value)
    {
        if (value == null)
            return this with { Tags = Tags.Remove(name) };
        else
            return this with { Tags = Tags.SetItem(name, value) };
    }

    public StatisticsValues Field(string name, bool value)
    {
        return this with { BoolFields = BoolFields.SetItem(name, value) };
    }

    public StatisticsValues Field(string name, long value)
    {
        return this with { LongFields = LongFields.SetItem(name, value) };
    }

    public StatisticsValues Field(string name, byte value) => Field(name, (long)value);
    public StatisticsValues Field(string name, short value) => Field(name, (long)value);
    public StatisticsValues Field(string name, int value) => Field(name, (long)value);


    public StatisticsValues Field(string name, ulong value)
    {
        // this probably has no practical impact, but questdb doesn't techncially support unsigned longs, only longs
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (ulong)long.MaxValue);

        return this with { UlongFields = UlongFields.SetItem(name, value) };
    }

    public StatisticsValues Field(string name, uint value) => Field(name, (ulong)value);
    public StatisticsValues Field(string name, ushort value) => Field(name, (ulong)value);

    public StatisticsValues Timestamp(DateTime timestamp)
    {
        return this with { Time = timestamp };
    }
}
