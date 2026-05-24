using System;
using System.Collections.Generic;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;

namespace MoonvillageQuestBoard;

public sealed class ConditionChecker
{
	public bool CheckAll(Dictionary<string, string>? conditions)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return true;
		}
		foreach (KeyValuePair<string, string> condition in conditions)
		{
			condition.Deconstruct(out var key, out var value);
			string keyRaw = key;
			string value2 = value;
			string key2 = keyRaw.Trim().ToLowerInvariant();
			if (!Check(key2, value2))
			{
				return false;
			}
		}
		return true;
	}

	private bool Check(string key, string value)
	{
		Farmer player = Game1.player;
		switch (key)
		{
		case "eventseen":
			return ((NetHashSet<string>)(object)player.eventsSeen).Contains(value);
		case "mailreceived":
			return ((NetHashSet<string>)(object)player.mailReceived).Contains(value) || ((NetHashSet<string>)(object)player.mailForTomorrow).Contains(value);
		case "npcmet":
			return ((NetDictionary<string, Friendship, NetRef<Friendship>, SerializableDictionary<string, Friendship>, NetStringDictionary<Friendship, NetRef<Friendship>>>)(object)player.friendshipData).ContainsKey(value);
		case "friendshiplevel":
			return CheckFriendship(value);
		case "season":
			return string.Equals(Game1.currentSeason, value, StringComparison.OrdinalIgnoreCase);
		case "year":
		{
			int y;
			return int.TryParse(value, out y) && Game1.year >= y;
		}
		case "mindaysplayed":
		{
			int d;
			return int.TryParse(value, out d) && Game1.stats.DaysPlayed >= d;
		}
		case "hasmod":
		{
			IModHelper? helperRef = ModEntry.HelperRef;
			return helperRef != null && helperRef.ModRegistry.IsLoaded(value);
		}
		default:
			return true;
		}
	}

	private static bool CheckFriendship(string value)
	{
		string[] parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2 || !int.TryParse(parts[1], out var hearts))
		{
			return false;
		}
		return Game1.player.getFriendshipHeartLevelForNPC(parts[0]) >= hearts;
	}
}
