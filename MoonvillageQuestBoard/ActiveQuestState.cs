using System.Collections.Generic;

namespace MoonvillageQuestBoard;

public sealed class ActiveQuestState
{
	public string QuestId { get; set; } = "";

	public int AcceptedDay { get; set; }

	public int DueDay { get; set; }

	public Dictionary<int, int> ObjectiveProgress { get; set; } = new Dictionary<int, int>();

	public bool IsSpecialOrder { get; set; }
}
