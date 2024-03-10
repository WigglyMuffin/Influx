using System.Collections.Generic;

namespace Influx.LocalStatistics;

public sealed class QuestInfo
{
    public required uint RowId { get; init; }
    public required string Name { get; init; }
    public required List<uint> PreviousQuestIds { get; init; }
    public required uint Genre { get; init; }
}
