using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Timers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using Influx.AllaganTools;
using Influx.Influx;
using Influx.LocalStatistics;
using Influx.SubmarineTracker;
using Influx.Windows;
using LLib;

namespace Influx;

[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("Performance", "CA1812")]
internal sealed class InfluxPlugin : IDalamudPlugin
{
    private readonly object _lock = new();
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly IClientState _clientState;
    private readonly ICommandManager _commandManager;
    private readonly ICondition _condition;
    private readonly IPluginLog _pluginLog;
    private readonly AllaganToolsIpc _allaganToolsIpc;
    private readonly SubmarineTrackerIpc _submarineTrackerIpc;
    private readonly LocalStatsCalculator _localStatsCalculator;
    private readonly FcStatsCalculator _fcStatsCalculator;
    private readonly InfluxStatisticsClient _influxStatisticsClient;
    private readonly WindowSystem _windowSystem;
    private readonly StatisticsWindow _statisticsWindow;
    private readonly ConfigurationWindow _configurationWindow;
    private readonly Timer _timer;

    public InfluxPlugin(IDalamudPluginInterface pluginInterface, IClientState clientState, IPluginLog pluginLog,
        ICommandManager commandManager, IChatGui chatGui, IDataManager dataManager, IFramework framework,
        IAddonLifecycle addonLifecycle, IGameGui gameGui, ICondition condition)
    {
        _pluginInterface = pluginInterface;
        _configuration = LoadConfig();
        _clientState = clientState;
        _commandManager = commandManager;
        _condition = condition;
        _pluginLog = pluginLog;
        DalamudReflector dalamudReflector = new DalamudReflector(pluginInterface, framework, pluginLog);
        _allaganToolsIpc = new AllaganToolsIpc(pluginInterface, chatGui, dalamudReflector, framework, _pluginLog);
        _submarineTrackerIpc = new SubmarineTrackerIpc(dalamudReflector);
        _localStatsCalculator =
            new LocalStatsCalculator(pluginInterface, clientState, addonLifecycle, pluginLog, dataManager);
        _fcStatsCalculator = new FcStatsCalculator(this, pluginInterface, clientState, addonLifecycle, gameGui,
            framework, _configuration, pluginLog);
        _influxStatisticsClient =
            new InfluxStatisticsClient(chatGui, _configuration, dataManager, clientState, _pluginLog);

        _windowSystem = new WindowSystem(typeof(InfluxPlugin).FullName);
        _statisticsWindow = new StatisticsWindow();
        _windowSystem.AddWindow(_statisticsWindow);
        _configurationWindow = new ConfigurationWindow(_pluginInterface, clientState, _configuration, _allaganToolsIpc);
        _configurationWindow.ConfigUpdated += (_, _) => _influxStatisticsClient.UpdateClient();
        _windowSystem.AddWindow(_configurationWindow);

        _commandManager.AddHandler("/influx", new CommandInfo(ProcessCommand)
        {
            HelpMessage = "Opens influx configuration"
        });

        _commandManager.AddHandler("/influx gil", new CommandInfo(ProcessCommand)
        {
            HelpMessage = "Opens influx statistics"
        });

        _timer = new Timer(TimeSpan.FromMinutes(1));
        _timer.Elapsed += (_, _) => UpdateStatistics();
        _timer.AutoReset = true;
        _timer.Enabled = true;

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenConfigUi += _configurationWindow.Toggle;
        _pluginInterface.UiBuilder.OpenMainUi += _statisticsWindow.Toggle;
        _condition.ConditionChange += UpdateOnLogout;
        _clientState.Login += AutoEnrollCharacter;
        
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
        if (arguments == "gil")
        {
            UpdateStatistics();
            _statisticsWindow.IsOpen = true;
        }
        else
            _configurationWindow.Toggle();
    }

