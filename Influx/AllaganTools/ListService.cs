using System;
using System.Collections;
using System.Reflection;
using System.Threading;

namespace Influx.AllaganTools;

internal sealed class ListService : IListService
{
    private readonly object _listService;
    private readonly object _listFilterService;
    private readonly MethodInfo _getListByKeyOrName;
    private readonly MethodInfo _refreshList;

    public ListService(object listService, object listFilterService)
    {
        ArgumentNullException.ThrowIfNull(listService);
        _listService = listService;
        _listFilterService = listFilterService;
        _getListByKeyOrName =
            _listService.GetType().GetMethod("GetListByKeyOrName") ?? throw new MissingMethodException();
        _refreshList = _listFilterService.GetType().GetMethod("RefreshList") ?? throw new MissingMethodException();
    }

    public FilterResult? GetFilterByKeyOrName(string keyOrName)
    {
        var f = _getListByKeyOrName.Invoke(_listService, [keyOrName]);
        if (f == null)
            return null;

        var parameters = _refreshList.GetParameters();

        object refreshResult;
        if (parameters.Length == 0)
        {
            refreshResult = _refreshList.Invoke(_listFilterService, Array.Empty<object>())!;
        }
        else if (parameters.Length == 1)
        {
            refreshResult = _refreshList.Invoke(_listFilterService, [f])!;
        }
        else if (parameters.Length == 2)
        {
            refreshResult = _refreshList.Invoke(_listFilterService, [f, CancellationToken.None])!;
        }
        else
        {
            throw new NotSupportedException($"RefreshList has {parameters.Length} parameters which is not supported");
        }

        return new FilterResult((IEnumerable)refreshResult);
    }
}