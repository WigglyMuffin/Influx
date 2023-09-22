using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons;
using Influx.AllaganTools;
using Influx.Influx;
using Influx.LocalStatistics;
using Influx.SubmarineTracker;
using Influx.Windows;

namespace Influx;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class InfluxPlugin : IDalamudPlugin
{
    public string Name => "Influx";

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly ClientState _clientState;
    private readonly CommandManager _commandManager;
    private readonly AllaganToolsIpc _allaganToolsIpc;
    private readonly SubmarineTrackerIpc _submarineTrackerIpc;
    private readonly LocalStatsCalculator _localStatsCalculator;
    private readonly InfluxStatisticsClient _influxStatisticsClient;
    private readonly WindowSystem _windowSystem;
    private readonly StatisticsWindow _statisticsWindow;
    private readonly ConfigurationWindow _configurationWindow;
    private readonly Timer _timer;

    public InfluxPlugin(DalamudPluginInterface pluginInterface, ClientState clientState,
        CommandManager commandManager, ChatGui chatGui, DataManager dataManager)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        _pluginInterface = pluginInterface;
        _configuration = LoadConfig();
        _clientState = clientState;
        _commandManager = commandManager;
        _allaganToolsIpc = new AllaganToolsIpc(pluginInterface, chatGui, _configuration);
        _submarineTrackerIpc = new SubmarineTrackerIpc(chatGui);
        _localStatsCalculator = new LocalStatsCalculator(pluginInterface, clientState, chatGui, dataManager);
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
        UpdateStatistics();
        _statisticsWindow.IsOpen = true;
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
                    .Where(x => _configuration.IncludedCharacters.Any(y => y.LocalContentId == x.Key.CharacterId || y.LocalContentId == x.Key.OwnerId))
                    .ToDictionary(x => x.Key, x => x.Value),
                Submarines = _submarineTrackerIpc.GetSubmarineStats(characters),
                LocalStats = _localStatsCalculator.GetAllCharacterStats()
                    .Where(x => characters.Any(y => y.CharacterId == x.Key))
                    .ToDictionary(x => characters.First(y => y.CharacterId == x.Key), x => x.Value)
                    .Where(x => _configuration.IncludedCharacters.Any(y => y.LocalContentId == x.Key.CharacterId || y.LocalContentId == x.Key.OwnerId))
                    .ToDictionary(x => x.Key, x => x.Value),
            };
            _statisticsWindow.OnStatisticsUpdate(update);
            _influxStatisticsClient.OnStatisticsUpdate(update);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "failed to update statistics");
        }
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= _configurationWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _timer.Dispose();
        _windowSystem.RemoveAllWindows();
        _commandManager.RemoveHandler("/influx");
        _influxStatisticsClient.Dispose();
        _localStatsCalculator.Dispose();
        _allaganToolsIpc.Dispose();

        ECommonsMain.Dispose();
    }
}