    private void UpdateStatistics()
    {
        lock (_lock)
        {
            if (!_clientState.IsLoggedIn ||
                _configuration.IncludedCharacters.All(x => x.LocalContentId != _clientState.LocalContentId))
            {
                _pluginLog.Verbose("Influx: not logged in or not enabled for this character");
                return;
            }

            try
            {
                var currencies = _allaganToolsIpc.CountCurrencies();
                var characters = currencies.Keys.ToList();
                if (characters.Count == 0)
                {
                    _pluginLog.Warning("Found 0 AllaganTools characters");
                    return;
                }

                foreach (Character character in characters)
                {
                    if (character.CharacterType == CharacterType.Character && character.FreeCompanyId != default)
                    {
                        bool isFcEnabled = _configuration.IncludedCharacters
                            .FirstOrDefault(x => x.LocalContentId == character.CharacterId)?.IncludeFreeCompany ?? true;
                        if (!isFcEnabled)
                            character.FreeCompanyId = default;
                    }
                }

                Dictionary<string, IReadOnlyList<SortingResult>> inventoryItems =
                    _configuration.IncludedInventoryFilters.Select(c => c.Name)
                        .Distinct()
                        .ToDictionary(c => c, c =>
                        {
                            var filter = _allaganToolsIpc.GetFilter(c);
                            if (filter == null)
                                return new List<SortingResult>();

                            return filter.GenerateFilteredList();
                        });

                var update = new StatisticsUpdate
                {
                    Currencies = currencies
                        .Where(x => _configuration.IncludedCharacters.Any(y =>
                            y.LocalContentId == x.Key.CharacterId ||
                            y.LocalContentId == x.Key.OwnerId ||
                            characters.Any(z =>
                                y.LocalContentId == z.CharacterId && z.FreeCompanyId == x.Key.CharacterId)))
                        .ToDictionary(x => x.Key, x => x.Value),
                    InventoryItems = inventoryItems,
                    Submarines = UpdateEnabledSubs(_submarineTrackerIpc.GetSubmarineStats(characters), characters),
                    LocalStats = _localStatsCalculator.GetAllCharacterStats()
                        .Where(x => characters.Any(y => y.CharacterId == x.Key))
                        .ToDictionary(x => characters.First(y => y.CharacterId == x.Key), x => x.Value)
                        .Where(x => _configuration.IncludedCharacters.Any(y =>
                            y.LocalContentId == x.Key.CharacterId ||
                            y.LocalContentId == x.Key.OwnerId ||
                            characters.Any(z =>
                                y.LocalContentId == z.CharacterId && z.FreeCompanyId == x.Key.CharacterId)))
                        .ToDictionary(x => x.Key, x => x.Value),
                    FcStats = _fcStatsCalculator.GetAllFcStats()
                        .Where(x => characters.Any(y => y.FreeCompanyId == x.Key))
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
    }

    private IReadOnlyDictionary<Character, List<SubmarineStats>> UpdateEnabledSubs(
        IReadOnlyDictionary<Character, List<SubmarineStats>> allSubs, List<Character> characters)
    {
        foreach (var (character, subs) in allSubs)
        {
            var owner = characters.FirstOrDefault(x => x.FreeCompanyId == character.CharacterId);
            if (owner == null)
                continue;

            var enabledSubs = _fcStatsCalculator.GetEnabledSubs(owner.CharacterId);
            foreach (var sub in subs)
                sub.Enabled = enabledSubs.Contains(sub.Name);
        }


        return allSubs;
    }


    private void AutoEnrollCharacter()
    {
        if (_configuration.AutoEnrollCharacters)
        {
            _configuration.IncludedCharacters.Add(new Configuration.CharacterInfo
            {
                LocalContentId = _clientState.LocalContentId,
                CachedPlayerName = _clientState.LocalPlayer?.Name.ToString() ?? "??",
                CachedWorldName = _clientState.LocalPlayer?.HomeWorld.Value.Name.ToString(),
            });
            _pluginInterface.SavePluginConfig(_configuration);
        }
    }


    private void UpdateOnLogout(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.LoggingOut && value)
        {
            try
            {
                _timer.Enabled = false;
                _localStatsCalculator.UpdateStatisticsLogout();
                UpdateStatistics();
            }
            finally
            {
                _timer.Enabled = true;
            }
        }
    }

    public void Dispose()
    {
        _condition.ConditionChange -= UpdateOnLogout;
        _pluginInterface.UiBuilder.OpenConfigUi -= _configurationWindow.Toggle;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _timer.Stop();
        _timer.Dispose();
        _windowSystem.RemoveAllWindows();
        _commandManager.RemoveHandler("/influx");
        _commandManager.RemoveHandler("/influx gil");
        _influxStatisticsClient.Dispose();
        _fcStatsCalculator.Dispose();
        _localStatsCalculator.Dispose();
        _allaganToolsIpc.Dispose();
    }
}
