using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace Influx.AllaganTools;

internal sealed class UnavailableCharacterMonitor(IPluginLog pluginLog) : ICharacterMonitor
{
    public IEnumerable<Character> PlayerCharacters
    {
        get
        {
            pluginLog.Warning("Character monitor is unavailable");
            return Array.Empty<Character>();
        }
    }

    public IEnumerable<Character> All
    {
        get
        {
            pluginLog.Warning("Character monitor is unavailable");
            return Array.Empty<Character>();
        }
    }
}
