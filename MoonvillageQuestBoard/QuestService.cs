using System;
using System.Collections.Generic;
using System.Linq;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MoonvillageQuestBoard;

public sealed class QuestService
{
	private readonly QuestRepository quests;

	private readonly ConditionChecker conditions;

	private readonly IModHelper helper;

	private readonly IMonitor monitor;

	private readonly BoardConfig config;

	private SaveState state;

	public QuestService(QuestRepository quests, ConditionChecker conditions, IModHelper helper, IMonitor monitor, BoardConfig config, SaveState state)
	{
		this.quests = quests;
		this.conditions = conditions;
		this.helper = helper;
		this.monitor = monitor;
		this.config = config;
		this.state = state;
	}

	public void SetState(SaveState state)
	{
		this.state = state;
	}

	public void RefreshDailyOffers(bool force = false)
	{
		if (!Context.IsWorldReady)
		{
			return;
		}
		int today = (int)Game1.stats.DaysPlayed;
		if (!force && state.LastRefreshDay == today)
		{
			return;
		}
		state.LastRefreshDay = today;
		state.TodaysOfferIds.Clear();
		List<BoardQuest> candidates = (from boardQuest in quests.All.Values
			where !state.ActiveQuests.ContainsKey(boardQuest.Id)
			where conditions.CheckAll(boardQuest.Conditions)
			where !state.LastCompletedDay.TryGetValue(boardQuest.Id, out var value) || today - value >= Math.Max(0, boardQuest.CooldownDays)
			select boardQuest).ToList();
		Random random = new Random((int)((long)Game1.uniqueIDForThisGame + (long)today + 991001));
		while (state.TodaysOfferIds.Count < config.MaxOffersPerDay && candidates.Count > 0)
		{
			int totalWeight = Math.Max(1, candidates.Sum((BoardQuest boardQuest) => Math.Max(1, boardQuest.Weight)));
			int roll = random.Next(totalWeight);
			int running = 0;
			BoardQuest chosen = candidates[0];
			foreach (BoardQuest q in candidates)
			{
				running += Math.Max(1, q.Weight);
				if (roll < running)
				{
					chosen = q;
					break;
				}
			}
			state.TodaysOfferIds.Add(chosen.Id);
			candidates.Remove(chosen);
		}
		Save();
	}

	public List<BoardQuest> GetTodaysOffers()
	{
		RefreshDailyOffers();
		return (from id in state.TodaysOfferIds
			where quests.All.ContainsKey(id)
			select quests.All[id]).ToList();
	}

	public List<BoardQuest> GetActiveQuestDefs()
	{
		return (from id in state.ActiveQuests.Keys
			where quests.All.ContainsKey(id)
			select quests.All[id]).ToList();
	}

	public bool Accept(BoardQuest quest)
	{
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Expected O, but got Unknown
		if (state.ActiveQuests.ContainsKey(quest.Id))
		{
			return false;
		}
		int today = (int)Game1.stats.DaysPlayed;
		state.ActiveQuests[quest.Id] = new ActiveQuestState
		{
			QuestId = quest.Id,
			AcceptedDay = today,
			DueDay = today + Math.Max(1, quest.DeadlineDays),
			IsSpecialOrder = quest.Type.Equals("SpecialOrder", StringComparison.OrdinalIgnoreCase)
		};
		state.TodaysOfferIds.Remove(quest.Id);
		Game1.addHUDMessage(new HUDMessage(quest.AcceptText));
		Save();
		return true;
	}

	public void CheckVisitLocation(string locationName)
	{
		bool progressChanged = false;
		foreach (var (id, active) in state.ActiveQuests.ToList())
		{
			if (!quests.All.TryGetValue(id, out BoardQuest quest))
			{
				continue;
			}
			for (int i = 0; i < quest.Objectives.Count; i++)
			{
				ObjectiveDef obj = quest.Objectives[i];
				if (obj.Kind.Equals("VisitLocation", StringComparison.OrdinalIgnoreCase) && string.Equals(obj.Target, locationName, StringComparison.OrdinalIgnoreCase))
				{
					AddProgress(active, i, 1, obj.Count);
					progressChanged = true;
				}
			}
		}
		if (progressChanged)
		{
			Save();
		}
	}

	public void CheckTalkToNpc(string npcName)
	{
		foreach (var (id, active) in state.ActiveQuests.ToList())
		{
			if (!quests.All.TryGetValue(id, out BoardQuest quest))
			{
				continue;
			}
			for (int i = 0; i < quest.Objectives.Count; i++)
			{
				ObjectiveDef obj = quest.Objectives[i];
				if (obj.Kind.Equals("TalkToNpc", StringComparison.OrdinalIgnoreCase) && (string.Equals(obj.Target, npcName, StringComparison.OrdinalIgnoreCase) || (obj.Target == "$giver" && npcName == quest.Giver)))
				{
					AddProgress(active, i, 1, obj.Count);
				}
			}
		}
		TryCompleteReadyQuests();
	}

