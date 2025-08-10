using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using Influx.AllaganTools;
using LLib.ImGui;

namespace Influx.Windows;

internal sealed class ConfigurationWindow : LWindow, IDisposable
{
    private readonly string[] _remoteTypeNames = ["InfluxDB", "QuestDB"];

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly AllaganToolsIpc _allaganToolsIpc;
    private string[] _filterNames = [];
    private int _filterIndexToAdd;
    private CancellationTokenSource _cts = new();
    private (bool Success, string Error)? _testConnectionResult;

    public ConfigurationWindow(IDalamudPluginInterface pluginInterface, IClientState clientState,
        Configuration configuration, AllaganToolsIpc allaganToolsIpc)
        : base("Configuration###InfluxConfiguration")
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _configuration = configuration;
        _allaganToolsIpc = allaganToolsIpc;
    }

    public event EventHandler? ConfigUpdated;
    public Func<CancellationToken, Task<(bool Success, string Error)>>? TestConnection { get; set; }

    public override void DrawContent()
    {
        using var tabBar = ImRaii.TabBar("InfluxConfigTabs");
        if (tabBar)
        {
            DrawConnectionSettings();
            DrawIncludedCharacters();
            DrawAllaganToolsFilters();
        }
    }

    public override void OnOpen() => RefreshFilters();

    private void RefreshFilters()
    {
        _filterNames = _allaganToolsIpc.GetSearchFilters()
            .Select(x => x.Value)
            .Order()
            .ToArray();
        _filterIndexToAdd = 0;
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

        int type = (int)_configuration.Server.Type;
        if (ImGui.Combo("Server Type", ref type, _remoteTypeNames, _remoteTypeNames.Length))
        {
            _configuration.Server.Type = (Configuration.ERemoteType)type;
            Save(true);
        }

        string server = _configuration.Server.Server;
        if (ImGui.InputText("Server URL", ref server, 128))
        {
            _configuration.Server.Server = server;
            Save(true);
        }

        if (_configuration.Server.Type == Configuration.ERemoteType.InfluxDb)
        {
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

            if (TestConnection != null)
            {
                if (ImGui.Button("Test Connection"))
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _testConnectionResult = null;

                    _cts = new CancellationTokenSource();
                    var cancellationToken = _cts.Token;

                    Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            var result = await TestConnection(cancellationToken).ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();

                            _testConnectionResult = result;
                        }
                        catch (TaskCanceledException)
                        {
                            // irrelevant
                        }
                    }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
                }

                if (_testConnectionResult is { } connectionResult)
                {
                    if (connectionResult.Success && string.IsNullOrEmpty(connectionResult.Error))
                        TextWrapped(ImGuiColors.HealerGreen, "Connection successful.");
                    else if (connectionResult.Success)
                    {
                        TextWrapped(ImGuiColors.HealerGreen, "URL, Token and Organization are valid.");
                        TextWrapped(ImGuiColors.DalamudYellow, connectionResult.Error);
                    }
                    else
                        TextWrapped(ImGuiColors.DalamudRed, $"Connection failed: {connectionResult.Error}");
                }
            }
        }
        else
        {
            string username = _configuration.Server.Username;
            if (ImGui.InputText("Username", ref username, 128))
            {
                _configuration.Server.Username = username;
                Save(true);
            }

            string password = _configuration.Server.Password;
            if (ImGui.InputText("Password", ref password, 128, ImGuiInputTextFlags.Password))
            {
                _configuration.Server.Password = password;
                Save(true);
            }

            string tablePrefix = _configuration.Server.TablePrefix;
            if (ImGui.InputText("Prefix", ref tablePrefix, 128))
            {
                _configuration.Server.TablePrefix = tablePrefix;
                Save(true);
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Helpful to distinguish between different accounts (which may upload data at different times).\nIf this is set to 'a', the tables will be named 'a_quests', 'a_retainer' etc.");
        }
    }

    private static void TextWrapped(Vector4 color, string text)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
    }

    private void DrawIncludedCharacters()
    {
        using var tabItem = ImRaii.TabItem("Included Characters");
        if (!tabItem)
            return;

        var refIncludeAll = _configuration.AutoEnrollCharacters;
        if (ImGui.Checkbox("Auto enroll characters on login", ref refIncludeAll))
        {
            _configuration.AutoEnrollCharacters = refIncludeAll;
            Save(true);
        }

        if (_clientState is { IsLoggedIn: true, LocalContentId: > 0, LocalPlayer.HomeWorld.RowId: > 0 })
        {
            string worldName = _clientState.LocalPlayer.HomeWorld.Value.Name.ToString();
            ImGui.TextWrapped(
                $"Current Character: {_clientState.LocalPlayer.Name} @ {worldName} ({_clientState.LocalContentId:X})");

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
                    var characterInfo =
                        _configuration.IncludedCharacters.First(c => c.LocalContentId == _clientState.LocalContentId);
                    _configuration.IncludedCharacters.Remove(characterInfo);
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
            Configuration.CharacterInfo? characterToRemove = null;
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
                        ImGui.OpenPopupOnItemClick($"###Context{characterInfo.LocalContentId}");

                        using var popup = ImRaii.ContextPopup($"###Context{characterInfo.LocalContentId}");
                        if (!popup)
                            continue;

                        if (!characterInfo.IncludeFreeCompany)
                        {
                            if (ImGui.MenuItem("Include Free Company"))
                            {
                                characterInfo.IncludeFreeCompany = true;
                                Save();
                            }
                        }
                        else
                        {
                            if (ImGui.MenuItem("Exclude Free Company"))
                            {
                                characterInfo.IncludeFreeCompany = false;
                                Save();
                            }
                        }

                        if (ImGui.MenuItem($"Remove {characterInfo.CachedPlayerName}"))
                            characterToRemove = characterInfo;
                    }

                    ImGui.Unindent(30);
                }
            }

            if (characterToRemove != null)
            {
                _configuration.IncludedCharacters.Remove(characterToRemove);
                Save();
            }
        }
    }

    private void DrawAllaganToolsFilters()
    {
        using var tabItem = ImRaii.TabItem("Inventory Filters");
        if (!tabItem)
            return;

        if (_configuration.IncludedInventoryFilters.Count > 0)
        {
            int? indexToRemove = null;

            ImGui.Text("Selected Filters:");
            ImGui.Indent(30);
            foreach (var filter in _configuration.IncludedInventoryFilters)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, $"{filter.Name}"))
                {
                    indexToRemove = _configuration.IncludedInventoryFilters.IndexOf(filter);
                }
            }

            ImGui.Unindent(30);

            if (indexToRemove != null)
            {
                _configuration.IncludedInventoryFilters.RemoveAt(indexToRemove.Value);
                Save();
            }
        }
        else
        {
            ImGui.Text("You are not tracking any AllaganTools filters.");
        }

        ImGui.Separator();

        if (_filterNames.Length > 0)
        {
            ImGui.Combo("Add Search Filter", ref _filterIndexToAdd, _filterNames, _filterNames.Length);

            ImGui.BeginDisabled(
                _configuration.IncludedInventoryFilters.Any(x => x.Name == _filterNames[_filterIndexToAdd]));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Track Filter"))
            {
                _configuration.IncludedInventoryFilters.Add(new Configuration.FilterInfo
                {
                    Name = _filterNames[_filterIndexToAdd],
                });
                Save();
            }

            ImGui.EndDisabled();
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudRed,
                "You don't have any search filters, or the AllaganTools integration doesn't work.");
        }

        ImGui.Separator();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh Filters"))
            RefreshFilters();
    }

    private void Save(bool sendEvent = false)
    {
        _pluginInterface.SavePluginConfig(_configuration);

        if (sendEvent)
            ConfigUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
