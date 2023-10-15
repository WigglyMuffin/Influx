using System;

namespace Influx.SubmarineTracker;

public class Build
{
    public Build(object @delegate)
    {

        HullIdentifier =
            (string)@delegate.GetType().GetProperty("HullIdentifier")!.GetValue(@delegate)!;
        SternIdentifier =
            (string)@delegate.GetType().GetProperty("SternIdentifier")!.GetValue(@delegate)!;
        BowIdentifier =
            (string)@delegate.GetType().GetProperty("BowIdentifier")!.GetValue(@delegate)!;
        BridgeIdentifier =
            (string)@delegate.GetType().GetProperty("BridgeIdentifier")!.GetValue(@delegate)!;
        FullIdentifier =
            (string)@delegate.GetType().GetMethod("FullIdentifier")!.Invoke(@delegate, Array.Empty<object>())!;
    }

    public string HullIdentifier { get; }
    public string SternIdentifier { get; }
    public string BowIdentifier { get; }
    public string BridgeIdentifier { get; }
    public string FullIdentifier { get; }
}
