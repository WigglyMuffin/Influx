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
        _allaganToolsIpc = new AllaganToolsIPC(pluginInterface, chatGui, _configuration);
        _influxClient = new InfluxDBClient(_configuration.Server.Server, _configuration.Server.Token);

        _commandManager.AddHandler("/influx", new CommandInfo(ProcessCommand));
        _timer = new Timer(_ => CountGil(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
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
        CountGil(true);
    }

    private void CountGil(bool printDebug = false)
    {
        if (!_clientState.IsLoggedIn)
            return;

        DateTime date = DateTime.UtcNow;
        IReadOnlyDictionary<AllaganToolsIPC.Character, AllaganToolsIPC.Currencies> stats;
        try
        {
            stats = _allaganToolsIpc.CountCurrencies();
        }
        catch (Exception e)
        {
            _chatGui.PrintError(e.ToString());
            return;
        }


        var validFcIds = stats.Keys
            .Where(x => x.CharacterType == AllaganToolsIPC.CharacterType.Character)
            .Select(x => x.FreeCompanyId)
            .ToList();
        Task.Run(async () =>
        {

            try
            {
                List<PointData> values = new();
                foreach (var (character, currencies) in stats)
                {
                    if (character.CharacterType == AllaganToolsIPC.CharacterType.Character)
                    {
                        values.Add(PointData.Measurement("currency")
                            .Tag("id", character.CharacterId.ToString())
                            .Tag("player_name", character.Name)
                            .Tag("type", character.CharacterType.ToString())
                            .Field("gil", currencies.Gil)
                            .Field("ventures", currencies.Ventures)
                            .Field("ceruleum_tanks", currencies.CeruleumTanks)
                            .Field("repair_kits", currencies.RepairKits)
                            .Timestamp(date, WritePrecision.S));
                    }
                    else if (character.CharacterType == AllaganToolsIPC.CharacterType.Retainer)
                    {
                        var owner = stats.Keys.First(x => x.CharacterId == character.OwnerId);
                        values.Add(PointData.Measurement("currency")
                            .Tag("id", character.CharacterId.ToString())
                            .Tag("player_name", owner.Name)
                            .Tag("type", character.CharacterType.ToString())
                            .Tag("retainer_name", character.Name)
                            .Field("gil", currencies.Gil)
                            .Field("ceruleum_tanks", currencies.CeruleumTanks)
                            .Field("repair_kits", currencies.RepairKits)
                            .Timestamp(date, WritePrecision.S));
                    }
                    else if (character.CharacterType == AllaganToolsIPC.CharacterType.FreeCompanyChest && validFcIds.Contains(character.CharacterId))
                    {
                        values.Add(PointData.Measurement("currency")
                            .Tag("id", character.CharacterId.ToString())
                            .Tag("fc_name", character.Name)
                            .Tag("type", character.CharacterType.ToString())
                            .Field("gil", currencies.Gil)
                            .Field("fccredit", currencies.FcCredits)
                            .Field("ceruleum_tanks", currencies.CeruleumTanks)
                            .Field("repair_kits", currencies.RepairKits)
                            .Timestamp(date, WritePrecision.S));
                    }
                }

                var writeApi = _influxClient.GetWriteApiAsync();
                await writeApi.WritePointsAsync(
                    values,
                    _configuration.Server.Bucket, _configuration.Server.Organization);

                if (printDebug)
                    _chatGui.Print($"Influx: {values.Count} points");
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
