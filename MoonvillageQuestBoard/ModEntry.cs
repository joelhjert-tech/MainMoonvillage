using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Monsters;

namespace MoonvillageQuestBoard;

public sealed class ModEntry : Mod
{
	private BoardConfig config = new BoardConfig();

	private ModConfig userConfig = new ModConfig();

	private SaveState state = new SaveState();

	private ConditionChecker conditions = new ConditionChecker();

	private QuestRepository? repository;

	private QuestService? service;

	private Texture2D? boardTexture;

	internal static IModHelper? HelperRef { get; private set; }

	internal static IMonitor? MonitorRef { get; private set; }

	public override void Entry(IModHelper helper)
	{
		HelperRef = helper;
		MonitorRef = Monitor;
		config = helper.Data.ReadJsonFile<BoardConfig>("assets/config/BoardConfig.json") ?? new BoardConfig();
		userConfig = helper.ReadConfig<ModConfig>();
		ApplyUserConfig();
		repository = new QuestRepository(helper, ((Mod)this).Monitor, config);
		service = new QuestService(repository, conditions, helper, ((Mod)this).Monitor, config, state);
		helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
		helper.Events.GameLoop.DayStarted += OnDayStarted;
		helper.Events.GameLoop.Saving += OnSaving;
		helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
		helper.Events.Input.ButtonPressed += OnButtonPressed;
		helper.Events.Player.Warped += OnWarped;
		helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
		helper.Events.Display.RenderedWorld += OnRenderedWorld;
		helper.Events.GameLoop.GameLaunched += OnGameLaunched;
		helper.Events.World.NpcListChanged += OnNpcListChanged;
	}

	private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
	{
		RegisterConfigMenu();
		ReloadContent();
	}

	private void RegisterConfigMenu()
	{
		IGenericModConfigMenuApi? configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
		if (configMenu == null)
		{
			if (userConfig.DebugLogging)
			{
				Monitor.Log("Generic Mod Config Menu not installed; skipping in-game config menu.", LogLevel.Trace);
			}
			return;
		}

		configMenu.Register(ModManifest, ResetConfig, SaveConfig);

		configMenu.AddSectionTitle(ModManifest, () => I18n.Text("i18n:config.section.board"));
		configMenu.AddNumberOption(ModManifest, () => userConfig.BoardTileX, value => userConfig.BoardTileX = value, () => I18n.Text("i18n:config.board_tile_x"), min: 0, max: 200);
		configMenu.AddNumberOption(ModManifest, () => userConfig.BoardTileY, value => userConfig.BoardTileY = value, () => I18n.Text("i18n:config.board_tile_y"), min: 0, max: 200);
		configMenu.AddBoolOption(ModManifest, () => userConfig.DrawBoardSprite, value => userConfig.DrawBoardSprite = value, () => I18n.Text("i18n:config.draw_board_sprite"));
		configMenu.AddNumberOption(ModManifest, () => userConfig.BoardSpriteScale, value => userConfig.BoardSpriteScale = value, () => I18n.Text("i18n:config.board_sprite_scale"), min: 1f, max: 8f, interval: 0.25f);
		configMenu.AddNumberOption(ModManifest, () => userConfig.BoardDrawOffsetX, value => userConfig.BoardDrawOffsetX = value, () => I18n.Text("i18n:config.board_draw_offset_x"), min: -256, max: 256);
		configMenu.AddNumberOption(ModManifest, () => userConfig.BoardDrawOffsetY, value => userConfig.BoardDrawOffsetY = value, () => I18n.Text("i18n:config.board_draw_offset_y"), min: -256, max: 256);

		configMenu.AddSectionTitle(ModManifest, () => I18n.Text("i18n:config.section.offers"));
		configMenu.AddNumberOption(ModManifest, () => userConfig.MaxOffersPerDay, value => userConfig.MaxOffersPerDay = value, () => I18n.Text("i18n:config.max_offers_per_day"), min: 1, max: 10);
		configMenu.AddNumberOption(ModManifest, () => userConfig.MaxOffersPerGiverPerDay, value => userConfig.MaxOffersPerGiverPerDay = value, () => I18n.Text("i18n:config.max_offers_per_giver_per_day"), min: 0, max: 10);
		configMenu.AddNumberOption(ModManifest, () => userConfig.RecentOfferCooldownDays, value => userConfig.RecentOfferCooldownDays = value, () => I18n.Text("i18n:config.recent_offer_cooldown_days"), min: 0, max: 28);
		configMenu.AddBoolOption(ModManifest, () => userConfig.ForceRefreshOffersOnLoad, value => userConfig.ForceRefreshOffersOnLoad = value, () => I18n.Text("i18n:config.force_refresh_offers_on_load"));

		configMenu.AddSectionTitle(ModManifest, () => I18n.Text("i18n:config.section.debug"));
		configMenu.AddBoolOption(ModManifest, () => userConfig.DebugLogging, value => userConfig.DebugLogging = value, () => I18n.Text("i18n:config.debug_logging"));
		configMenu.AddBoolOption(ModManifest, () => userConfig.ApplyFlagChangesOnSaveLoad, value => userConfig.ApplyFlagChangesOnSaveLoad = value, () => I18n.Text("i18n:config.apply_flag_changes_on_save_load"));
		configMenu.AddTextOption(ModManifest, () => userConfig.EventFlagsToAdd, value => userConfig.EventFlagsToAdd = value, () => I18n.Text("i18n:config.event_flags_to_add"), () => I18n.Text("i18n:config.flag_list_tooltip"));
		configMenu.AddTextOption(ModManifest, () => userConfig.EventFlagsToRemove, value => userConfig.EventFlagsToRemove = value, () => I18n.Text("i18n:config.event_flags_to_remove"), () => I18n.Text("i18n:config.flag_list_tooltip"));
		configMenu.AddTextOption(ModManifest, () => userConfig.MailFlagsToAdd, value => userConfig.MailFlagsToAdd = value, () => I18n.Text("i18n:config.mail_flags_to_add"), () => I18n.Text("i18n:config.flag_list_tooltip"));
		configMenu.AddTextOption(ModManifest, () => userConfig.MailFlagsToRemove, value => userConfig.MailFlagsToRemove = value, () => I18n.Text("i18n:config.mail_flags_to_remove"), () => I18n.Text("i18n:config.flag_list_tooltip"));
	}

