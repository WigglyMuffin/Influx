using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using Influx.AllaganTools;
using LLib;

namespace Influx.SubmarineTracker;

internal sealed class SubmarineTrackerIpc
{
    private readonly DalamudReflector _dalamudReflector;

    public SubmarineTrackerIpc(DalamudReflector dalamudReflector)
    {
        _dalamudReflector = dalamudReflector;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public IReadOnlyDictionary<Character, SubmarineStats> GetSubmarineStats(List<Character> characters)
    {
        if (_dalamudReflector.TryGetDalamudPlugin("Submarine Tracker", out IDalamudPlugin? it, false, true))
        {
            var databaseCache = it.GetType()
                .GetField("DatabaseCache", BindingFlags.Static | BindingFlags.Public)!
                .GetValue(null)!;
            var getSubmarines = databaseCache.GetType()
                .GetMethod("GetSubmarines", [])!;
            var knownSubmarineData = ((IEnumerable)getSubmarines.Invoke(databaseCache, [])!).Cast<object>();
            return knownSubmarineData
                .Select(x => new Submarine(x))
                .GroupBy(x => x.FreeCompanyId)
                .Select(x => new SubmarineInfo(
                    characters.SingleOrDefault(y =>
                        y.CharacterType == CharacterType.FreeCompanyChest && y.CharacterId == x.Key),
                    x.ToList()
                ))
                .Where(x => x.Fc != null)
                .ToDictionary(x => x.Fc!, x => x.Subs);
        }
        else
            return new Dictionary<Character, SubmarineStats>();
    }

    private sealed record SubmarineInfo(Character? Fc, SubmarineStats Subs)
    {
        public SubmarineInfo(Character? fc, List<Submarine> subs)
            : this(fc, new SubmarineStats { Submarines = subs.Select(x => Convert(fc, subs.IndexOf(x), x)).ToList() })
        {
        }

        private static SingleSubmarineStats Convert(Character? fc, int index, Submarine y)
        {
            return new SingleSubmarineStats
            {
                Id = index,
                Name = y.Name,
                WorldId = fc?.WorldId ?? 0,
                Level = y.Level,
                PredictedLevel = y.PredictedLevel,
                Hull = y.Build.HullIdentifier,
                Stern = y.Build.SternIdentifier,
                Bow = y.Build.BowIdentifier,
                Bridge = y.Build.BridgeIdentifier,
                Build = y.Build.FullIdentifier,
                State = y.State,
                ReturnTime = y.ReturnTime,
            };
        }
    }
}
