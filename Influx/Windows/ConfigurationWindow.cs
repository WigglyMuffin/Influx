using System.Linq;
using Dalamud.Game.ClientState;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;

namespace Influx.Windows;

internal class ConfigurationWindow : Window
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly ClientState _clientState;
    private readonly Configuration _configuration;

    public ConfigurationWindow(DalamudPluginInterface pluginInterface, ClientState clientState,
        Configuration configuration)
        : base("Configuration###InfluxConfiguration")
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _configuration = configuration;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("InfluxConfigTabs"))
        {
            DrawIncludedCharacters();

            ImGui.EndTabBar();
        }
    }

    private void DrawIncludedCharacters()
    {
        if (ImGui.BeginTabItem("Included Characters"))
        {
            if (_clientState is { IsLoggedIn: true, LocalContentId: > 0 })
            {
                string worldName = _clientState.LocalPlayer?.HomeWorld.GameData?.Name ?? "??";
                ImGui.TextWrapped(
                    $"Current Character: {_clientState.LocalPlayer?.Name} @ {worldName} ({_clientState.LocalContentId:X})");
                ImGui.Indent(30);
                if (_configuration.IncludedCharacters.Any(x => x.LocalContentId == _clientState.LocalContentId))
                {
                    ImGui.TextColored(ImGuiColors.HealerGreen, "This character is currently included.");
                    if (ImGui.Button("Remove inclusion"))
                    {
                        _configuration.IncludedCharacters.RemoveAll(
                            c => c.LocalContentId == _clientState.LocalContentId);
                        Save();
                    }
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed,
                        "This character is currently excluded.");

                    if (ImGui.Button("Include current character"))
                    {
                        _configuration.IncludedCharacters.Add(new Configuration.CharacterInfo
                        {
                            LocalContentId = _clientState.LocalContentId,
                            CachedPlayerName = _clientState.LocalPlayer?.Name.ToString() ?? "??",
                            CachedWorldName = worldName,
                        });
                        Save();
                    }
                }

                ImGui.Unindent(30);
            }
            else
            {
                ImGui.TextColored(ImGuiColors.DalamudRed, "You are not logged in.");
            }

            ImGui.Separator();
            ImGui.TextWrapped("Characters that are included:");
            ImGui.Spacing();

            ImGui.Indent(30);
            if (_configuration.IncludedCharacters.Count == 0)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No included characters.");
            }
            else
            {
                foreach (var characterInfo in _configuration.IncludedCharacters.OrderBy(x => x.CachedWorldName).ThenBy(x => x.LocalContentId))
                {
                    ImGui.Text(
                        $"{characterInfo.CachedPlayerName} @ {characterInfo.CachedWorldName} ({characterInfo.LocalContentId:X})");
                }
            }

            ImGui.Unindent(30);

            ImGui.EndTabItem();
        }
    }

    private void Save()
    {
        _pluginInterface.SavePluginConfig(_configuration);
    }
}
