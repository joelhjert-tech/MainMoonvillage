namespace MoonvillageQuestBoard;

public sealed class ModConfig
{
	public int BoardTileX { get; set; } = 35;

	public int BoardTileY { get; set; } = 31;

	public bool DrawBoardSprite { get; set; } = true;

	public float BoardSpriteScale { get; set; } = 4f;

	public int BoardDrawOffsetX { get; set; } = 0;

	public int BoardDrawOffsetY { get; set; } = -64;

	public int MaxOffersPerDay { get; set; } = 3;

	public int MaxOffersPerGiverPerDay { get; set; } = 1;

	public int RecentOfferCooldownDays { get; set; } = 2;

	public bool ForceRefreshOffersOnLoad { get; set; } = true;

	public bool DebugLogging { get; set; }

	public bool ApplyFlagChangesOnSaveLoad { get; set; }

	public string EventFlagsToAdd { get; set; } = "";

	public string EventFlagsToRemove { get; set; } = "";

	public string MailFlagsToAdd { get; set; } = "";

	public string MailFlagsToRemove { get; set; } = "";
}
