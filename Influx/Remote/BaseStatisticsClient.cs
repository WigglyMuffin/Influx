using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Influx.AllaganTools;
using Influx.LocalStatistics;
using GrandCompany = FFXIVClientStructs.FFXIV.Client.UI.Agent.GrandCompany;

namespace Influx.Remote;

internal abstract class BaseStatisticsClient : IDisposable
{
    private readonly IChatGui _chatGui;
    private readonly Configuration _configuration;
    private readonly GameData _gameData;
    private readonly IClientState _clientState;
    private readonly IPluginLog _pluginLog;

    protected BaseStatisticsClient(
        IChatGui chatGui,
        Configuration configuration,
        GameData gameData,
        IClientState clientState,
        IPluginLog pluginLog)
    {
        _chatGui = chatGui;
        _configuration = configuration;
        _gameData = gameData;
        _clientState = clientState;
        _pluginLog = pluginLog;
    }

    public abstract bool Enabled { get; }

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
        Task.Run(async () =>
        {
            try
            {
                List<StatisticsValues> values = new();
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
                            values.Add(StatisticsValues.Measurement("submersibles")
                                .Tag("id", fc.CharacterId.ToString(CultureInfo.InvariantCulture))
                                .Tag("world", _gameData.WorldNames[fc.WorldId])
                                .Tag("fc_name", fc.Name)
                                .Tag("sub_id", $"{fc.CharacterId}_{sub.Id}")
                                .Tag("sub_name", sub.Name)
                                .Tag("part_hull", sub.Hull)
                                .Tag("part_stern", sub.Stern)
                                .Tag("part_bow", sub.Bow)
                                .Tag("part_bridge", sub.Bridge)
                                .Tag("build", sub.Build)
                                .Field("enabled", sub.Enabled)
                                .Field("level", sub.Level)
                                .Field("predicted_level", sub.PredictedLevel)
                                .Field("state", (int)sub.State)
                                .Field("return_time", new DateTimeOffset(sub.ReturnTime).ToUnixTimeSeconds())
                                .Timestamp(date));
                        }
                    }
                }

