using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Influx.SubmarineTracker;

internal sealed class FcSubmarines
{
    public FcSubmarines(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        Submarines = ((IEnumerable)@delegate.GetType().GetField("Submarines")!.GetValue(@delegate)!)
            .Cast<object>()
            .Select(x => new Submarine(x))
            .ToList()
            .AsReadOnly();
    }

    public IList<Submarine> Submarines { get; }
}
