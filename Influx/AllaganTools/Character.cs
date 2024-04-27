using System;
using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class Character
{
    private readonly object _delegate;
    private readonly FieldInfo _name;
    private readonly FieldInfo _level;

    public Character(object @delegate)
    {
        ArgumentNullException.ThrowIfNull(@delegate);
        _delegate = @delegate;
        _name = _delegate.GetType().GetField("Name")!;
        _level = _delegate.GetType().GetField("Level")!;

        CharacterId = (ulong)_delegate.GetType().GetField("CharacterId")!.GetValue(_delegate)!;
        CharacterType = (CharacterType)_delegate.GetType().GetProperty("CharacterType")!.GetValue(_delegate)!;
        ClassJob = (byte)_delegate.GetType().GetField("ClassJob")!.GetValue(_delegate)!;
        OwnerId = (ulong)_delegate.GetType().GetField("OwnerId")!.GetValue(_delegate)!;
        FreeCompanyId = (ulong)_delegate.GetType().GetField("FreeCompanyId")!.GetValue(_delegate)!;
        WorldId = (uint)_delegate.GetType().GetField("WorldId")!.GetValue(_delegate)!;
    }

    public ulong CharacterId { get; }
    public CharacterType CharacterType { get; }
    public byte ClassJob { get; }
    public ulong OwnerId { get; }
    public ulong FreeCompanyId { get; }
    public uint WorldId { get; }
    public string Name => (string)_name.GetValue(_delegate)!;
    public uint Level => (uint)_level.GetValue(_delegate)!;
}
