using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Influx.AllaganTools;

internal sealed class FilterResult
{
    private readonly object _delegate;
    private readonly PropertyInfo _sortedItems;

    public FilterResult(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        _delegate = @delegate;
        _sortedItems =
            _delegate.GetType().GetProperty("SortedItems") ?? throw new MissingMemberException();
    }

    public IReadOnlyList<SortingResult> GenerateFilteredList()
    {
        return ((IEnumerable)_sortedItems.GetValue(_delegate)!)
            .Cast<object>()
            .Select(x => new SortingResult(x))
            .ToList();
    }
}
