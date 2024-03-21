using Dalamud.Plugin.Services;

namespace Influx.AllaganTools;

internal sealed class UnavailableFilterService(IPluginLog pluginLog) : IFilterService
{
    public Filter? GetFilterByKeyOrName(string keyOrName)
    {
        pluginLog.Warning("Filter Service is unavailable");
        return null;
    }
}
