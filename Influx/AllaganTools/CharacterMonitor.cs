using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class CharacterMonitor : ICharacterMonitor
{
    private readonly object _delegate;
    private readonly MethodInfo _getPlayerCharacters;
    private readonly MethodInfo _allCharacters;

    public CharacterMonitor(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        _delegate = @delegate;
        _getPlayerCharacters = _delegate.GetType().GetMethod("GetPlayerCharacters")!;
        _allCharacters = _delegate.GetType().GetMethod("AllCharacters")!;
    }

    public IEnumerable<Character> PlayerCharacters => GetCharactersInternal(_getPlayerCharacters);
    public IEnumerable<Character> All => GetCharactersInternal(_allCharacters);

    private List<Character> GetCharactersInternal(MethodInfo methodInfo)
    {
        return ((IEnumerable)methodInfo.Invoke(_delegate, Array.Empty<object>())!)
            .Cast<object>()
            .Select(x => x.GetType().GetProperty("Value")!.GetValue(x)!)
            .Select(x => new Character(x))
            .ToList();
    }
}
