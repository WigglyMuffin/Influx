using System;
using System.Linq;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace Influx.Windows;

internal sealed class ConfigurationWindow : Window
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;

    public ConfigurationWindow(DalamudPluginInterface pluginInterface, IClientState clientState,
        Configuration configuration)
        : base("Configuration###InfluxConfiguration")
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _configuration = configuration;
    }

    public event EventHandler? ConfigUpdated;

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("InfluxConfigTabs");
        if (tabBar)
        {
            DrawConnectionSettings();
            DrawIncludedCharacters();
        }
    }

    private void DrawConnectionSettings()
    {
        using var tabItem = ImRaii.TabItem("Connection Settings");
        if (!tabItem)
            return;

        bool enabled = _configuration.Server.Enabled;
        if (ImGui.Checkbox("Enable Server Connection", ref enabled))
        {
            _configuration.Server.Enabled = enabled;
            Save(true);
        }

        string server = _configuration.Server.Server;
        if (ImGui.InputText("InfluxDB URL", ref server, 128))
        {
            _configuration.Server.Server = server;
            Save(true);
        }

        string token = _configuration.Server.Token;
        if (ImGui.InputText("Token", ref token, 128, ImGuiInputTextFlags.Password))
        {
            _configuration.Server.Token = token;
            Save(true);
        }

        string organization = _configuration.Server.Organization;
        if (ImGui.InputText("Organization", ref organization, 128))
        {
            _configuration.Server.Organization = organization;
            Save(true);
        }

        string bucket = _configuration.Server.Bucket;
        if (ImGui.InputText("Bucket", ref bucket, 128))
        {
            _configuration.Server.Bucket = bucket;
            Save(true);
        }
    }

    private void DrawIncludedCharacters()
    {
        using var tabItem = ImRaii.TabItem("Included Characters");
        if (!tabItem)
            return;

        if (_clientState is { IsLoggedIn: true, LocalContentId: > 0 })
        {
            string worldName = _clientState.LocalPlayer?.HomeWorld.GameData?.Name ?? "??";
            ImGui.TextWrapped(
                $"Current Character: {_clientState.LocalPlayer?.Name} @ {worldName} ({_clientState.LocalContentId:X})");

            ImGui.Indent(30);
            Configuration.CharacterInfo? includedCharacter =
                _configuration.IncludedCharacters.FirstOrDefault(x => x.LocalContentId == _clientState.LocalContentId);
            if (includedCharacter != null)
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, "This character is currently included.");

                bool includeFreeCompany = includedCharacter.IncludeFreeCompany;
                if (ImGui.Checkbox("Include Free Company statistics", ref includeFreeCompany))
                {
                    includedCharacter.IncludeFreeCompany = includeFreeCompany;
                    Save();
                }

                ImGui.Spacing();

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

        if (_configuration.IncludedCharacters.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "No included characters.");
        }
        else
        {
            foreach (var world in _configuration.IncludedCharacters.OrderBy(x => x.CachedWorldName)
                         .ThenBy(x => x.LocalContentId).GroupBy(x => x.CachedWorldName))
            {
                if (ImGui.CollapsingHeader($"{world.Key} ({world.Count()})##World{world.Key}",
                        ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent(30);
                    foreach (var characterInfo in world)
                    {
                        ImGui.Selectable(
                            $"{characterInfo.CachedPlayerName} @ {characterInfo.CachedWorldName} ({characterInfo.LocalContentId:X}{(!characterInfo.IncludeFreeCompany ? ", no FC" : "")})");
                    }

                    ImGui.Unindent(30);
                }
            }
        }
    }

    private void Save(bool sendEvent = false)
    {
        _pluginInterface.SavePluginConfig(_configuration);

        if (sendEvent)
            ConfigUpdated?.Invoke(this, EventArgs.Empty);
    }
}
