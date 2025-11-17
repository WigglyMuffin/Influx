using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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
    private IListService _lists;
    private bool _isConfigured;

    public event Action? OnInitialized;

    public AllaganToolsIpc(IDalamudPluginInterface pluginInterface, IChatGui chatGui, DalamudReflector dalamudReflector,
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
        _lists = new UnavailableListService(_pluginLog);

        _initialized.Subscribe(ConfigureIpc);

        try
        {
            ICallGateSubscriber<bool> isInitializedFunc =
                pluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            bool isInitialized = isInitializedFunc.InvokeFunc();
            if (isInitialized)
                ConfigureIpc(true);
        }
        catch (IpcNotReadyError)
        {
            _pluginLog.Information("Allagan Tools not ready yet, will initialize when it loads");
        }
    }

    private void ConfigureIpc(bool initialized)
    {
        if (_isConfigured)
            return;

        _pluginLog.Information("Configuring Allagan tools IPC");
        TryConfigureIpc(0);
    }

    private void TryConfigureIpc(int attempt)
    {
        if (_isConfigured)
            return;

        const int maxAttempts = 10;
        var delayMs = 100 + (attempt * 200); // Exponential backoff: 100ms, 300ms, 500ms, etc.

        _framework.RunOnTick(() =>
        {
            try
            {
                if (_dalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var it, false, true))
                {
                    var hostedPlugin = it.GetType().BaseType!;
                    var host = hostedPlugin.GetField("host", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(it)!;
                    var serviceProvider = host.GetType().GetProperty("Services")!.GetValue(host)!;
                    var getServiceMethod = serviceProvider.GetType().GetMethod("GetService")!;
                    object GetService(Type t) => getServiceMethod.Invoke(serviceProvider, [t])!;

                    var cclName = it.GetType().Assembly.GetReferencedAssemblies().First(aN => aN.Name == "CriticalCommonLib")!;

                    var ccl = AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName == cclName.FullName)!;

                    _characters =
                        new CharacterMonitor(GetService(ccl.GetType("CriticalCommonLib.Services.ICharacterMonitor")!));
                    _inventories = new InventoryMonitor(
                        GetService(ccl.GetType("CriticalCommonLib.Services.IInventoryMonitor")!));
                    _lists = new ListService(
                        GetService(it.GetType().Assembly.GetType("InventoryTools.Services.Interfaces.IListService")!),
                        GetService(it.GetType().Assembly.GetType("InventoryTools.Lists.ListFilterService")!));

                    _isConfigured = true;
                    _pluginLog.Information("Successfully configured Allagan Tools IPC");
                    OnInitialized?.Invoke();
                }
                else
                {
                    _pluginLog.Warning($"Reflection was unsuccessful (attempt {attempt + 1}/{maxAttempts})");
                    if (attempt < maxAttempts - 1)
                    {
                        TryConfigureIpc(attempt + 1);
                    }
                    else
                    {
                        _pluginLog.Warning("Failed to configure Allagan Tools IPC after maximum attempts");
                    }
                }
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, $"Could not initialize IPC (attempt {attempt + 1}/{maxAttempts})");
                if (attempt < maxAttempts - 1)
                {
                    TryConfigureIpc(attempt + 1);
                }
                else
                {
                    _pluginLog.Error("Failed to configure Allagan Tools IPC after maximum attempts");
                    _chatGui.PrintError($"Influx: Failed to connect to Allagan Tools after {maxAttempts} attempts");
                }
            }
        }, TimeSpan.FromMilliseconds(delayMs));
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

    public FilterResult? GetFilter(string keyOrName)
    {
        try
        {
            return _lists.GetFilterByKeyOrName(keyOrName);
        }
        catch (IpcError e)
        {
            _pluginLog.Error(e, $"Unable to retrieve filter items for filter '{keyOrName}'");
            return null;
        }
    }

    public Dictionary<Character, Currencies> CountCurrencies()
    {
        _pluginLog.Verbose($"Updating characters with {_characters.GetType()} and {_inventories.GetType()}");
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
                        FreeSlots = inv.FreeInventorySlots,
                    };
                });
    }

    public void Dispose()
    {
        _initialized.Unsubscribe(ConfigureIpc);
        _characters = new UnavailableCharacterMonitor(_pluginLog);
        _inventories = new UnavailableInventoryMonitor(_pluginLog);
        _lists = new UnavailableListService(_pluginLog);
        _isConfigured = false;
        OnInitialized = null;
    }

    private sealed class InventoryWrapper(IEnumerable<InventoryItem> items)
    {
        public long Sum(int itemId) => items.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);

        public int FreeInventorySlots => 140 - items.Count(x => x.Category == 1);
    }
}