namespace Influx.AllaganTools;

internal interface IFilterService
{
    Filter? GetFilterByKeyOrName(string keyOrName);
}
