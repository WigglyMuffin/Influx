using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

namespace Influx.LocalStatistics;

internal sealed class LocalStatsCalculator : IDisposable
{
    private const uint ComingToGridania = 65575;
    private const uint ComingToLimsa = 65643;
    private const uint ComingToUldah = 66130;

    private const uint EnvoyGridania = 66043;
    private const uint EnvoyLimsa = 66082;
    private const uint EnvoyUldah = 66064;

    private const uint JointQuest = 65781;

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IPluginLog _pluginLog;
    private readonly Dictionary<ulong, LocalStats> _cache = new();

    private IReadOnlyList<QuestInfo>? _gridaniaStart;
    private IReadOnlyList<QuestInfo>? _limsaStart;
    private IReadOnlyList<QuestInfo>? _uldahStart;
    private IReadOnlyList<QuestInfo>? _msqQuests;


    public LocalStatsCalculator(
        DalamudPluginInterface pluginInterface,
        IClientState clientState,
        IAddonLifecycle addonLifecycle,
        IPluginLog pluginLog,
        IDataManager dataManager)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _addonLifecycle = addonLifecycle;
        _pluginLog = pluginLog;

        _clientState.Login += UpdateStatistics;
        _clientState.TerritoryChanged += UpdateStatistics;
        _addonLifecycle.RegisterListener(AddonEvent.PreSetup, "JournalAccept", UpdateStatistics);

        Task.Run(() =>
        {
            List<QuestInfo> msq = new();
            foreach (var quest in dataManager.GetExcelSheet<Quest>()!.Where(x => x.JournalGenre.Row is >= 1 and <= 12))
            {
                var previousQuests = quest.PreviousQuest?.Select(x => x.Row).Where(x => x != 0).ToList();
                if (previousQuests != null && quest.Unknown12 != 0)
                    previousQuests.Add(quest.Unknown12);

                msq.Add(new QuestInfo
                {
                    RowId = quest.RowId,
                    Name = quest.Name.ToString(),
                    PreviousQuestIds = previousQuests ?? new(),
                    Genre = quest.JournalGenre.Row,
                });
            }

            _gridaniaStart = PopulateStartingCities(msq, EnvoyGridania, ComingToGridania, false);
            _limsaStart = PopulateStartingCities(msq, EnvoyLimsa, ComingToLimsa, true);
            _uldahStart = PopulateStartingCities(msq, EnvoyUldah, ComingToUldah, true);

            List<QuestInfo> sortedQuests = new();
            sortedQuests.Add(msq.First(x => x.RowId == JointQuest));
            msq.Remove(sortedQuests[0]);

            while (msq.FirstOrDefault(quest => quest.PreviousQuestIds.Count == 0 ||
                                               quest.PreviousQuestIds.All(x => sortedQuests.Any(y => x == y.RowId))) is
                   { } qq)
            {
                sortedQuests.Add(qq);
                msq.Remove(qq);
            }

            _msqQuests = sortedQuests.AsReadOnly();
        });

        foreach (var file in _pluginInterface.ConfigDirectory.GetFiles("l.*.json"))
        {
            try
            {
                var stats = JsonConvert.DeserializeObject<LocalStats>(File.ReadAllText(file.FullName));
                if (stats == null)
                    continue;

                _cache[stats.ContentId] = stats;
            }
            catch (Exception e)
            {
                _pluginLog.Warning(e, $"Could not parse file {file.FullName}");
            }
        }

