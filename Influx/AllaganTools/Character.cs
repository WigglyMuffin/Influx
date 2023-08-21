using System.Reflection;

namespace Influx.AllaganTools;

internal sealed class Character
{
    private readonly object _delegate;
    private readonly FieldInfo _name;

    public Character(object @delegate)
    {
        _delegate = @delegate;
        _name = _delegate.GetType().GetField("Name")!;

        CharacterId = (ulong)_delegate.GetType().GetField("CharacterId")!.GetValue(_delegate)!;
        CharacterType = (CharacterType)_delegate.GetType().GetProperty("CharacterType")!.GetValue(_delegate)!;
        OwnerId = (ulong)_delegate.GetType().GetField("OwnerId")!.GetValue(_delegate)!;
        FreeCompanyId = (ulong)_delegate.GetType().GetField("FreeCompanyId")!.GetValue(_delegate)!;
    }

    public ulong CharacterId { get; }
    public CharacterType CharacterType { get; }
    public ulong OwnerId { get; }
    public ulong FreeCompanyId { get; }
    public string Name => (string)_name.GetValue(_delegate)!;
}
