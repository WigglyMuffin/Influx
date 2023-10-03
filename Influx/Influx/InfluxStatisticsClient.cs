using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Influx.AllaganTools;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Lumina.Excel.GeneratedSheets;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Influx.Influx;

internal sealed class InfluxStatisticsClient : IDisposable
{
    private readonly InfluxDBClient _influxClient;
    private readonly IChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly IClientState _clientState;
    private readonly IReadOnlyDictionary<byte, byte> _classJobToArrayIndex;
    private readonly IReadOnlyDictionary<byte, string> _classJobNames;
    private readonly Dictionary<sbyte, string> _expToJobs;

    public InfluxStatisticsClient(IChatGui chatGui, Configuration configuration, IDataManager dataManager,
        IClientState clientState)
    {
        _influxClient = new InfluxDBClient(configuration.Server.Server, configuration.Server.Token);
        _chatGui = chatGui;
        _configuration = configuration;
        _clientState = clientState;

        _classJobToArrayIndex = dataManager.GetExcelSheet<ClassJob>()!.Where(x => x.RowId > 0)
            .ToDictionary(x => (byte)x.RowId, x => (byte)x.ExpArrayIndex);
        _classJobNames = dataManager.GetExcelSheet<ClassJob>()!.Where(x => x.RowId > 0)
            .ToDictionary(x => (byte)x.RowId, x => x.Abbreviation.ToString());
        _expToJobs = dataManager.GetExcelSheet<ClassJob>()!.Where(x => x.RowId > 0)
            .Where(x => x.JobIndex > 0)
            .Where(x => x.Abbreviation.ToString() != "SMN")
            .ToDictionary(x => x.ExpArrayIndex, x => x.Abbreviation.ToString());
    }

    public bool Enabled => _configuration.Server.Enabled;

    public void OnStatisticsUpdate(StatisticsUpdate update)
    {
        if (!Enabled || _configuration.IncludedCharacters.All(x => x.LocalContentId != _clientState.LocalContentId))
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
                            .Tag("fc_id", character.FreeCompanyId > 0 ? character.FreeCompanyId.ToString() : null)
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
                                .Tag("fc_id", character.FreeCompanyId > 0 ? character.FreeCompanyId.ToString() : null)
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
                                .Field("squadron_unlocked", localStats.SquadronUnlocked ? 1 : 0)
                                .Timestamp(date, WritePrecision.S));

                            if (localStats.ClassJobLevels.Count > 0)
                            {
                                foreach (var (expIndex, abbreviation) in _expToJobs)
                                {
                                    var level = localStats.ClassJobLevels[expIndex];
                                    if (level > 0)
                                    {
                                        values.Add(PointData.Measurement("experience")
                                            .Tag("id", character.CharacterId.ToString())
                                            .Tag("player_name", character.Name)
                                            .Tag("type", character.CharacterType.ToString())
                                            .Tag("fc_id", character.FreeCompanyId > 0 ? character.FreeCompanyId.ToString() : null)
                                            .Tag("job", abbreviation)
                                            .Field("level", level)
                                            .Timestamp(date, WritePrecision.S));
                                    }
                                }
                            }

                            if (localStats.MsqCount != -1)
                            {
                                values.Add(PointData.Measurement("quests")
                                    .Tag("id", character.CharacterId.ToString())
                                    .Tag("player_name", character.Name)
                                    .Tag("msq_name", localStats.MsqName)
                                    .Tag("fc_id",
                                        character.FreeCompanyId > 0 ? character.FreeCompanyId.ToString() : null)
                                    .Field("msq_count", localStats.MsqCount)
                                    .Field("msq_genre", localStats.MsqGenre)
                                    .Timestamp(date, WritePrecision.S));
                            }
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

                        if (update.LocalStats.TryGetValue(owner, out var ownerStats) && character.ClassJob != 0)
                        {
                            values.Add(PointData.Measurement("retainer")
                                .Tag("id", character.CharacterId.ToString())
                                .Tag("player_name", owner.Name)
                                .Tag("type", character.CharacterType.ToString())
                                .Tag("retainer_name", character.Name)
                                .Tag("class", _classJobNames[character.ClassJob])
                                .Field("level", character.Level)
                                .Field("is_max_level", character.Level == ownerStats.MaxLevel ? 1 : 0)
                                .Field("can_reach_max_level",
                                    ownerStats.ClassJobLevels.Count > 0 &&
                                    ownerStats.ClassJobLevels[_classJobToArrayIndex[character.ClassJob]] ==
                                    ownerStats.MaxLevel
                                        ? 1
                                        : 0)
                                .Field("levels_before_cap",
                                    ownerStats.ClassJobLevels.Count > 0
                                        ? ownerStats.ClassJobLevels[_classJobToArrayIndex[character.ClassJob]] -
                                          character.Level
                                        : 0)
                                .Timestamp(date, WritePrecision.S));
                        }
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
        _influxClient.Dispose();
    }
}
