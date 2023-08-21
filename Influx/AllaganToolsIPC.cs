using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using ECommons.Reflection;

namespace Influx;

internal sealed class AllaganToolsIPC : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly ChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly ICallGateSubscriber<bool, bool>? _initalized;
    private readonly ICallGateSubscriber<bool>? _isInitialized;

    public CharacterMonitor Characters { get; private set; }
    public InventoryMonitor Inventories { get; private set; }

    public AllaganToolsIPC(DalamudPluginInterface pluginInterface, ChatGui chatGui, Configuration configuration)
    {
        _pluginInterface = pluginInterface;
        _chatGui = chatGui;
        _configuration = configuration;

        _initalized = _pluginInterface.GetIpcSubscriber<bool, bool>("AllaganTools.Initialized");
        _isInitialized = _pluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
        _initalized.Subscribe(ConfigureIpc);

        ConfigureIpc(true);
    }

    private void ConfigureIpc(bool initialized)
    {
        try
        {
            if (DalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var it, false, true) &&
                _isInitialized != null && _isInitialized.InvokeFunc())
            {
                var pluginService = it.GetType().Assembly.GetType("InventoryTools.PluginService")!;

                Characters = new CharacterMonitor(pluginService.GetProperty("CharacterMonitor")!.GetValue(null)!);
                Inventories = new InventoryMonitor(
                    pluginService.GetProperty("InventoryMonitor")!.GetValue(null)!);
            }
        }
        catch (Exception e)
        {
            _chatGui.PrintError(e.ToString());
        }
    }

    public Dictionary<Character, Currencies> CountCurrencies()
    {
        var characters = Characters.All.ToDictionary(x => x.CharacterId, x => x);
        return Inventories.All
            .Where(x => !_configuration.ExcludedCharacters.Contains(x.Key))
            .ToDictionary(
                x => characters[x.Value.CharacterId],
                y =>
                {
                    var inv = new InventoryWrapper(y.Value.GetAllItems());
                    return new Currencies
                    {
                        Gil = inv.Sum(1),
                        FcCredits = inv.Sum(80),
                        Ventures = inv.Sum(21072),
                        CeruleumTanks = inv.Sum(10155),
                        RepairKits = inv.Sum(10373),
                    };
                });
    }

    public void Dispose()
    {
        _initalized?.Unsubscribe(ConfigureIpc);
    }

    public class CharacterMonitor
    {
        private readonly object _delegate;
        private readonly MethodInfo _getPlayerCharacters;
        private readonly MethodInfo _allCharacters;

        public CharacterMonitor(object @delegate)
        {
            _delegate = @delegate;
            _getPlayerCharacters = _delegate.GetType().GetMethod("GetPlayerCharacters")!;
            _allCharacters = _delegate.GetType().GetMethod("AllCharacters")!;
        }

        public IEnumerable<Character> PlayerCharacters => GetCharactersInternal(_getPlayerCharacters);
        public IEnumerable<Character> All => GetCharactersInternal(_allCharacters);

        private IEnumerable<Character> GetCharactersInternal(MethodInfo methodInfo)
        {
            return ((IEnumerable)methodInfo.Invoke(_delegate, Array.Empty<object>())!)
                .Cast<object>()
                .Select(x => x.GetType().GetProperty("Value")!.GetValue(x)!)
                .Select(x => new Character(x))
                .ToList();
        }
    }

    public class Character
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

    public enum CharacterType
    {
        Character,
        Retainer,
        FreeCompanyChest,
        Housing,
        Unknown,
    }

    public struct Currencies
    {
        public long Gil { get; init; }
        public long GcSeals { get; init; }
        public long FcCredits { get; init; }
        public long Ventures { get; init; }
        public long CeruleumTanks { get; init; }
        public long RepairKits { get; init; }
    }

    public sealed class InventoryMonitor
    {
        private readonly object _delegate;
        private readonly PropertyInfo _inventories;

        public InventoryMonitor(object @delegate)
        {
            _delegate = @delegate;
            _inventories = _delegate.GetType().GetProperty("Inventories")!;
        }

        public IReadOnlyDictionary<ulong, Inventory> All =>
            ((IEnumerable)_inventories.GetValue(_delegate)!)
            .Cast<object>()
            .Select(x => x.GetType().GetProperty("Value")!.GetValue(x)!)
            .Select(x => new Inventory(x))
            .ToDictionary(x => x.CharacterId, x => x);
    }

    public sealed class Inventory
    {
        private readonly object _delegate;
        private readonly MethodInfo _getAllInventories;

        public Inventory(object @delegate)
        {
            _delegate = @delegate;
            _getAllInventories = _delegate.GetType().GetMethod("GetAllInventories")!;
            CharacterId = (ulong)_delegate.GetType().GetProperty("CharacterId")!.GetValue(_delegate)!;
        }

        public ulong CharacterId { get; }

        public IEnumerable<InventoryItem> GetAllItems() =>
            ((IEnumerable)_getAllInventories.Invoke(_delegate, Array.Empty<object>())!)
            .Cast<IEnumerable>()
            .SelectMany(x => x.Cast<object>())
            .Select(x => new InventoryItem(x))
            .ToList();
    }

    public sealed class InventoryItem
    {
        private readonly object _delegate;

        public InventoryItem(object @delegate)
        {
            _delegate = @delegate;
            ItemId = (uint)_delegate.GetType().GetField("ItemId")!.GetValue(_delegate)!;
            Quantity = (uint)_delegate.GetType().GetField("Quantity")!.GetValue(_delegate)!;
        }

        public uint ItemId { get; }
        public uint Quantity { get; }
    }

    public sealed class InventoryWrapper
    {
        private readonly IEnumerable<InventoryItem> _items;

        public InventoryWrapper(IEnumerable<InventoryItem> items)
        {
            _items = items;
        }

        public long Sum(int itemId) => _items.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);
    }
}
