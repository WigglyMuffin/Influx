namespace Influx.AllaganTools;

internal sealed class UnavailableFilterService : IFilterService
{
    public Filter? GetFilterByKeyOrName(string keyOrName) => null;
}
