using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using ECommons;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace Influx;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class InfluxPlugin : IDalamudPlugin
{
    public string Name => "Influx";

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly ClientState _clientState;
    private readonly CommandManager _commandManager;
    private readonly ChatGui _chatGui;
    private readonly AllaganToolsIPC _allaganToolsIpc;
    private readonly InfluxDBClient _influxClient;
    private readonly Timer _timer;

    public InfluxPlugin(DalamudPluginInterface pluginInterface, ClientState clientState,
        CommandManager commandManager, ChatGui chatGui)
    {
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        _pluginInterface = pluginInterface;
        _configuration = LoadConfig();
        _clientState = clientState;
        _commandManager = commandManager;
        _chatGui = chatGui;
        _allaganToolsIpc = new AllaganToolsIPC(pluginInterface, chatGui, clientState);
        _influxClient = new InfluxDBClient(_configuration.Server.Server, _configuration.Server.Token);

        _commandManager.AddHandler("/influx", new CommandInfo(ProcessCommand));
        _timer = new Timer(_ => CountGil(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private Configuration LoadConfig()
    {
        var config = _pluginInterface.GetPluginConfig() as Configuration;
        if (config != null)
            return config;

        config = new Configuration();
        _pluginInterface.SavePluginConfig(config);
        return config;
    }

    private void ProcessCommand(string command, string arguments)
    {
        CountGil();
    }

    private void CountGil()
    {
        if (!_clientState.IsLoggedIn)
            return;

        DateTime date = DateTime.UtcNow;
        var stats = _allaganToolsIpc.CountGil();

        Task.Run(async () =>
        {
            List<PointData> values = new();
            foreach (var (character, gil) in stats)
            {
                if (character.CharacterType == AllaganToolsIPC.CharacterType.Character)
                {
                    values.Add(PointData.Measurement("currency")
                        .Tag("player_name", character.Name)
                        .Tag("type", character.CharacterType.ToString())
                        .Field("gil", gil)
                        .Timestamp(date, WritePrecision.S));
                }
                else if (character.CharacterType == AllaganToolsIPC.CharacterType.Retainer)
                {
                    var owner = stats.Keys.First(x => x.CharacterId == character.OwnerId);
                    values.Add(PointData.Measurement("currency")
                        .Tag("player_name", owner.Name)
                        .Tag("type", character.CharacterType.ToString())
                        .Tag("retainer_name", character.Name)
                        .Field("gil", gil)
                        .Timestamp(date, WritePrecision.S));
                }
            }

            var writeApi = _influxClient.GetWriteApiAsync();
            try
            {
                await writeApi.WritePointsAsync(
                    values,
                    _configuration.Server.Bucket, _configuration.Server.Organization);
                //_chatGui.Print($"Influx: {values.Count} points");
            }
            catch (Exception e)
            {
                _chatGui.PrintError(e.ToString());
            }
        });
    }

    public void Dispose()
    {
        _timer.Dispose();
        _commandManager.RemoveHandler("/influx");
        _influxClient.Dispose();
        _allaganToolsIpc.Dispose();

        ECommonsMain.Dispose();
    }
}
