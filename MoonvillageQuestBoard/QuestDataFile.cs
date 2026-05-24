using System.Collections.Generic;

namespace MoonvillageQuestBoard;

public sealed class QuestDataFile
{
	public Dictionary<string, BoardQuest> Quests { get; set; } = new Dictionary<string, BoardQuest>();
}
