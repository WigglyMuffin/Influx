using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class FilterResult
{
    private readonly IEnumerable _searchResultList;

    public FilterResult(IEnumerable searchResultList)
    {
        ArgumentNullException.ThrowIfNull(searchResultList);
        _searchResultList = searchResultList;
    }

    public IReadOnlyList<SortingResult> GenerateFilteredList()
    {
        return _searchResultList
            .Cast<object>()
            .Select(x => x.GetType()
                .GetField("_sortingResult", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(x)!)
            .Select(x => new SortingResult(x))
            .ToList();
    }
}
