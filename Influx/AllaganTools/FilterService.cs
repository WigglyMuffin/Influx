using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class FilterService : IFilterService
{
    private readonly object _delegate;
    private readonly MethodInfo _getFilterByKeyOrName;

    public FilterService(object @delegate)
    {
        _delegate = @delegate;
        _getFilterByKeyOrName = _delegate.GetType().GetMethod("GetFilterByKeyOrName")!;
    }

    public Filter? GetFilterByKeyOrName(string keyOrName)
    {
        var f = _getFilterByKeyOrName.Invoke(_delegate, new object?[] { keyOrName });
        return f != null ? new Filter(f) : null;
    }
}
