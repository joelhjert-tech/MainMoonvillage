using System.Collections.Generic;

namespace MoonvillageQuestBoard;

public sealed class BoardQuest
{
	public string Id { get; set; } = "";

	public string Title { get; set; } = "Untitled Request";

	public string Type { get; set; } = "Quest";

	public string Category { get; set; } = "Moonvillage";

	public string Giver { get; set; } = "DittModID_Annette";

	public string Description { get; set; } = "";

	public string AcceptText { get; set; } = "Request accepted.";

	public string CompleteText { get; set; } = "Request completed.";

	public string ObjectiveText { get; set; } = "";

	public int Weight { get; set; } = 100;

	public int CooldownDays { get; set; } = 7;

	public int DeadlineDays { get; set; } = 7;

	public Dictionary<string, string> Conditions { get; set; } = new Dictionary<string, string>();

	public List<ObjectiveDef> Objectives { get; set; } = new List<ObjectiveDef>();

	public List<RewardDef> Rewards { get; set; } = new List<RewardDef>();

	public string CompletionMailFlag { get; set; } = "";
}
