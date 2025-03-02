using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Influx.AllaganTools;
using Influx.LocalStatistics;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Lumina.Excel.Sheets;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Influx.Influx;

internal sealed class InfluxStatisticsClient : IDisposable
{
    private InfluxDBClient? _influxClient;
    private readonly IChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly IClientState _clientState;
    private readonly IPluginLog _pluginLog;
    private readonly ReadOnlyDictionary<byte, byte> _classJobToArrayIndex;
    private readonly ReadOnlyDictionary<byte, string> _classJobNames;
    private readonly ReadOnlyDictionary<sbyte, ClassJobDetail> _expToJobs;
    private readonly ReadOnlyDictionary<uint, PriceInfo> _prices;
    private readonly ReadOnlyDictionary<uint, string> _worldNames;

    public InfluxStatisticsClient(IChatGui chatGui, Configuration configuration, IDataManager dataManager,
        IClientState clientState, IPluginLog pluginLog)
    {
        _chatGui = chatGui;
        _configuration = configuration;
        _clientState = clientState;
        _pluginLog = pluginLog;
        UpdateClient();

        _classJobToArrayIndex = dataManager.GetExcelSheet<ClassJob>().Where(x => x.RowId > 0)
            .ToDictionary(x => (byte)x.RowId, x => (byte)x.ExpArrayIndex)
            .AsReadOnly();
        _classJobNames = dataManager.GetExcelSheet<ClassJob>().Where(x => x.RowId > 0)
            .ToDictionary(x => (byte)x.RowId, x => x.Abbreviation.ToString())
            .AsReadOnly();
        _expToJobs = dataManager.GetExcelSheet<ClassJob>()
            .Where(x => x.RowId > 0 && !string.IsNullOrEmpty(x.Name.ToString()))
            .Where(x => x.JobIndex > 0 || x.DohDolJobIndex >= 0)
            .Where(x => x.Abbreviation.ToString() != "SMN")
            .ToDictionary(x => x.ExpArrayIndex,
                x => new ClassJobDetail(x.Abbreviation.ToString(), x.DohDolJobIndex >= 0))
            .AsReadOnly();
        _prices = dataManager.GetExcelSheet<Item>()
            .AsEnumerable()
            .ToDictionary(x => x.RowId, x => new PriceInfo
            {
                Name = x.Name.ToString(),
                Normal = x.PriceLow,
                UiCategory = x.ItemUICategory.RowId,
            })
            .AsReadOnly();
        _worldNames = dataManager.GetExcelSheet<World>()
            .Where(x => x.RowId > 0 && x.IsPublic)
            .ToDictionary(x => x.RowId, x => x.Name.ToString())
            .AsReadOnly();
    }

    public bool Enabled => _configuration.Server.Enabled &&
                           !string.IsNullOrEmpty(_configuration.Server.Server) &&
                           !string.IsNullOrEmpty(_configuration.Server.Token) &&
                           !string.IsNullOrEmpty(_configuration.Server.Organization) &&
                           !string.IsNullOrEmpty(_configuration.Server.Bucket);

    public void UpdateClient()
    {
        _influxClient?.Dispose();
        _influxClient = null;

        if (Enabled)
            _influxClient = new InfluxDBClient(_configuration.Server.Server, _configuration.Server.Token);
    }

    public void OnStatisticsUpdate(StatisticsUpdate update)
    {
        if (!Enabled || _configuration.IncludedCharacters.All(x => x.LocalContentId != _clientState.LocalContentId))
            return;

        DateTime date = DateTime.UtcNow;
        date = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);
        IReadOnlyDictionary<Character, Currencies> currencyStats = update.Currencies;

