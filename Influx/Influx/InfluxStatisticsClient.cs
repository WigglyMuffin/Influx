using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Influx.AllaganTools;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace Influx.Influx;

internal class InfluxStatisticsClient : IDisposable
{
    private const string MutexName = "Global\\c31c89b5-5efb-4c7e-bf87-21717a2814ef";

    private readonly InfluxDBClient _influxClient;
    private readonly ChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly Mutex _mutex;
    private readonly bool _mutexCreated;

    public InfluxStatisticsClient(ChatGui chatGui, Configuration configuration)
    {
        _influxClient = new InfluxDBClient(configuration.Server.Server, configuration.Server.Token);
        _chatGui = chatGui;
        _configuration = configuration;
        _mutex = new Mutex(true, MutexName, out _mutexCreated);
    }

    public bool Enabled => _configuration.Server.Enabled;

    public void OnStatisticsUpdate(StatisticsUpdate update)
    {
        if (!Enabled || !_mutexCreated)
            return;

        DateTime date = DateTime.UtcNow;
        IReadOnlyDictionary<Character, Currencies> currencyStats = update.Currencies;

        var validFcIds = currencyStats.Keys
            .Where(x => x.CharacterType == CharacterType.Character)
            .Select(x => x.FreeCompanyId)
            .ToList();
        Task.Run(async () =>
        {
            try
            {
                List<PointData> values = new();
                foreach (var (character, currencies) in currencyStats)
                {
                    if (character.CharacterType == CharacterType.Character)
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

                        if (update.LocalStats.TryGetValue(character, out var localStats))
                        {
                            values.Add(PointData.Measurement("grandcompany")
                                .Tag("id", character.CharacterId.ToString())
                                .Tag("player_name", character.Name)
                                .Tag("type", character.CharacterType.ToString())
                                .Field("gc", localStats.GrandCompany)
                                .Field("gc_rank", localStats.GcRank)
                                .Field("seals", (GrandCompany)localStats.GrandCompany switch
                                {
                                    GrandCompany.Maelstrom => currencies.GcSealsMaelstrom,
                                    GrandCompany.TwinAdder => currencies.GcSealsTwinAdders,
                                    GrandCompany.ImmortalFlames => currencies.GcSealsImmortalFlames,
                                    _ => 0,
                                })
                                .Field("seal_cap", localStats.GcRank switch
                                {
                                    1 => 10_000,
                                    2 => 15_000,
                                    3 => 20_000,
                                    4 => 25_000,
                                    5 => 30_000,
                                    6 => 35_000,
                                    7 => 40_000,
                                    8 => 45_000,
                                    9 => 50_000,
                                    10 => 80_000,
                                    11 => 90_000,
                                    _ => 0,
                                })
                                .Field("squadron_unlocked", localStats?.SquadronUnlocked == true ? 1 : 0)
                                .Timestamp(date, WritePrecision.S));
                        }
                    }
                    else if (character.CharacterType == CharacterType.Retainer)
                    {
                        var owner = currencyStats.Keys.First(x => x.CharacterId == character.OwnerId);
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
                    else if (character.CharacterType == CharacterType.FreeCompanyChest &&
                             validFcIds.Contains(character.CharacterId))
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

                foreach (var (fc, subs) in update.Submarines)
                {
                    if (validFcIds.Contains(fc.CharacterId))
                    {
                        foreach (var sub in subs)
                        {
                            values.Add(PointData.Measurement("submersibles")
                                .Tag("id", fc.CharacterId.ToString())
                                .Tag("fc_name", fc.Name)
                                .Tag("sub_id", $"{fc.CharacterId}_{sub.Id}")
                                .Tag("sub_name", sub.Name)
                                .Field("level", sub.Level)
                                .Timestamp(date, WritePrecision.S));
                        }
                    }
                }

                var writeApi = _influxClient.GetWriteApiAsync();
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
        _mutex.Dispose();
        _influxClient.Dispose();
    }
}
