using System;
using System.Collections;
using System.Reflection;

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
        return f != null ? new FilterResult((IEnumerable)_refreshList.Invoke(_listFilterService, [f])!) : null;
    }
}