        var validFcIds = currencyStats.Keys
            .Where(x => x.CharacterType == CharacterType.Character)
            .Where(x => _configuration.IncludedCharacters
                .Any(config => config.LocalContentId == x.CharacterId && config.IncludeFreeCompany))
            .Select(x => x.FreeCompanyId)
            .ToList();
        var client = _influxClient;
        if (client == null)
            return;
        Task.Run(async () =>
        {
            try
            {
                List<PointData> values = new();
                foreach (var (character, currencies) in currencyStats)
                {
                    if (character.CharacterType == CharacterType.Character)
                    {
                        values.AddRange(GenerateCharacterStats(character, currencies, update, date));
                    }
                    else if (character.CharacterType == CharacterType.Retainer)
                    {
                        values.AddRange(GenerateRetainerStats(character, currencies, update, date));
                    }
                    else if (character.CharacterType == CharacterType.FreeCompanyChest &&
                             validFcIds.Contains(character.CharacterId))
                    {
                        values.AddRange(GenerateFcStats(character, currencies, update, date));
                    }
                }

                foreach (var (fc, subs) in update.Submarines)
                {
                    if (validFcIds.Contains(fc.CharacterId))
                    {
                        foreach (var sub in subs)
                        {
                            values.Add(PointData.Measurement("submersibles")
                                .Tag("id", fc.CharacterId.ToString(CultureInfo.InvariantCulture))
                                .Tag("world", _worldNames[fc.WorldId])
                                .Tag("fc_name", fc.Name)
                                .Tag("sub_id", $"{fc.CharacterId}_{sub.Id}")
                                .Tag("sub_name", sub.Name)
                                .Tag("part_hull", sub.Hull)
                                .Tag("part_stern", sub.Stern)
                                .Tag("part_bow", sub.Bow)
                                .Tag("part_bridge", sub.Bridge)
                                .Tag("build", sub.Build)
                                .Field("enabled", sub.Enabled ? 1 : 0)
                                .Field("level", sub.Level)
                                .Field("predicted_level", sub.PredictedLevel)
                                .Field("state", (int)sub.State)
                                .Field("return_time", new DateTimeOffset(sub.ReturnTime).ToUnixTimeSeconds())
                                .Timestamp(date, WritePrecision.S));
                        }
                    }
                }

                var writeApi = client.GetWriteApiAsync();
                await writeApi.WritePointsAsync(
                        values,
                        _configuration.Server.Bucket, _configuration.Server.Organization)
                    .ConfigureAwait(false);

                _pluginLog.Verbose($"Influx: Sent {values.Count} data points to server");
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Unable to update statistics");
                _chatGui.PrintError(e.Message);
            }
        });
    }

    private IEnumerable<PointData> GenerateCharacterStats(Character character, Currencies currencies,
        StatisticsUpdate update, DateTime date)
    {
        update.LocalStats.TryGetValue(character, out LocalStats? localStats);

        bool includeFc = character.FreeCompanyId > 0 &&
                         _configuration.IncludedCharacters.Any(x =>
                             x.LocalContentId == character.CharacterId && x.IncludeFreeCompany);

        Func<string, PointData> pointData = s => PointData.Measurement(s)
            .Tag("id", character.CharacterId.ToString(CultureInfo.InvariantCulture))
            .Tag("player_name", character.Name)
            .Tag("world", _worldNames[character.WorldId])
            .Tag("type", character.CharacterType.ToString())
            .Tag("fc_id", includeFc ? character.FreeCompanyId.ToString(CultureInfo.InvariantCulture) : null)
            .Timestamp(date, WritePrecision.S);

        yield return pointData("currency")
            .Field("gil", localStats?.Gil ?? 0)
            .Field("mgp", localStats?.MGP ?? 0)
            .Field("ventures", currencies.Ventures)
            .Field("ceruleum_tanks", currencies.CeruleumTanks)
            .Field("repair_kits", currencies.RepairKits)
            .Field("free_inventory", currencies.FreeSlots);

        if (localStats != null)
        {
            yield return pointData("grandcompany")
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
                .Field("squadron_unlocked", localStats.SquadronUnlocked ? 1 : 0);

            if (localStats.ClassJobLevels.Count > 0)
            {
                foreach (var (expIndex, job) in _expToJobs)
                {
                    // last update to this char was in 6.x, so we don't have PCT/VPR data
                    if (localStats.ClassJobLevels.Count <= expIndex)
                        continue;

                    var level = localStats.ClassJobLevels[expIndex];
                    if (level > 0)
                    {
                        yield return pointData("experience")
                            .Tag("job", job.Abbreviation)
                            .Tag("job_type", job.Type)
                            .Field("level", level);
                    }
                }
            }

            if (localStats.MsqCount != -1)
            {
                yield return pointData("quests")
                    .Tag("msq_name", localStats.MsqName)
                    .Field("msq_count", localStats.MsqCount)
                    .Field("msq_genre", localStats.MsqGenre);
            }
        }

        foreach (var inventoryPoint in GenerateInventoryStats(character.CharacterId, update, pointData))
            yield return inventoryPoint;
    }

    private IEnumerable<PointData> GenerateRetainerStats(Character character, Currencies currencies,
        StatisticsUpdate update, DateTime date)
    {
        var owner = update.Currencies.Keys.First(x => x.CharacterId == character.OwnerId);

        Func<string, PointData> pointData = s => PointData.Measurement(s)
            .Tag("id", character.CharacterId.ToString(CultureInfo.InvariantCulture))
            .Tag("player_name", owner.Name)
            .Tag("player_id", character.OwnerId.ToString(CultureInfo.InvariantCulture))
            .Tag("world", _worldNames[character.WorldId])
            .Tag("type", character.CharacterType.ToString())
            .Tag("retainer_name", character.Name)
            .Timestamp(date, WritePrecision.S);

        yield return pointData("currency")
            .Field("gil", currencies.Gil)
            .Field("ceruleum_tanks", currencies.CeruleumTanks)
            .Field("repair_kits", currencies.RepairKits);

        if (update.LocalStats.TryGetValue(owner, out var ownerStats) && character.ClassJob != 0)
        {
            yield return pointData("retainer")
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
                        : 0);
        }


        foreach (var inventoryPoint in GenerateInventoryStats(character.CharacterId, update, pointData))
            yield return inventoryPoint;
    }

    private IEnumerable<PointData> GenerateInventoryStats(ulong localContentId, StatisticsUpdate update,
        Func<string, PointData> pointData)
    {
        foreach (var (filterName, items) in update.InventoryItems)
        {
            foreach (var item in items.Where(x => x.LocalContentId == localContentId)
                         .GroupBy(x => new { x.ItemId, x.IsHq }))
            {
                _prices.TryGetValue(item.Key.ItemId, out PriceInfo priceInfo);

                bool priceHq = item.Key.IsHq || priceInfo.UiCategory == 58; // materia always uses HQ prices

                yield return pointData("items")
                    .Tag("filter_name", filterName)
                    .Tag("item_id", item.Key.ItemId.ToString(CultureInfo.InvariantCulture))
                    .Tag("item_name", priceInfo.Name)
                    .Tag("hq", (item.Key.IsHq ? 1 : 0).ToString(CultureInfo.InvariantCulture))
                    .Field("quantity", item.Sum(x => x.Quantity))
                    .Field("total_gil", item.Sum(x => x.Quantity) * (priceHq ? priceInfo.Hq : priceInfo.Normal));
            }
        }
    }

    private IEnumerable<PointData> GenerateFcStats(Character character, Currencies currencies, StatisticsUpdate update,
        DateTime date)
    {
        update.FcStats.TryGetValue(character.CharacterId, out FcStats? fcStats);

        Func<string, PointData> pointData = s => PointData.Measurement(s)
            .Tag("id", character.CharacterId.ToString(CultureInfo.InvariantCulture))
            .Tag("fc_name", character.Name)
            .Tag("world", _worldNames[character.WorldId])
            .Tag("type", character.CharacterType.ToString())
            .Timestamp(date, WritePrecision.S);

        yield return pointData("currency")
            .Field("gil", currencies.Gil)
            .Field("fccredit", fcStats?.FcCredits ?? 0)
            .Field("ceruleum_tanks", currencies.CeruleumTanks)
            .Field("repair_kits", currencies.RepairKits);

        foreach (var inventoryPoint in GenerateInventoryStats(character.CharacterId, update, pointData))
            yield return inventoryPoint;
    }

    public async Task<(bool Success, string Error)> TestConnection(CancellationToken cancellationToken)
    {
        string orgName = _configuration.Server.Organization;
        string bucketName = _configuration.Server.Bucket;
        if (_influxClient == null)
            return (false, "InfluxDB client is not initialized");

        try
        {
            bool ping = await _influxClient.PingAsync().ConfigureAwait(false);
            if (!ping)
                return (false, "Ping failed");
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Unable to connect to InfluxDB server");
            return (false, "Failed to ping InfluxDB server");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var buckets = await _influxClient.GetBucketsApi()
                .FindBucketsByOrgNameAsync(orgName, cancellationToken)
                .ConfigureAwait(false);

            if (buckets == null)
                return (false, "InfluxDB returned no buckets");

            if (buckets.Count == 0)
                return (true, "Could not check if bucket exists (the token might not have permissions to query buckets)");

            if (buckets.All(x => x.Name != bucketName))
                return (false, $"Bucket '{bucketName}' not found");
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Could not query buckets from InfluxDB");
            return (false, "Failed to load buckets from InfluxDB server");
        }

        return (true, string.Empty);
    }

    public void Dispose()
    {
        _influxClient?.Dispose();
    }

    private struct PriceInfo
    {
        public string Name { get; init; }
        public uint Normal { get; init; }
        public uint Hq => Normal + (uint)Math.Ceiling((decimal)Normal / 10);
        public uint UiCategory { get; set; }
    }

    private sealed record ClassJobDetail(string Abbreviation, bool IsNonCombat)
    {
        public string Type => IsNonCombat ? "doh_dol" : "combat";
    }
}
