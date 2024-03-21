using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Influx.AllaganTools;

internal sealed class Filter
{
    private readonly object _delegate;
    private readonly MethodInfo _generateFilteredList;

    public Filter(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        _delegate = @delegate;
        _generateFilteredList = _delegate.GetType().GetMethod("GenerateFilteredList")!;
    }

    public IReadOnlyList<SortingResult> GenerateFilteredList()
    {
        Task task = (Task)_generateFilteredList.Invoke(_delegate, new object?[] { null })!;
        object result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return ((IEnumerable)result.GetType().GetProperty("SortedItems")!.GetValue(result)!)
            .Cast<object>()
            .Select(x => new SortingResult(x))
            .ToList();
    }
}
