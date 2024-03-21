using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using LLib;

namespace Influx.AllaganTools;

internal sealed class AllaganToolsIpc : IDisposable
{
    private readonly IChatGui _chatGui;
    private readonly DalamudReflector _dalamudReflector;
    private readonly IFramework _framework;
    private readonly IPluginLog _pluginLog;
    private readonly ICallGateSubscriber<bool, bool> _initialized;
    private readonly ICallGateSubscriber<Dictionary<string, string>> _getSearchFilters;

    private ICharacterMonitor _characters;
    private IInventoryMonitor _inventories;
    private IFilterService _filters;

    public AllaganToolsIpc(DalamudPluginInterface pluginInterface, IChatGui chatGui, DalamudReflector dalamudReflector,
        IFramework framework, IPluginLog pluginLog)
    {
        _chatGui = chatGui;
        _dalamudReflector = dalamudReflector;
        _framework = framework;
        _pluginLog = pluginLog;

        _initialized = pluginInterface.GetIpcSubscriber<bool, bool>("AllaganTools.Initialized");
        _getSearchFilters =
            pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("AllaganTools.GetSearchFilters");

        _characters = new UnavailableCharacterMonitor(_pluginLog);
        _inventories = new UnavailableInventoryMonitor(_pluginLog);
        _filters = new UnavailableFilterService(_pluginLog);

        _initialized.Subscribe(ConfigureIpc);

        try
        {
            ICallGateSubscriber<bool> isInitializedFunc =
                pluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            bool isInitialized = isInitializedFunc.InvokeFunc();
            if (isInitialized)
                ConfigureIpc(true);
        }
        catch (IpcNotReadyError e)
        {
            _pluginLog.Error(e, "Not initializing ATools yet, ipc not ready");
        }
    }

    private void ConfigureIpc(bool initialized)
    {
        _pluginLog.Information("Configuring Allagan tools IPC");
        _framework.RunOnTick(() =>
        {
            try
            {
                if (_dalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var it, false, true))
                {
                    var pluginService = it.GetType().Assembly.GetType("InventoryTools.PluginService")!;

                    _characters = new CharacterMonitor(pluginService.GetProperty("CharacterMonitor")!.GetValue(null)!);
                    _inventories = new InventoryMonitor(
                        pluginService.GetProperty("InventoryMonitor")!.GetValue(null)!);
                    _filters = new FilterService(pluginService.GetProperty("FilterService")!.GetValue(null)!);
                }
                else
                {
                    _pluginLog.Warning("Reflection was unsuccessful");
                }
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Could not initialize IPC");
                _chatGui.PrintError(e.ToString());
            }
        }, TimeSpan.FromMilliseconds(100));
    }

    public Dictionary<string, string> GetSearchFilters()
    {
        try
        {
            return _getSearchFilters.InvokeFunc();
        }
        catch (IpcError e)
        {
            _pluginLog.Error(e, "Unable to retrieve allagantools filters");
            return new Dictionary<string, string>();
        }
    }

    public Filter? GetFilter(string keyOrName)
    {
        try
        {
            return _filters.GetFilterByKeyOrName(keyOrName);
        }
        catch (IpcError e)
        {
            _pluginLog.Error(e, $"Unable to retrieve filter items for filter '{keyOrName}'");
            return null;
        }
    }

    public Dictionary<Character, Currencies> CountCurrencies()
    {
        _pluginLog.Debug($"{_characters.GetType()}, {_inventories.GetType()}");
        var characters = _characters.All.ToDictionary(x => x.CharacterId, x => x);
        return _inventories.All
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
                        Ventures = inv.Sum(21072),
                        CeruleumTanks = inv.Sum(10155),
                        RepairKits = inv.Sum(10373),
                    };
                });
    }

    public void Dispose()
    {
        _initialized.Unsubscribe(ConfigureIpc);
        _characters = new UnavailableCharacterMonitor(_pluginLog);
        _inventories = new UnavailableInventoryMonitor(_pluginLog);
        _filters = new UnavailableFilterService(_pluginLog);
    }

    private sealed class InventoryWrapper(IEnumerable<InventoryItem> items)
    {
        public long Sum(int itemId) => items.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);
    }
}
