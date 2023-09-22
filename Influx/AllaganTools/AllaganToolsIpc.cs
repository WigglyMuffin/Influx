using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.Reflection;
using ECommons.Schedulers;

namespace Influx.AllaganTools;

internal sealed class AllaganToolsIpc : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly ChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly ICallGateSubscriber<bool, bool>? _initalized;
    private readonly ICallGateSubscriber<bool>? _isInitialized;

    public ICharacterMonitor Characters { get; private set; } = new UnavailableCharacterMonitor();
    public IInventoryMonitor Inventories { get; private set; } = new UnavailableInventoryMonitor();

    public AllaganToolsIpc(DalamudPluginInterface pluginInterface, ChatGui chatGui, Configuration configuration)
    {
        _pluginInterface = pluginInterface;
        _chatGui = chatGui;
        _configuration = configuration;

        _initalized = _pluginInterface.GetIpcSubscriber<bool, bool>("AllaganTools.Initialized");
        _isInitialized = _pluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
        _initalized.Subscribe(ConfigureIpc);

        try
        {
            bool isInitialized = _isInitialized.InvokeFunc();
            if (isInitialized)
                ConfigureIpc(true);
        }
        catch (IpcNotReadyError e)
        {
            PluginLog.Error(e, "Not initializing ATools yet, ipc not ready");
        }
    }

    private void ConfigureIpc(bool initialized)
    {
        PluginLog.Information("Configuring Allagan tools IPC");
        _ = new TickScheduler(() =>
        {
            try
            {
                if (DalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var it, false, true))
                {
                    var pluginService = it.GetType().Assembly.GetType("InventoryTools.PluginService")!;

                    Characters = new CharacterMonitor(pluginService.GetProperty("CharacterMonitor")!.GetValue(null)!);
                    Inventories = new InventoryMonitor(
                        pluginService.GetProperty("InventoryMonitor")!.GetValue(null)!);
                }
                else
                {
                    PluginLog.Warning("Reflection was unsuccessful");
                }
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "Could not initialize IPC");
                _chatGui.PrintError(e.ToString());
            }
        }, 100);
    }

    public Dictionary<Character, Currencies> CountCurrencies()
    {
        PluginLog.Debug($"{Characters.GetType()}, {Inventories.GetType()}");
        var characters = Characters.All.ToDictionary(x => x.CharacterId, x => x);
        return Inventories.All
            .Where(x => characters.ContainsKey(x.Value.CharacterId))
            .ToDictionary(
                x => characters[x.Value.CharacterId],
                y =>
                {
                    var inv = new InventoryWrapper(y.Value.GetAllItems());
                    return new Currencies
                    {
                        Gil = inv.Sum(1),
                        GcSealsMaelstrom = inv.Sum(20),
                        GcSealsTwinAdders = inv.Sum(21),
                        GcSealsImmortalFlames = inv.Sum(22),
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
        Characters = new UnavailableCharacterMonitor();
        Inventories = new UnavailableInventoryMonitor();
    }

    private sealed class InventoryWrapper
    {
        private readonly IEnumerable<InventoryItem> _items;

        public InventoryWrapper(IEnumerable<InventoryItem> items)
        {
            _items = items;
        }

        public long Sum(int itemId) => _items.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);
    }
}
