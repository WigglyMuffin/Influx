using System.Collections.Generic;

namespace Influx.AllaganTools;

internal interface ICharacterMonitor
{
    IEnumerable<Character> PlayerCharacters { get; }
    IEnumerable<Character> All { get; }
}
