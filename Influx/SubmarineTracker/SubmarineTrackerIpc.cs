using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Influx.AllaganTools;
using LLib;

namespace Influx.SubmarineTracker;

internal sealed class SubmarineTrackerIpc
{
    private readonly DalamudReflector _dalamudReflector;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _pluginLog;

    public SubmarineTrackerIpc(DalamudReflector dalamudReflector, IChatGui chatGui, IPluginLog pluginLog)
    {
        _dalamudReflector = dalamudReflector;
        _chatGui = chatGui;
        _pluginLog = pluginLog;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public IReadOnlyDictionary<Character, List<SubmarineStats>> GetSubmarineStats(List<Character> characters)
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
                .Select(x => new SubmarineInfo(x.Fc!, x.Subs))
                .GroupBy(x => x.Fc)
                .ToDictionary(x => x.Key, x =>
                {
                    if (x.Count() != 1)
                    {
                        _chatGui.PrintError($"[Influx] Unable to collect data, FC '{x.Key.Name}' is included in statistics through multiple characters/owners.");
                        var characterNames = characters.Where(y => y.FreeCompanyId == x.Key.CharacterId).Select(y => y.Name).ToList();
                        throw new InvalidOperationException($"Unable to collect FC data for FC '{x.Key}'{Environment.NewLine}Multiple characters include the same FC ({string.Join(", ", characterNames)}), only one of them should have 'Include Free Company Statistics' set");
                    }

                    return x.Single().Subs;
                });
        }
        else
            return new Dictionary<Character, List<SubmarineStats>>();
    }

    private sealed record SubmarineInfo(Character Fc, List<SubmarineStats> Subs)
    {
        public SubmarineInfo(Character fc, IList<Submarine> subs)
            : this(fc, subs.Select(x => Convert(fc, subs.IndexOf(x), x)).ToList())
        {
        }

        private static SubmarineStats Convert(Character fc, int index, Submarine y)
        {
            return new SubmarineStats
            {
                Id = index,
                Name = y.Name,
                WorldId = fc.WorldId,
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
