using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Influx.AllaganTools;

namespace Influx.Windows;

internal sealed class StatisticsWindow : Window
{
    private List<StatisticsRow> _rows = new();

    public StatisticsWindow()
        : base("Statistics###InfluxStatistics")
    {
        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        Size = new Vector2(400, 400);
        SizeCondition = ImGuiCond.Appearing;
    }

    public override void Draw()
    {
        if (ImGui.BeginTable("Currencies###InfluxStatisticsCurrencies", 2))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn($"Gil ({_rows.Sum(x => x.Gil):N0})##Gil");
            ImGui.TableHeadersRow();

            foreach (var row in _rows)
            {
                ImGui.TableNextRow();
                if (ImGui.TableNextColumn())
                    ImGui.Text(row.Name);

                if (ImGui.TableNextColumn())
                    ImGui.Text(row.Gil.ToString("N0", CultureInfo.InvariantCulture));
            }

            ImGui.EndTable();
        }
    }

    public void OnStatisticsUpdate(StatisticsUpdate update)
    {
        var retainers = update.Currencies
            .Where(x => x.Key.CharacterType == CharacterType.Retainer)
            .GroupBy(x => update.Currencies.FirstOrDefault(y => y.Key.CharacterId == x.Key.OwnerId).Key)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Value).ToList());

        _rows = update.Currencies.Where(x => x.Key.CharacterType == CharacterType.Character)
            .Select(x =>
            {
                var currencies = new List<Currencies> { x.Value };
                if (retainers.TryGetValue(x.Key, out var retainerCurrencies))
                    currencies.AddRange(retainerCurrencies);
                return new StatisticsRow
                {
                    Name = x.Key.Name,
                    Type = x.Key.CharacterType.ToString(),
                    Gil = currencies.Sum(y => y.Gil),
                    FcCredits = currencies.Sum(y => y.FcCredits),
                };
            })
            .Where(x => x.Gil > 0 || x.FcCredits > 0)
            .OrderByDescending(x => x.Gil)
            .ToList();
    }

    public sealed class StatisticsRow
    {
        public required string Name { get; init; }
        public required string Type { get; init; }
        public long Gil { get; init; }
        public long FcCredits { get; init; }
    }
}
