using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace MoonvillageQuestBoard;

public sealed class BoardConfig
{
	public string BoardId { get; set; } = "MoonvillageBoard";

	public string Location { get; set; } = "Custom_Moonvillage";

	public int[] Tile { get; set; } = new int[2] { 28, 19 };

	public int[] FootprintTiles { get; set; } = new int[2] { 1, 1 };

	public string Title { get; set; } = "Moon Village Requests";

	public string TextureFile { get; set; } = "assets/Textures/MoonQuestBoard.png";

	public string BackgroundFile { get; set; } = "assets/Textures/MoonQuestBoardBackground.png";

	public string QuestsFile { get; set; } = "assets/Data/Quest.json";

	public string SpecialOrdersFile { get; set; } = "assets/Data/SpecialOrders.json";

	public int MaxOffersPerDay { get; set; } = 3;

	public bool DrawBoardSprite { get; set; } = true;

	public float WorldScale { get; set; } = 4f;

	public int[] DrawOffsetPixels { get; set; } = new int[2] { 0, -64 };

	public Dictionary<string, string> UnlockConditions { get; set; } = new Dictionary<string, string>
	{
		["eventseen"] = "991001",
		["npcmet"] = "DittModID_Annette"
	};

	public Point TilePoint => new Point(Tile.ElementAtOrDefault(0), Tile.ElementAtOrDefault(1));

	public Point FootprintPoint => new Point(Math.Max(1, FootprintTiles.ElementAtOrDefault(0)), Math.Max(1, FootprintTiles.ElementAtOrDefault(1)));

	public Vector2 DrawOffset => new Vector2((float)DrawOffsetPixels.ElementAtOrDefault(0), (float)DrawOffsetPixels.ElementAtOrDefault(1));
}
