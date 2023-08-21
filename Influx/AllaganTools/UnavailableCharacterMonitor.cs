using System;
using System.Collections.Generic;

namespace Influx.AllaganTools;

internal sealed class UnavailableCharacterMonitor : ICharacterMonitor
{
    public IEnumerable<Character> PlayerCharacters => Array.Empty<Character>();
    public IEnumerable<Character> All => Array.Empty<Character>();
}
