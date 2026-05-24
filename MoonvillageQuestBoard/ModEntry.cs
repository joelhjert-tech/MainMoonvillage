using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Monsters;

namespace MoonvillageQuestBoard;

public sealed class ModEntry : Mod
{
	private BoardConfig config = new BoardConfig();

	private SaveState state = new SaveState();

	private ConditionChecker conditions = new ConditionChecker();

	private QuestRepository? repository;

	private QuestService? service;

	private Texture2D? boardTexture;

	internal static IModHelper? HelperRef { get; private set; }

	public override void Entry(IModHelper helper)
	{
		HelperRef = helper;
		config = helper.Data.ReadJsonFile<BoardConfig>("assets/config/BoardConfig.json") ?? new BoardConfig();
		repository = new QuestRepository(helper, ((Mod)this).Monitor, config);
		service = new QuestService(repository, conditions, helper, ((Mod)this).Monitor, config, state);
		helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
		helper.Events.GameLoop.DayStarted += OnDayStarted;
		helper.Events.GameLoop.Saving += OnSaving;
		helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
		helper.Events.Input.ButtonPressed += OnButtonPressed;
		helper.Events.Player.Warped += OnWarped;
		helper.Events.Display.RenderedWorld += OnRenderedWorld;
		helper.Events.GameLoop.GameLaunched += delegate
		{
			ReloadContent();
		};
		helper.Events.World.NpcListChanged += OnNpcListChanged;
	}

	private void ReloadContent()
	{
		repository?.Reload();
		try
		{
			boardTexture = ((Mod)this).Helper.ModContent.Load<Texture2D>(config.TextureFile);
		}
		catch (Exception ex)
		{
			((Mod)this).Monitor.Log("Could not load board texture '" + config.TextureFile + "': " + ex.Message, (LogLevel)3);
			boardTexture = null;
		}
	}

	private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
	{
		ReloadContent();
		state = ((Mod)this).Helper.Data.ReadSaveData<SaveState>("moonvillage-quest-board-state") ?? new SaveState();
		service?.SetState(state);
		service?.RefreshDailyOffers(force: true);
	}

	private void OnDayStarted(object? sender, DayStartedEventArgs e)
	{
		service?.RefreshDailyOffers(force: true);
	}

	private void OnSaving(object? sender, SavingEventArgs e)
	{
		((Mod)this).Helper.Data.WriteSaveData<SaveState>("moonvillage-quest-board-state", state);
	}

	private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
	{
		state = new SaveState();
	}

	private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (Context.IsWorldReady && SButtonExtensions.IsActionButton(e.Button))
		{
			GameLocation currentLocation = Game1.currentLocation;
			object obj = ((currentLocation != null) ? currentLocation.NameOrUniqueName : null);
			if (obj == null)
			{
				GameLocation currentLocation2 = Game1.currentLocation;
				obj = ((currentLocation2 != null) ? currentLocation2.Name : null) ?? "";
			}
			if (IsBoardTile((string)obj, e.Cursor.GrabTile))
			{
				((Mod)this).Helper.Input.Suppress(e.Button);
				OpenBoard();
			}
			else
			{
				CheckNpcTalk(e.Cursor.GrabTile);
			}
		}
	}

	private void OpenBoard()
	{
		if (service != null)
		{
			if (!conditions.CheckAll(config.UnlockConditions))
			{
				Game1.drawObjectDialogue("The board is silent. Moonvillage is not ready to post requests here yet.");
			}
			else
			{
				Game1.activeClickableMenu = (IClickableMenu)(object)new MoonBoardMenu(config, service);
			}
		}
	}

	private void CheckNpcTalk(Vector2 grabTile)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (service == null || Game1.currentLocation == null)
		{
			return;
		}
		foreach (NPC npc in Game1.currentLocation.characters)
		{
			if (Vector2.Distance(((Character)npc).Tile, grabTile) <= 1.5f)
			{
				service.CheckTalkToNpc(((Character)npc).Name);
				break;
			}
		}
	}

	private void OnWarped(object? sender, WarpedEventArgs e)
	{
		if (e.IsLocalPlayer && service != null)
		{
			service.CheckVisitLocation(e.NewLocation.NameOrUniqueName ?? e.NewLocation.Name);
		}
	}

	private void OnNpcListChanged(object? sender, NpcListChangedEventArgs e)
	{
		if (service == null)
		{
			return;
		}
		foreach (NPC npc in e.Removed)
		{
			Monster monster = (Monster)(object)((npc is Monster) ? npc : null);
			if (monster != null && monster.Health <= 0)
			{
				string name = ((Character)monster).Name ?? ((object)monster).GetType().Name ?? "";
				service.CheckMonsterSlain(name);
			}
		}
	}

	private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		if (Context.IsWorldReady && config.DrawBoardSprite && boardTexture != null && Game1.currentLocation != null)
		{
			string location = Game1.currentLocation.NameOrUniqueName ?? Game1.currentLocation.Name;
			if (string.Equals(location, config.Location, StringComparison.OrdinalIgnoreCase))
			{
				Point tilePoint = config.TilePoint;
				Vector2 tile = ((Point)(ref tilePoint)).ToVector2();
				Vector2 world = tile * 64f + config.DrawOffset;
				e.SpriteBatch.Draw(boardTexture, Game1.GlobalToLocal(Game1.viewport, world), (Rectangle?)null, Color.White, 0f, Vector2.Zero, config.WorldScale, (SpriteEffects)0, (tile.Y + 1f) * 64f / 10000f);
			}
		}
	}

	private bool IsBoardTile(string locationName, Vector2 tile)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		if (!string.Equals(locationName, config.Location, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		Point origin = config.TilePoint;
		Point footprint = config.FootprintPoint;
		if (tile.X >= (float)origin.X && tile.X < (float)(origin.X + footprint.X) && tile.Y >= (float)origin.Y)
		{
			return tile.Y < (float)(origin.Y + footprint.Y);
		}
		return false;
	}
}