        if (_clientState.IsLoggedIn)
            UpdateStatistics();
    }

    private IReadOnlyList<QuestInfo> PopulateStartingCities(List<QuestInfo> quests, uint envoyQuestId,
        uint startingQuestId, bool popCallOfTheSea)
    {
        QuestInfo callOfTheSea = quests.First(x => x.PreviousQuestIds.Contains(envoyQuestId));
        if (popCallOfTheSea)
            quests.Remove(callOfTheSea);

        List<QuestInfo> startingCityQuests = new List<QuestInfo> { callOfTheSea };
        uint? questId = envoyQuestId;
        QuestInfo? quest;

        do
        {
            quest = quests.First(x => x.RowId == questId);
            quests.Remove(quest);

            if (quest.Name == "Close to Home")
            {
                quest = new QuestInfo
                {
                    RowId = startingQuestId,
                    Name = "Coming to ...",
                    PreviousQuestIds = new(),
                    Genre = quest.Genre,
                };
            }

            startingCityQuests.Add(quest);
            questId = quest.PreviousQuestIds.FirstOrDefault();
        } while (questId != null && questId != 0);

        return Enumerable.Reverse(startingCityQuests).ToList().AsReadOnly();
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PreSetup, "JournalAccept", UpdateStatistics);
        _clientState.Login -= UpdateStatistics;
    }

    private void UpdateStatistics(ushort territoryType) => UpdateStatistics();

    public void UpdateStatisticsLogout() => UpdateStatistics();

    private void UpdateStatistics(AddonEvent type, AddonArgs args) => UpdateStatistics();

    private unsafe void UpdateStatistics()
    {
        var localContentId = _clientState.LocalContentId;
        if (localContentId == 0)
        {
            _pluginLog.Warning("No local character id");
            return;
        }

        _pluginLog.Information($"Updating character {_clientState.LocalContentId}");
        try
        {
            PlayerState* playerState = PlayerState.Instance();
            if (playerState == null)
                return;

            LocalStats localStats = new()
            {
                ContentId = localContentId,
                GrandCompany = playerState->GrandCompany,
                GcRank = playerState->GetGrandCompanyRank(),
                SquadronUnlocked = playerState->GetGrandCompanyRank() >= 9 && (QuestManager.IsQuestComplete(67925) ||
                                                                               QuestManager.IsQuestComplete(67926) ||
                                                                               QuestManager.IsQuestComplete(67927)),
                MaxLevel = playerState->MaxLevel,
                ClassJobLevels = ExtractClassJobLevels(playerState),
                StartingTown = playerState->StartTown,
                Gil = InventoryManager.Instance()->GetInventoryItemCount(1),
                MGP = InventoryManager.Instance()->GetInventoryItemCount(29),
            };

            if (_msqQuests != null)
            {
                if (QuestManager.IsQuestComplete(JointQuest))
                {
                    var quests = _msqQuests.Where(x => QuestManager.IsQuestComplete(x.RowId)).ToList();
                    localStats.MsqCount = 24 + quests.Count;
                    localStats.MsqName = quests.Last().Name;
                    localStats.MsqGenre = quests.Last().Genre;
                }
                else
                {
                    //_pluginLog.Information($"XX → {playerState->StartTown}");
                    IReadOnlyList<QuestInfo> cityQuests = playerState->StartTown switch
                    {
                        1 => _limsaStart!,
                        2 => _gridaniaStart!,
                        3 => _uldahStart!,
                        _ => new List<QuestInfo>(),
                    };
                    var quests = cityQuests.Where(x => QuestManager.IsQuestComplete(x.RowId)).ToList();
                    localStats.MsqCount = quests.Count;
                    localStats.MsqName = quests.LastOrDefault()?.Name ?? string.Empty;
                    localStats.MsqGenre = quests.LastOrDefault()?.Genre ?? 0;
                }
            }
            else
            {
                localStats.MsqCount = -1;
                localStats.MsqName = string.Empty;
                localStats.MsqGenre = 0;
            }

            _pluginLog.Verbose($"Current MSQ Progress: {localStats.MsqCount}, {localStats.MsqName}");

            if (_cache.TryGetValue(localContentId, out var existingStats))
            {
                if (existingStats != localStats)
                {
                    _cache[localContentId] = localStats;
                    File.WriteAllText(
                        Path.Join(_pluginInterface.GetPluginConfigDirectory(), $"l.{localContentId:X8}.json"),
                        JsonConvert.SerializeObject(localStats));
                }
            }
            else
            {
                _cache[localContentId] = localStats;
                File.WriteAllText(
                    Path.Join(_pluginInterface.GetPluginConfigDirectory(), $"l.{localContentId:X8}.json"),
                    JsonConvert.SerializeObject(localStats));
            }
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Failed to update local stats");
        }
    }

    private unsafe List<short> ExtractClassJobLevels(PlayerState* playerState)
    {
        List<short> levels = new();
        for (int i = 0; i < 30; ++i)
            levels.Add(playerState->ClassJobLevelArray[i]);
        return levels;
    }

    public IReadOnlyDictionary<ulong, LocalStats> GetAllCharacterStats() => _cache.AsReadOnly();
}
