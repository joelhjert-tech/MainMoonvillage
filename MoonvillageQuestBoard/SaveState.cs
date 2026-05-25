using System.Collections.Generic;

namespace MoonvillageQuestBoard;

public sealed class SaveState
{
	public int LastRefreshDay { get; set; } = -1;

	public List<string> TodaysOfferIds { get; set; } = new List<string>();

	public Dictionary<string, ActiveQuestState> ActiveQuests { get; set; } = new Dictionary<string, ActiveQuestState>();

	public Dictionary<string, int> LastCompletedDay { get; set; } = new Dictionary<string, int>();

	public Dictionary<string, int> LastOfferedDay { get; set; } = new Dictionary<string, int>();
}
