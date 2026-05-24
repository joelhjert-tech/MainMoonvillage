using System.Collections.Generic;

namespace MoonvillageQuestBoard;

public sealed class SpecialOrdersDataFile
{
	public Dictionary<string, BoardQuest> SpecialOrders { get; set; } = new Dictionary<string, BoardQuest>();
}
