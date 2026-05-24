namespace MoonvillageQuestBoard;

public sealed class RewardDef
{
	public string Kind { get; set; } = "Money";

	public int Amount { get; set; }

	public string Target { get; set; } = "";

	public int Count { get; set; } = 1;
}
