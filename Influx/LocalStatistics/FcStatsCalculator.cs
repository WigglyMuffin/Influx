using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoRetainerAPI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Newtonsoft.Json;

namespace Influx.LocalStatistics;

internal sealed class FcStatsCalculator : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IGameGui _gameGui;
    private readonly IFramework _framework;
    private readonly Configuration _configuration;
    private readonly IPluginLog _pluginLog;
    private readonly AutoRetainerApi _autoRetainerApi;

    private readonly Dictionary<ulong, FcStats> _cache = new();

    private Status? _status;

    public FcStatsCalculator(
        IDalamudPlugin plugin,
        DalamudPluginInterface pluginInterface,
        IClientState clientState,
        IAddonLifecycle addonLifecycle,
        IGameGui gameGui,
        IFramework framework,
        Configuration configuration,
        IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _addonLifecycle = addonLifecycle;
        _gameGui = gameGui;
        _framework = framework;
        _configuration = configuration;
        _pluginLog = pluginLog;

        ECommonsMain.Init(_pluginInterface, plugin);
        _autoRetainerApi = new();
        _autoRetainerApi.OnCharacterPostprocessStep += CheckCharacterPostProcess;
        _autoRetainerApi.OnCharacterReadyToPostProcess += DoCharacterPostProcess;
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "FreeCompany", FcPostReceiveEvent);
        _framework.Update += FrameworkUpdate;
        _clientState.Logout += Logout;

        foreach (var file in _pluginInterface.ConfigDirectory.GetFiles("f.*.json"))
        {
            try
            {
                var stats = JsonConvert.DeserializeObject<FcStats>(File.ReadAllText(file.FullName));
                if (stats == null)
                    continue;

                _cache[stats.ContentId] = stats;
            }
            catch (Exception e)
            {
                _pluginLog.Warning(e, $"Could not parse file {file.FullName}");
            }
        }
    }

    private unsafe void CheckCharacterPostProcess()
    {
        bool includeFc = _configuration.IncludedCharacters.Any(x =>
            x.LocalContentId == _clientState.LocalContentId &&
            x.IncludeFreeCompany);
        if (!includeFc)
            return;

        var infoProxy = Framework.Instance()->UIModule->GetInfoModule()->GetInfoProxyById(InfoProxyId.FreeCompany);
        if (infoProxy != null)
        {
            var fcProxy = (InfoProxyFreeCompany*)infoProxy;
            if (fcProxy->ID != 0)
            {
                _pluginLog.Information($"Requesting post-process, FC is {fcProxy->ID}");
                _autoRetainerApi.RequestCharacterPostprocess();
            }
            else
                _pluginLog.Information("No FC id");
        }
        else
            _pluginLog.Information("No FreeCompany info proxy");
    }

    private void DoCharacterPostProcess()
    {
        _status = new();

        unsafe
        {
            AtkUnitBase* addon = (AtkUnitBase*)_gameGui.GetAddonByName("FreeCompany");
            if (addon != null && addon->IsVisible)
                FcPostReceiveEvent(AddonEvent.PostReceiveEvent);
            else
                Chat.Instance.SendMessage("/freecompanycmd");
        }
    }

    private void FcPostReceiveEvent(AddonEvent type, AddonArgs? args = null)
    {
        if (_status != null)
        {
            _pluginLog.Verbose("FC window received event...");
            _status.WindowOpened = true;
        }
        else
            _pluginLog.Verbose("Not tracking status for FC window");
    }

    private void FrameworkUpdate(IFramework framework)
    {
        if (_status == null)
            return;

        if (_status.FallbackFinishPostProcessing < DateTime.Now)
        {
            _status = null;
            _autoRetainerApi.FinishCharacterPostProcess();
        }
        else if (_status.WindowOpened && UpdateFcCredits())
        {
            _status = null;
            _autoRetainerApi.FinishCharacterPostProcess();
        }
    }

    private void Logout()
    {
        if (_status != null)
            _autoRetainerApi.FinishCharacterPostProcess();
        _status = null;
    }

    // ideally we'd hook the update to the number array, but #effort
    private unsafe bool UpdateFcCredits()
    {
        try
        {
            var infoProxy =
                Framework.Instance()->UIModule->GetInfoModule()->GetInfoProxyById(InfoProxyId.FreeCompany);
            if (infoProxy != null)
            {
                var fcProxy = (InfoProxyFreeCompany*)infoProxy;
                ulong localContentId = fcProxy->ID;
                if (localContentId != 0)
                {
                    var atkArrays = Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule
                        .AtkArrayDataHolder;
                    if (atkArrays.NumberArrayCount > 50)
                    {
                        var fcArrayData = atkArrays.GetNumberArrayData(50);
                        FcStats fcStats = new FcStats
                        {
                            ContentId = localContentId,
                            FcCredits = fcArrayData->IntArray[9]
                        };

                        _pluginLog.Verbose($"Current FC credits: {fcStats.FcCredits:N0}");
                        if (fcStats.FcCredits > 0)
                        {
                            if (_cache.TryGetValue(localContentId, out var existingStats))
                            {
                                if (existingStats != fcStats)
                                {
                                    _cache[localContentId] = fcStats;
                                    File.WriteAllText(
                                        Path.Join(_pluginInterface.GetPluginConfigDirectory(),
                                            $"f.{localContentId:X8}.json"),
                                        JsonConvert.SerializeObject(fcStats));
                                }
                            }
                            else
                            {
                                _cache[localContentId] = fcStats;
                                File.WriteAllText(
                                    Path.Join(_pluginInterface.GetPluginConfigDirectory(),
                                        $"f.{localContentId:X8}.json"),
                                    JsonConvert.SerializeObject(fcStats));
                            }

                            return true;
                        }
                    }

                    return false;
                }
                else
                    // no point updating if no fc id
                    return true;
            }
        }
        catch (Exception e)
        {
            _pluginLog.Warning(e, "Unable to update fc credits");
        }

        return false;
    }

    public IReadOnlyDictionary<ulong, FcStats> GetAllFcStats() => _cache.AsReadOnly();

    public HashSet<string> GetEnabledSubs(ulong characterId)
    {
        var offlineCharacterData = _autoRetainerApi.GetOfflineCharacterData(characterId);
        if (offlineCharacterData == null || !offlineCharacterData.WorkshopEnabled)
            return [];

        return offlineCharacterData.EnabledSubs;
    }

    public void Dispose()
    {
        _clientState.Logout -= Logout;
        _framework.Update -= FrameworkUpdate;
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "FreeCompany", FcPostReceiveEvent);
        _autoRetainerApi.OnCharacterPostprocessStep -= CheckCharacterPostProcess;
        _autoRetainerApi.OnCharacterReadyToPostProcess -= DoCharacterPostProcess;
        _autoRetainerApi.Dispose();
        ECommonsMain.Dispose();
    }

    private sealed class Status
    {
        public bool WindowOpened { get; set; }
        public DateTime FallbackFinishPostProcessing { get; set; } = DateTime.Now.AddSeconds(10);
    }
}
