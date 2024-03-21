using System;

namespace Influx.SubmarineTracker;

internal sealed class Build
{
    public Build(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        Type type = @delegate.GetType();
        HullIdentifier =
            (string)type.GetProperty("HullIdentifier")!.GetValue(@delegate)!;
        SternIdentifier =
            (string)type.GetProperty("SternIdentifier")!.GetValue(@delegate)!;
        BowIdentifier =
            (string)type.GetProperty("BowIdentifier")!.GetValue(@delegate)!;
        BridgeIdentifier =
            (string)type.GetProperty("BridgeIdentifier")!.GetValue(@delegate)!;
        FullIdentifier =
            (string)type.GetMethod("FullIdentifier")!.Invoke(@delegate, Array.Empty<object>())!;
    }

    public string HullIdentifier { get; }
    public string SternIdentifier { get; }
    public string BowIdentifier { get; }
    public string BridgeIdentifier { get; }
    public string FullIdentifier { get; }
}
