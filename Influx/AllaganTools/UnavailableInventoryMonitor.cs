using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace Influx.AllaganTools;

internal sealed class UnavailableInventoryMonitor(IPluginLog pluginLog) : IInventoryMonitor
{
    public IReadOnlyDictionary<ulong, Inventory> All
    {
        get
        {
            pluginLog.Warning("Inventory monitor is unavailable");
            return new Dictionary<ulong, Inventory>();
        }
    }
}
