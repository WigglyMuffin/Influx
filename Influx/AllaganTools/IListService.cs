namespace Influx.AllaganTools;

internal interface IListService
{
    FilterResult? GetFilterByKeyOrName(string keyOrName);
}
