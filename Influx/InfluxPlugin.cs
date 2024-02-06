using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using AutoRetainerAPI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Influx.AllaganTools;
using Influx.Influx;
using Influx.LocalStatistics;
using Influx.SubmarineTracker;
using Influx.Windows;
using LLib;
using Task = System.Threading.Tasks.Task;

namespace Influx;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class InfluxPlugin : IDalamudPlugin
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly IClientState _clientState;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog _pluginLog;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IGameGui _gameGui;
    private readonly AllaganToolsIpc _allaganToolsIpc;
    private readonly SubmarineTrackerIpc _submarineTrackerIpc;
    private readonly LocalStatsCalculator _localStatsCalculator;
    private readonly InfluxStatisticsClient _influxStatisticsClient;
    private readonly WindowSystem _windowSystem;
    private readonly StatisticsWindow _statisticsWindow;
    private readonly ConfigurationWindow _configurationWindow;
    private readonly Timer _timer;
    private readonly AutoRetainerApi _autoRetainerApi;

    private bool closeFcWindow = false;

    public InfluxPlugin(DalamudPluginInterface pluginInterface, IClientState clientState, IPluginLog pluginLog,
        ICommandManager commandManager, IChatGui chatGui, IDataManager dataManager, IFramework framework,
        IAddonLifecycle addonLifecycle, IGameGui gameGui)
    {
        _pluginInterface = pluginInterface;
        _configuration = LoadConfig();
        _clientState = clientState;
        _commandManager = commandManager;
        _pluginLog = pluginLog;
        _addonLifecycle = addonLifecycle;
        _gameGui = gameGui;
        DalamudReflector dalamudReflector = new DalamudReflector(pluginInterface, framework, pluginLog);
        _allaganToolsIpc = new AllaganToolsIpc(pluginInterface, chatGui, dalamudReflector, framework, _pluginLog);
        _submarineTrackerIpc = new SubmarineTrackerIpc(dalamudReflector);
        _localStatsCalculator = new LocalStatsCalculator(pluginInterface, clientState, addonLifecycle, pluginLog, dataManager);
        _influxStatisticsClient = new InfluxStatisticsClient(chatGui, _configuration, dataManager, clientState);

        _windowSystem = new WindowSystem(typeof(InfluxPlugin).FullName);
        _statisticsWindow = new StatisticsWindow();
        _windowSystem.AddWindow(_statisticsWindow);
        _configurationWindow = new ConfigurationWindow(_pluginInterface, clientState, _configuration);
        _windowSystem.AddWindow(_configurationWindow);

        _commandManager.AddHandler("/influx", new CommandInfo(ProcessCommand));
        _timer = new Timer(_ => UpdateStatistics(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenConfigUi += _configurationWindow.Toggle;

        ECommonsMain.Init(_pluginInterface, this);
        _autoRetainerApi = new();
        _autoRetainerApi.OnCharacterPostprocessStep += CheckCharacterPostProcess;
        _autoRetainerApi.OnCharacterReadyToPostProcess += DoCharacterPostProcess;
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup ,"FreeCompany", CloseFcWindow);
    }

    private Configuration LoadConfig()
    {
        if (_pluginInterface.GetPluginConfig() is Configuration config)
            return config;

        config = new Configuration();
        _pluginInterface.SavePluginConfig(config);
        return config;
    }

    private void ProcessCommand(string command, string arguments)
    {
        if (arguments == "c" || arguments == "config")
            _configurationWindow.Toggle();
        else
        {
            UpdateStatistics();
            _statisticsWindow.IsOpen = true;
        }
    }

    private void UpdateStatistics()
    {
        if (!_clientState.IsLoggedIn ||
            _configuration.IncludedCharacters.All(x => x.LocalContentId != _clientState.LocalContentId))
            return;

        try
        {
            var currencies = _allaganToolsIpc.CountCurrencies();
            var characters = currencies.Keys.ToList();
            if (characters.Count == 0)
                return;

            var update = new StatisticsUpdate
            {
                Currencies = currencies
                    .Where(x => _configuration.IncludedCharacters.Any(y =>
                        y.LocalContentId == x.Key.CharacterId ||
                        y.LocalContentId == x.Key.OwnerId ||
                        characters.Any(z => y.LocalContentId == z.CharacterId && z.FreeCompanyId == x.Key.CharacterId)))
                    .ToDictionary(x => x.Key, x => x.Value),
                Submarines = _submarineTrackerIpc.GetSubmarineStats(characters),
                LocalStats = _localStatsCalculator.GetAllCharacterStats()
                    .Where(x => characters.Any(y => y.CharacterId == x.Key))
                    .ToDictionary(x => characters.First(y => y.CharacterId == x.Key), x => x.Value)
                    .Where(x => _configuration.IncludedCharacters.Any(y =>
                        y.LocalContentId == x.Key.CharacterId ||
                        y.LocalContentId == x.Key.OwnerId ||
                        characters.Any(z => y.LocalContentId == z.CharacterId && z.FreeCompanyId == x.Key.CharacterId)))
                    .ToDictionary(x => x.Key, x => x.Value),
            };
            _statisticsWindow.OnStatisticsUpdate(update);
            _influxStatisticsClient.OnStatisticsUpdate(update);
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "failed to update statistics");
        }
    }

    private unsafe void CheckCharacterPostProcess()
    {

        var infoProxy = Framework.Instance()->UIModule->GetInfoModule()->GetInfoProxyById(InfoProxyId.FreeCompany);
        if (infoProxy != null)
        {
            var fcProxy = (InfoProxyFreeCompany*)infoProxy;
            if (fcProxy->ID != 0)
            {
                _pluginLog.Information($"Requesting post-process, FC is {fcProxy->ID}");
                _autoRetainerApi.RequestCharacterPostprocess();
            }
            else
                _pluginLog.Information("No FC id");
        }
        else
            _pluginLog.Information("No FreeCompany info proxy");
    }

    private void DoCharacterPostProcess()
    {
        closeFcWindow = true;
        Chat.Instance.SendMessage("/freecompanycmd");
    }

    private void CloseFcWindow(AddonEvent type, AddonArgs args)
    {
        if (closeFcWindow)
        {
            Task.Run(async () =>
            {
                // this runs every 500ms
                // https://github.com/Critical-Impact/CriticalCommonLib/blob/7b3814e703dd5b2981cd4334524b4b301c23e639/Services/InventoryScanner.cs#L436
                await Task.Delay(550);

                _pluginLog.Information("Closing FC window...");
                unsafe
                {
                    AtkUnitBase* addon = (AtkUnitBase*)_gameGui.GetAddonByName("FreeCompany");
                    if (addon->IsVisible)
                        addon->FireCallbackInt(-1);
                }

                closeFcWindow = false;
                _autoRetainerApi.FinishCharacterPostProcess();
            });
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup ,"FreeCompany", CloseFcWindow);
        _autoRetainerApi.OnCharacterPostprocessStep -= CheckCharacterPostProcess;
        _autoRetainerApi.OnCharacterReadyToPostProcess -= DoCharacterPostProcess;
        _autoRetainerApi.Dispose();
        ECommonsMain.Dispose();

        _pluginInterface.UiBuilder.OpenConfigUi -= _configurationWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _timer.Dispose();
        _windowSystem.RemoveAllWindows();
        _commandManager.RemoveHandler("/influx");
        _influxStatisticsClient.Dispose();
        _localStatsCalculator.Dispose();
        _allaganToolsIpc.Dispose();
    }
}
