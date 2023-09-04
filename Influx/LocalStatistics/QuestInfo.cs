using System.Collections.Generic;

namespace Influx.LocalStatistics;

public class QuestInfo
{
    public uint RowId { get; set; }
    public string Name { get; set; }
    public List<uint> PreviousQuestIds { get; set; }
    public uint Genre { get; set; }
}