                await SaveStatistics(values).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "Unable to update statistics");
                _chatGui.PrintError(e.Message);
            }
        });
    }

    protected abstract Task SaveStatistics(List<StatisticsValues> values);

    private IEnumerable<StatisticsValues> GenerateCharacterStats(Character character, Currencies currencies,
        StatisticsUpdate update, DateTime date)
    {
        update.LocalStats.TryGetValue(character, out LocalStats? localStats);

        bool includeFc = character.FreeCompanyId > 0 &&
                         _configuration.IncludedCharacters.Any(x =>
                             x.LocalContentId == character.CharacterId && x.IncludeFreeCompany);

        Func<string, StatisticsValues> value = s => StatisticsValues.Measurement(s)
            .Tag("id", character.CharacterId.ToString(CultureInfo.InvariantCulture))
            .Tag("player_name", character.Name)
            .Tag("world", _gameData.WorldNames[character.WorldId])
            .Tag("type", character.CharacterType.ToString())
            .Tag("fc_id", includeFc ? character.FreeCompanyId.ToString(CultureInfo.InvariantCulture) : null)
            .Timestamp(date);

        yield return value("currency")
            .Field("gil", localStats?.Gil ?? 0)
            .Field("mgp", localStats?.MGP ?? 0)
            .Field("ventures", currencies.Ventures)
            .Field("ceruleum_tanks", currencies.CeruleumTanks)
            .Field("repair_kits", currencies.RepairKits)
            .Field("free_inventory", currencies.FreeSlots);

        if (localStats != null)
        {
            yield return value("grandcompany")
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
                    1 => 10_000u,
                    2 => 15_000u,
                    3 => 20_000u,
                    4 => 25_000u,
                    5 => 30_000u,
                    6 => 35_000u,
                    7 => 40_000u,
                    8 => 45_000u,
                    9 => 50_000u,
                    10 => 80_000u,
                    11 => 90_000u,
                    _ => 0u,
                })
                .Field("squadron_unlocked", localStats.SquadronUnlocked);

            if (localStats.ClassJobLevels.Count > 0)
            {
                foreach (var (expIndex, job) in _gameData.ExpToJobs)
                {
                    // last update to this char was in 6.x, so we don't have PCT/VPR data
                    if (localStats.ClassJobLevels.Count <= expIndex)
                        continue;

                    var level = localStats.ClassJobLevels[expIndex];
                    if (level > 0)
                    {
                        yield return value("experience")
                            .Tag("job", job.Abbreviation)
                            .Tag("job_type", job.Type)
                            .Field("level", level);
                    }
                }
            }

            if (localStats.MsqCount != -1)
            {
                yield return value("quests")
                    .Tag("msq_name", localStats.MsqName)
                    .Field("msq_count", localStats.MsqCount)
                    .Field("msq_genre", localStats.MsqGenre);
            }
        }

        foreach (var inventoryPoint in GenerateInventoryStats(character.CharacterId, update, value))
            yield return inventoryPoint;
    }

    private IEnumerable<StatisticsValues> GenerateRetainerStats(Character character, Currencies currencies,
        StatisticsUpdate update, DateTime date)
    {
        var owner = update.Currencies.Keys.First(x => x.CharacterId == character.OwnerId);

        Func<string, StatisticsValues> value = s => StatisticsValues.Measurement(s)
            .Tag("id", character.CharacterId.ToString(CultureInfo.InvariantCulture))
            .Tag("player_name", owner.Name)
            .Tag("player_id", character.OwnerId.ToString(CultureInfo.InvariantCulture))
            .Tag("world", _gameData.WorldNames[character.WorldId])
            .Tag("type", character.CharacterType.ToString())
            .Tag("retainer_name", character.Name)
            .Timestamp(date);

        yield return value("currency")
            .Field("gil", currencies.Gil)
            .Field("ceruleum_tanks", currencies.CeruleumTanks)
            .Field("repair_kits", currencies.RepairKits);

        if (update.LocalStats.TryGetValue(owner, out var ownerStats) && character.ClassJob != 0)
        {
            yield return value("retainer")
                .Tag("class", _gameData.ClassJobNames[character.ClassJob])
                .Field("level", character.Level)
                .Field("is_max_level", character.Level == ownerStats.MaxLevel)
                .Field("can_reach_max_level",
                    ownerStats.ClassJobLevels.Count > 0 &&
                    ownerStats.ClassJobLevels[_gameData.ClassJobToArrayIndex[character.ClassJob]] ==
                    ownerStats.MaxLevel)
                .Field("levels_before_cap",
                    ownerStats.ClassJobLevels.Count > 0
                        ? ownerStats.ClassJobLevels[_gameData.ClassJobToArrayIndex[character.ClassJob]] -
                          character.Level
                        : 0);
        }


        foreach (var inventoryPoint in GenerateInventoryStats(character.CharacterId, update, value))
            yield return inventoryPoint;
    }

    private IEnumerable<StatisticsValues> GenerateInventoryStats(ulong localContentId, StatisticsUpdate update,
        Func<string, StatisticsValues> value)
    {
        foreach (var (filterName, items) in update.InventoryItems)
        {
            foreach (var item in items.Where(x => x.LocalContentId == localContentId)
                         .GroupBy(x => new { x.ItemId, x.IsHq }))
            {
                _gameData.Prices.TryGetValue(item.Key.ItemId, out GameData.PriceInfo priceInfo);

                bool priceHq = item.Key.IsHq || priceInfo.UiCategory == 58; // materia always uses HQ prices

                yield return value("items")
                    .Tag("filter_name", filterName)
                    .Tag("item_id", item.Key.ItemId.ToString(CultureInfo.InvariantCulture))
                    .Tag("item_name", priceInfo.Name)
                    .Tag("hq", (item.Key.IsHq ? 1 : 0).ToString(CultureInfo.InvariantCulture))
                    .Field("quantity", item.Sum(x => x.Quantity))
                    .Field("total_gil", item.Sum(x => x.Quantity) * (priceHq ? priceInfo.Hq : priceInfo.Normal));
            }
        }
    }

    private IEnumerable<StatisticsValues> GenerateFcStats(Character character, Currencies currencies, StatisticsUpdate update,
        DateTime date)
    {
        update.FcStats.TryGetValue(character.CharacterId, out FcStats? fcStats);

        Func<string, StatisticsValues> value = s => StatisticsValues.Measurement(s)
            .Tag("id", character.CharacterId.ToString(CultureInfo.InvariantCulture))
            .Tag("fc_name", character.Name)
            .Tag("world", _gameData.WorldNames[character.WorldId])
            .Tag("type", character.CharacterType.ToString())
            .Timestamp(date);

        yield return value("currency")
            .Field("gil", currencies.Gil)
            .Field("fccredit", fcStats?.FcCredits ?? 0)
            .Field("ceruleum_tanks", currencies.CeruleumTanks)
            .Field("repair_kits", currencies.RepairKits);

        foreach (var inventoryPoint in GenerateInventoryStats(character.CharacterId, update, value))
            yield return inventoryPoint;
    }

    public abstract Task<(bool Success, string Error)> TestConnection(CancellationToken cancellationToken);

    public abstract void Dispose();
}