	private void ResetConfig()
	{
		userConfig = new ModConfig();
		SaveConfig();
	}

	private void SaveConfig()
	{
		Helper.WriteConfig(userConfig);
		ApplyUserConfig();
		if (Context.IsWorldReady)
		{
			if (userConfig.ApplyFlagChangesOnSaveLoad)
			{
				ApplyConfiguredFlagChanges();
			}
			service?.RefreshDailyOffers(force: true);
		}
	}

	private void ApplyUserConfig()
	{
		config.Tile = new[] { Math.Max(0, userConfig.BoardTileX), Math.Max(0, userConfig.BoardTileY) };
		config.DrawBoardSprite = userConfig.DrawBoardSprite;
		config.WorldScale = Math.Clamp(userConfig.BoardSpriteScale, 1f, 8f);
		config.DrawOffsetPixels = new[] { userConfig.BoardDrawOffsetX, userConfig.BoardDrawOffsetY };
		config.MaxOffersPerDay = Math.Clamp(userConfig.MaxOffersPerDay, 1, 10);
		config.MaxOffersPerGiverPerDay = Math.Clamp(userConfig.MaxOffersPerGiverPerDay, 0, 10);
		config.RecentOfferCooldownDays = Math.Clamp(userConfig.RecentOfferCooldownDays, 0, 28);
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
		ApplyUserConfig();
		ReloadContent();
		state = ((Mod)this).Helper.Data.ReadSaveData<SaveState>("moonvillage-quest-board-state") ?? new SaveState();
		service?.SetState(state);
		if (userConfig.ApplyFlagChangesOnSaveLoad)
		{
			ApplyConfiguredFlagChanges();
		}
		service?.RefreshDailyOffers(force: userConfig.ForceRefreshOffersOnLoad);
	}

	private void ApplyConfiguredFlagChanges()
	{
		if (!Context.IsWorldReady)
		{
			return;
		}

		List<string> addedEvents = AddFlags(((NetHashSet<string>)(object)Game1.player.eventsSeen), userConfig.EventFlagsToAdd);
		List<string> removedEvents = RemoveFlags(((NetHashSet<string>)(object)Game1.player.eventsSeen), userConfig.EventFlagsToRemove);
		List<string> addedMail = AddFlags(((NetHashSet<string>)(object)Game1.player.mailReceived), userConfig.MailFlagsToAdd);
		List<string> removedMail = RemoveFlags(((NetHashSet<string>)(object)Game1.player.mailReceived), userConfig.MailFlagsToRemove);
		removedMail.AddRange(RemoveFlags(((NetHashSet<string>)(object)Game1.player.mailForTomorrow), userConfig.MailFlagsToRemove));

		if (userConfig.DebugLogging)
		{
			LogFlagChanges("event flags added", addedEvents);
			LogFlagChanges("event flags removed", removedEvents);
			LogFlagChanges("mail flags added", addedMail);
			LogFlagChanges("mail flags removed", removedMail);
		}
	}

	private static List<string> AddFlags(NetHashSet<string> flags, string rawFlags)
	{
		List<string> changed = new List<string>();
		foreach (string flag in ParseFlagList(rawFlags))
		{
			if (flags.Add(flag))
			{
				changed.Add(flag);
			}
		}
		return changed;
	}

	private static List<string> RemoveFlags(NetHashSet<string> flags, string rawFlags)
	{
		List<string> changed = new List<string>();
		foreach (string flag in ParseFlagList(rawFlags))
		{
			if (flags.Remove(flag))
			{
				changed.Add(flag);
			}
		}
		return changed;
	}

	private static IEnumerable<string> ParseFlagList(string rawFlags)
	{
		if (string.IsNullOrWhiteSpace(rawFlags))
		{
			return Enumerable.Empty<string>();
		}
		return rawFlags
			.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(flag => !string.IsNullOrWhiteSpace(flag));
	}

	private void LogFlagChanges(string label, List<string> flags)
	{
		if (flags.Count > 0)
		{
			Monitor.Log($"Configured {label}: {string.Join(", ", flags)}", LogLevel.Info);
		}
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

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.activeClickableMenu != null)
		{
			return;
		}

		string location = Game1.currentLocation.NameOrUniqueName ?? Game1.currentLocation.Name ?? "";
		if (IsBoardTile(location, Helper.Input.GetCursorPosition().GrabTile))
		{
			Game1.mouseCursor = 2;
		}
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
				Game1.drawObjectDialogue(I18n.Text("i18n:questboard.locked"));
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
				Vector2 tile = tilePoint.ToVector2();
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
