using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    public Dictionary<Character, List<SubmarineStats>> GetSubmarineStats(List<Character> characters)
    {
        if (_dalamudReflector.TryGetDalamudPlugin("Submarine Tracker", out IDalamudPlugin? it, false, true))
        {
            var submarineData = it.GetType().Assembly.GetType("SubmarineTracker.Data.Submarines");
            var knownSubmarineData = submarineData!.GetField("KnownSubmarines")!;
            return ((IEnumerable)knownSubmarineData.GetValue(null)!).Cast<object>()
                .Select(x => new
                {
                    OwnerId = (ulong)x.GetType().GetProperty("Key")!.GetValue(x)!,
                    FcWrapper = x.GetType().GetProperty("Value")!.GetValue(x)!
                })
                .Select(x => new
                {
                    Owner = characters.FirstOrDefault(y => y.CharacterId == x.OwnerId),
                    Subs = new FcSubmarines(x.FcWrapper).Submarines,
                })
                .Where(x => x.Owner != null)
                .Select(x => new
                {
                    x.Subs,
                    Fc = characters.FirstOrDefault(y => y.CharacterId == x.Owner!.FreeCompanyId)
                })
                .Where(x => x.Fc != null)
                .ToDictionary(
                    x => x.Fc!,
                    x => x.Subs.Select(y => new SubmarineStats
                    {
                        Id = x.Subs.IndexOf(y),
                        Name = y.Name,
                        WorldId = x.Fc!.WorldId,
                        Level = y.Level,
                        PredictedLevel = y.PredictedLevel,
                        Hull = y.Build.HullIdentifier,
                        Stern = y.Build.SternIdentifier,
                        Bow = y.Build.BowIdentifier,
                        Bridge = y.Build.BridgeIdentifier,
                        Build = y.Build.FullIdentifier,
                        State = y.State,
                        ReturnTime = y.ReturnTime,
                    }).ToList());
        }
        else
            return new Dictionary<Character, List<SubmarineStats>>();
    }
}
