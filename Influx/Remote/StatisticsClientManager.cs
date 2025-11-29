using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Influx.Remote.Clients;

namespace Influx.Remote;

internal sealed class StatisticsClientManager : IDisposable
{
    private readonly IChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly GameData _gameData;
    private readonly IClientState _clientState;
    private readonly IPlayerState _playerState;
    private readonly IPluginLog _pluginLog;

    private BaseStatisticsClient? _statisticsClient;

    public StatisticsClientManager(
        IChatGui chatGui,
        Configuration configuration,
        IDataManager dataManager,
        IClientState clientState,
        IPlayerState playerState,
        IPluginLog pluginLog)
    {
        _chatGui = chatGui;
        _configuration = configuration;
        _gameData = new GameData(dataManager);
        _clientState = clientState;
        _playerState = playerState;
        _pluginLog = pluginLog;

        UpdateClient();
    }

    public void OnStatisticsUpdate(StatisticsUpdate update) => _statisticsClient?.OnStatisticsUpdate(update);


    public async Task<(bool Success, string Error)> TestConnection(CancellationToken cancellationToken)
    {
        if (_statisticsClient is { } client)
            return await client.TestConnection(cancellationToken).ConfigureAwait(false);
        else
            return (false, "Client is not configured.");
    }

    public void UpdateClient()
    {
        _statisticsClient?.Dispose();
        _statisticsClient = null;

        BaseStatisticsClient? newClient = _configuration.Server.Type switch
        {
            Configuration.ERemoteType.InfluxDb => new InfluxDbStatisticsClient(_chatGui, _configuration, _gameData,
                _clientState, _playerState, _pluginLog),
            Configuration.ERemoteType.QuestDb => new QuestDbStatisticsClient(_chatGui, _configuration, _gameData,
                _clientState, _playerState, _pluginLog),
            _ => null
        };

        if (newClient is { Enabled: false })
        {
            newClient.Dispose();
            return;
        }

        _statisticsClient = newClient;
    }

    public void Dispose()
    {
        _statisticsClient?.Dispose();
        _statisticsClient = null;
    }
}