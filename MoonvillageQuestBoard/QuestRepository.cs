using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace MoonvillageQuestBoard;

public sealed class QuestRepository
{
	private readonly IMonitor monitor;

	private readonly IModHelper helper;

	private readonly BoardConfig config;

	public Dictionary<string, BoardQuest> All { get; } = new Dictionary<string, BoardQuest>();

	public QuestRepository(IModHelper helper, IMonitor monitor, BoardConfig config)
	{
		this.helper = helper;
		this.monitor = monitor;
		this.config = config;
	}

	public void Reload()
	{
		All.Clear();
		LoadQuestFile(config.QuestsFile, specialOrders: false);
		LoadQuestFile(config.SpecialOrdersFile, specialOrders: true);
		monitor.Log($"Loaded {All.Count} Moonvillage board entries.", (LogLevel)2);
	}

	private void LoadQuestFile(string path, bool specialOrders)
	{
		try
		{
			string key;
			BoardQuest value;
			if (specialOrders)
			{
				SpecialOrdersDataFile file = helper.Data.ReadJsonFile<SpecialOrdersDataFile>(path);
				if (file?.SpecialOrders == null)
				{
					return;
				}
				{
					foreach (KeyValuePair<string, BoardQuest> specialOrder in file.SpecialOrders)
					{
						specialOrder.Deconstruct(out key, out value);
						string id = key;
						BoardQuest quest = value;
						Add(id, quest, special: true);
					}
					return;
				}
			}
			QuestDataFile file2 = helper.Data.ReadJsonFile<QuestDataFile>(path);
			if (file2?.Quests == null)
			{
				return;
			}
			foreach (KeyValuePair<string, BoardQuest> quest3 in file2.Quests)
			{
				quest3.Deconstruct(out key, out value);
				string id2 = key;
				BoardQuest quest2 = value;
				Add(id2, quest2, special: false);
			}
		}
		catch (Exception value2)
		{
			monitor.Log($"Failed loading {path}: {value2}", (LogLevel)4);
		}
	}

	private void Add(string id, BoardQuest quest, bool special)
	{
		quest.Id = (string.IsNullOrWhiteSpace(quest.Id) ? id : quest.Id);
		quest.Type = (special ? "SpecialOrder" : quest.Type);
		All[quest.Id] = quest;
	}
}
