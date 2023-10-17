using System;
using Dalamud.Plugin.Services;
using LLib;
using Addon = Lumina.Excel.GeneratedSheets.Addon;

namespace Influx;

internal sealed class GameStrings
{
    public GameStrings(IDataManager dataManager, IPluginLog pluginLog)
    {
        LogoutToTitleScreen = dataManager.GetString<Addon>(115, s => s.Text, pluginLog)
                              ?? throw new Exception($"Unable to resolve {nameof(LogoutToTitleScreen)}");
        LogoutAndExitGame = dataManager.GetString<Addon>(116, s => s.Text, pluginLog)
                            ?? throw new Exception($"Unable to resolve {nameof(LogoutAndExitGame)}");
    }

    public string LogoutToTitleScreen { get; }
    public string LogoutAndExitGame { get; }
}
