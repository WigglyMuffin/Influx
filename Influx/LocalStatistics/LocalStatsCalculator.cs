using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Newtonsoft.Json;

namespace Influx.LocalStatistics;

public class LocalStatsCalculator : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly ClientState _clientState;
    private readonly ChatGui _chatGui;
    private readonly Dictionary<ulong, LocalStats> _cache = new();

    public LocalStatsCalculator(
        DalamudPluginInterface pluginInterface,
        ClientState clientState,
        ChatGui chatGui)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _chatGui = chatGui;

        _clientState.Login += UpdateStatistics;
        _clientState.Logout += UpdateStatistics;
        _clientState.TerritoryChanged += UpdateStatistics;

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
                PluginLog.Warning(e, $"Could not parse file {file.FullName}");
            }
        }

        if (_clientState.IsLoggedIn)
            UpdateStatistics();
    }

    public void Dispose()
    {
        _clientState.Login -= UpdateStatistics;
        _clientState.Logout -= UpdateStatistics;
        _clientState.TerritoryChanged -= UpdateStatistics;
    }

    private void UpdateStatistics(object? sender, EventArgs e) => UpdateStatistics();

    private void UpdateStatistics(object? sender, ushort territoryType) => UpdateStatistics();

    private unsafe void UpdateStatistics()
    {
        var localContentId = _clientState.LocalContentId;
        if (localContentId == 0)
        {
            PluginLog.Warning("No local character id");
            return;
        }

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
                SquadronUnlocked = (GrandCompany)playerState->GrandCompany switch
                {
                    GrandCompany.Maelstrom => QuestManager.IsQuestComplete(67926),
                    GrandCompany.TwinAdder => QuestManager.IsQuestComplete(67925),
                    GrandCompany.ImmortalFlames => QuestManager.IsQuestComplete(67927),
                    _ => false
                },
                MaxLevel = playerState->MaxLevel,
                ClassJobLevels = ExtractClassJobLevels(playerState),
            };

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
            PluginLog.Error(e, "Failed to update local stats");
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
