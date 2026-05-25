using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MoonvillageQuestBoard;

public sealed class MoonBoardMenu : IClickableMenu
{
	private readonly BoardConfig config;

	private readonly QuestService service;

	private readonly List<BoardQuest> offers;

	private readonly List<BoardQuest> active;

	private readonly List<ClickableComponent> acceptButtons = new List<ClickableComponent>();

	private readonly List<ClickableComponent> completeButtons = new List<ClickableComponent>();

	public MoonBoardMenu(BoardConfig config, QuestService service)
		: base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, true)
	{
		this.config = config;
		this.service = service;
		offers = service.GetTodaysOffers();
		active = service.GetActiveQuestDefs();
		RebuildButtons();
	}

	private void RebuildButtons()
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Expected O, but got Unknown
		acceptButtons.Clear();
		completeButtons.Clear();
		int y = base.yPositionOnScreen + 120;
		for (int i = 0; i < offers.Count; i++)
		{
			acceptButtons.Add(new ClickableComponent(new Rectangle(base.xPositionOnScreen + base.width - 190, y + i * 110, 130, 48), offers[i].Id));
		}
		int activeY = y + Math.Max(1, offers.Count) * 110 + 20;
		for (int j = 0; j < active.Count; j++)
		{
			completeButtons.Add(new ClickableComponent(new Rectangle(base.xPositionOnScreen + base.width - 190, activeY + j * 80, 130, 48), active[j].Id));
		}
	}

	public override void receiveLeftClick(int x, int y, bool playSound = true)
	{
		foreach (ClickableComponent button in acceptButtons)
		{
			if (button.containsPoint(x, y))
			{
				BoardQuest quest = offers.FirstOrDefault((BoardQuest q) => q.Id == button.name);
				if (quest != null && service.Accept(quest))
				{
					Game1.playSound("newArtifact", (int?)null);
					Game1.exitActiveMenu();
				}
				return;
			}
		}
		foreach (ClickableComponent button2 in completeButtons)
		{
			if (button2.containsPoint(x, y))
			{
				service.TryCompleteReadyQuests();
				Game1.playSound("smallSelect", (int?)null);
				Game1.exitActiveMenu();
				return;
			}
		}
		base.receiveLeftClick(x, y, playSound);
	}

	public override void draw(SpriteBatch b)
	{
		try
		{
			DrawContents(b);
		}
		catch (Exception ex)
		{
			ModEntry.MonitorRef?.Log($"Moon quest board failed while drawing: {ex}", LogLevel.Error);
			Game1.drawDialogueBox(base.xPositionOnScreen, base.yPositionOnScreen, base.width, base.height, false, true);
			Utility.drawTextWithShadow(b, "Moon quest board could not be drawn. Check the SMAPI log for details.", Game1.smallFont, new Vector2(base.xPositionOnScreen + 60, base.yPositionOnScreen + 80), Game1.textColor);
		}
		base.drawMouse(b, false, -1);
	}

	private void DrawContents(SpriteBatch b)
	{
		IClickableMenu.drawTextureBox(b, base.xPositionOnScreen, base.yPositionOnScreen, base.width, base.height, Color.White);
		Utility.drawTextWithShadow(b, SafeText(config.Title), Game1.dialogueFont, new Vector2(base.xPositionOnScreen + 48, base.yPositionOnScreen + 28), Game1.textColor);
		int y = base.yPositionOnScreen + 110;
		if (offers.Count == 0)
		{
			Utility.drawTextWithShadow(b, SafeText("i18n:questboard.no_offers"), Game1.smallFont, new Vector2((float)(base.xPositionOnScreen + 60), (float)y), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
			y += 70;
		}
		else
		{
			foreach (BoardQuest quest in offers)
			{
				DrawQuestRow(b, quest, y, activeQuest: false);
				y += 110;
			}
		}
		if (active.Count > 0)
		{
			Utility.drawTextWithShadow(b, SafeText("i18n:questboard.active"), Game1.smallFont, new Vector2((float)(base.xPositionOnScreen + 60), (float)(y + 10)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
			y += 55;
			foreach (BoardQuest quest2 in active)
			{
				DrawQuestRow(b, quest2, y, activeQuest: true);
				y += 80;
			}
		}
	}

	private void DrawQuestRow(SpriteBatch b, BoardQuest quest, int y, bool activeQuest)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		int x = base.xPositionOnScreen + 60;
		string title = activeQuest ? ("- " + SafeText(quest.Title)) : SafeText(quest.Title);
		Utility.drawTextWithShadow(b, title, Game1.smallFont, new Vector2((float)x, (float)y), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
		string body = SafeText(string.IsNullOrWhiteSpace(quest.ObjectiveText) ? quest.Description : quest.ObjectiveText);
		Utility.drawTextWithShadow(b, Game1.parseText(body, Game1.smallFont, base.width - 290), Game1.smallFont, new Vector2((float)x, (float)(y + 32)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
		Rectangle button = new Rectangle(base.xPositionOnScreen + base.width - 190, y + 10, 130, 48);
		IClickableMenu.drawTextureBox(b, button.X, button.Y, button.Width, button.Height, Color.White);
		Utility.drawTextWithShadow(b, SafeText(activeQuest ? "i18n:questboard.check" : "i18n:questboard.accept"), Game1.smallFont, new Vector2((float)(button.X + 26), (float)(button.Y + 12)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
	}

	private static string SafeText(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? "" : I18n.Text(value) ?? "";
	}
}