	public void CheckMonsterSlain(string monsterName)
	{
		bool progressChanged = false;
		foreach (var (id, active) in state.ActiveQuests.ToList())
		{
			if (!quests.All.TryGetValue(id, out BoardQuest quest))
			{
				continue;
			}
			for (int i = 0; i < quest.Objectives.Count; i++)
			{
				ObjectiveDef obj = quest.Objectives[i];
				if (obj.Kind.Equals("SlayMonster", StringComparison.OrdinalIgnoreCase) && string.Equals(obj.Target, monsterName, StringComparison.OrdinalIgnoreCase))
				{
					AddProgress(active, i, 1, obj.Count);
					progressChanged = true;
				}
			}
		}
		if (progressChanged)
		{
			Save();
		}
	}

	public void TryCompleteReadyQuests()
	{
		foreach (var (id, active) in state.ActiveQuests.ToList())
		{
			if (quests.All.TryGetValue(id, out BoardQuest quest) && ObjectivesMet(quest, active) && RemoveDeliveredItemsIfNeeded(quest))
			{
				CompleteQuest(quest);
			}
		}
		Save();
	}

	private bool ObjectivesMet(BoardQuest quest, ActiveQuestState active)
	{
		for (int i = 0; i < quest.Objectives.Count; i++)
		{
			ObjectiveDef obj = quest.Objectives[i];
			if (obj.Kind.Equals("DeliverItem", StringComparison.OrdinalIgnoreCase))
			{
				if (CountItem(obj.Target) < obj.Count)
				{
					return false;
				}
				continue;
			}
			int p;
			int progress = (active.ObjectiveProgress.TryGetValue(i, out p) ? p : 0);
			if (progress < Math.Max(1, obj.Count))
			{
				return false;
			}
		}
		return true;
	}

	private bool RemoveDeliveredItemsIfNeeded(BoardQuest quest)
	{
		foreach (ObjectiveDef obj in quest.Objectives)
		{
			if (obj.Kind.Equals("DeliverItem", StringComparison.OrdinalIgnoreCase) && obj.RemoveItemsOnComplete && !RemoveItem(obj.Target, obj.Count))
			{
				return false;
			}
		}
		return true;
	}

	private void CompleteQuest(BoardQuest quest)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		foreach (RewardDef reward in quest.Rewards)
		{
			ApplyReward(reward, quest);
		}
		if (!string.IsNullOrWhiteSpace(quest.CompletionMailFlag))
		{
			((NetHashSet<string>)(object)Game1.player.mailReceived).Add(quest.CompletionMailFlag);
		}
		state.ActiveQuests.Remove(quest.Id);
		state.LastCompletedDay[quest.Id] = (int)Game1.stats.DaysPlayed;
		Game1.addHUDMessage(new HUDMessage(quest.CompleteText));
	}

	private void ApplyReward(RewardDef reward, BoardQuest quest)
	{
		switch (reward.Kind.Trim().ToLowerInvariant())
		{
		case "money":
		{
			Farmer player = Game1.player;
			player.Money += reward.Amount;
			break;
		}
		case "friendship":
		{
			string npc = (string.IsNullOrWhiteSpace(reward.Target) ? quest.Giver : reward.Target);
			Game1.player.changeFriendship(reward.Amount, Game1.getCharacterFromName(npc, true, false));
			break;
		}
		case "item":
		{
			Item item = ItemRegistry.Create(reward.Target, Math.Max(1, reward.Count), 0, false);
			Game1.player.addItemByMenuIfNecessary(item, (behaviorOnItemSelect)null, false);
			break;
		}
		case "mail":
			if (!string.IsNullOrWhiteSpace(reward.Target))
			{
				((NetHashSet<string>)(object)Game1.player.mailReceived).Add(reward.Target);
			}
			break;
		}
	}

	private void AddProgress(ActiveQuestState active, int objectiveIndex, int amount, int max)
	{
		int p;
		int current = (active.ObjectiveProgress.TryGetValue(objectiveIndex, out p) ? p : 0);
		active.ObjectiveProgress[objectiveIndex] = Math.Min(Math.Max(1, max), current + amount);
	}

	private int CountItem(string qualifiedOrObjectId)
	{
		int total = 0;
		foreach (Item item in Game1.player.Items)
		{
			if (item != null && MatchesItem(item, qualifiedOrObjectId))
			{
				total += item.Stack;
			}
		}
		return total;
	}

	private bool RemoveItem(string qualifiedOrObjectId, int count)
	{
		if (CountItem(qualifiedOrObjectId) < count)
		{
			return false;
		}
		int left = count;
		for (int i = 0; i < Game1.player.Items.Count; i++)
		{
			if (left <= 0)
			{
				break;
			}
			Item item = Game1.player.Items[i];
			if (item != null && MatchesItem(item, qualifiedOrObjectId))
			{
				int take = Math.Min(left, item.Stack);
				item.Stack -= take;
				left -= take;
				if (item.Stack <= 0)
				{
					Game1.player.Items[i] = null;
				}
			}
		}
		return true;
	}

	private static bool MatchesItem(Item item, string qualifiedOrObjectId)
	{
		if (string.Equals(item.QualifiedItemId, qualifiedOrObjectId, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (string.Equals(item.ItemId, qualifiedOrObjectId, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (qualifiedOrObjectId.StartsWith("(O)"))
		{
			Object obj = (Object)(object)((item is Object) ? item : null);
			if (obj != null)
			{
				return ((Item)obj).ParentSheetIndex.ToString() == qualifiedOrObjectId.Substring(3);
			}
		}
		return false;
	}

	private void Save()
	{
		helper.Data.WriteSaveData<SaveState>("moonvillage-quest-board-state", state);
	}
}
