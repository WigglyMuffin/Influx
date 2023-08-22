using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui;
using ECommons.Reflection;
using Influx.AllaganTools;

namespace Influx.SubmarineTracker;

internal sealed class SubmarineTrackerIpc
{
    private readonly ChatGui _chatGui;

    public SubmarineTrackerIpc(ChatGui chatGui)
    {
        _chatGui = chatGui;
    }

    public Dictionary<Character, List<SubmarineStats>> GetSubmarineStats(List<Character> characters)
    {
        if (DalamudReflector.TryGetDalamudPlugin("Submarine Tracker", out var it, false, true))
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
                        Level = y.Level,
                    }).ToList());
        }
        else
            return new Dictionary<Character, List<SubmarineStats>>();
    }
}

public sealed class FcSubmarines
{
    private readonly object _delegate;

    public FcSubmarines(object @delegate)
    {
        _delegate = @delegate;
        Submarines = ((IEnumerable)_delegate.GetType().GetField("Submarines")!.GetValue(_delegate)!)
            .Cast<object>()
            .Select(x => new Submarine(x))
            .ToList();
    }

    public List<Submarine> Submarines { get; }
}

public sealed class Submarine
{
    private readonly object _delegate;

    public Submarine(object @delegate)
    {
        _delegate = @delegate;
        Name = (string)_delegate.GetType().GetProperty("Name")!.GetValue(_delegate)!;
        Level = (ushort)_delegate.GetType().GetProperty("Rank")!.GetValue(_delegate)!;
    }

    public string Name { get; set; }
    public ushort Level { get; }
}
