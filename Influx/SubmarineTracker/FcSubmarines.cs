using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Influx.SubmarineTracker;

public sealed class FcSubmarines
{
    private readonly object _delegate;

    public FcSubmarines(object @delegate)
    {
        _delegate = @delegate;
        Submarines = ((IEnumerable)_delegate.GetType().GetField("Submarines")!.GetValue(_delegate)!)
            .Cast<object>()
            .Select(x => new Submarine(x))
            .ToList();
    }

    public List<Submarine> Submarines { get; }
}
