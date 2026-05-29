using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
			if (!CheckCondition(key, value))
			{
				return false;
			}
		}
		return true;
	}

	private bool CheckCondition(string rawKey, string rawValue)
	{
		string key = rawKey.Trim();
		bool invert = key.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
		if (invert)
		{
			key = key.Substring(4).Trim();
		}

		bool result = SplitAlternatives(rawValue).Any(value => Check(key.ToLowerInvariant(), value));
		return invert ? !result : result;
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
		case "npcexists":
			return Game1.getCharacterFromName(value, true, false) != null;
		case "friendshiplevel":
			return CheckFriendship(value);
		case "season":
			return SplitWords(value).Any(season => string.Equals(Game1.currentSeason, season, StringComparison.OrdinalIgnoreCase));
		case "date":
			return CheckDate(value);
		case "dayrange":
			return CheckNumberRange(Game1.dayOfMonth, value);
		case "daysofweek":
			return CheckDayOfWeek(value);
		case "weather":
			return CheckWeather(value);
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
		case "maxdaysplayed":
		{
			int d;
			return int.TryParse(value, out d) && Game1.stats.DaysPlayed <= d;
		}
		case "isplayermarried":
			return bool.TryParse(value, out var married) && Game1.player.isMarriedOrRoommates() == married;
		case "ismultiplayer":
			return bool.TryParse(value, out var multiplayer) && Context.IsMultiplayer == multiplayer;
		case "iscommunitycentercompleted":
			return bool.TryParse(value, out var ccComplete) && HasMail("ccIsComplete") == ccComplete;
		case "skilllevel":
			return CheckSkillLevel(value);
		case "mindeepestminelevel":
		{
			int level;
			return int.TryParse(value, out level) && player.deepestMineLevel >= level;
		}
		case "statatleast":
			return CheckStatAtLeast(value);
		case "hasitemeverobtained":
			return player.basicShipped.ContainsKey(value) || player.cookingRecipes.ContainsKey(value);
		case "gsq":
			return CheckGameStateQuery(value);
		case "hasmod":
		{
			IModHelper? helperRef = ModEntry.HelperRef;
			return helperRef != null && helperRef.ModRegistry.IsLoaded(value);
		}
		default:
			ModEntry.MonitorRef?.Log($"Unknown Moonvillage quest board condition '{key}' with value '{value}'. The condition will fail closed.", LogLevel.Warn);
			return false;
		}
	}

	private static IEnumerable<string> SplitAlternatives(string value)
	{
		return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	private static IEnumerable<string> SplitWords(string value)
	{
		return value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	private static bool CheckFriendship(string value)
	{
		string[] parts = value.Contains(':')
			? value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			: value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2 || !int.TryParse(parts[1], out var hearts))
		{
			return false;
		}
		return Game1.player.getFriendshipHeartLevelForNPC(parts[0]) >= hearts;
	}

	private static bool CheckDate(string value)
	{
		string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2 || !string.Equals(Game1.currentSeason, parts[0], StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return CheckNumberRange(Game1.dayOfMonth, parts[1]);
	}

	private static bool CheckNumberRange(int current, string range)
	{
		string[] parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 1)
		{
			return int.TryParse(parts[0], out var day) && current == day;
		}
		return parts.Length == 2
			&& int.TryParse(parts[0], out var start)
			&& int.TryParse(parts[1], out var end)
			&& current >= start
			&& current <= end;
	}

	private static bool CheckDayOfWeek(string value)
	{
		string[] names = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
		int dayIndex = ((int)Game1.stats.DaysPlayed + 1) % 7;
		foreach (string token in SplitWords(value))
		{
			if (int.TryParse(token, out var numeric) && numeric == dayIndex)
			{
				return true;
			}
			if (names[dayIndex].StartsWith(token, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static bool CheckWeather(string value)
	{
		foreach (string token in SplitWords(value))
		{
			if (token.Equals("rain", StringComparison.OrdinalIgnoreCase) && Game1.isRaining)
			{
				return true;
			}
			if (token.Equals("storm", StringComparison.OrdinalIgnoreCase) && Game1.isLightning)
			{
				return true;
			}
			if (token.Equals("snow", StringComparison.OrdinalIgnoreCase) && Game1.isSnowing)
			{
				return true;
			}
			if ((token.Equals("sun", StringComparison.OrdinalIgnoreCase) || token.Equals("clear", StringComparison.OrdinalIgnoreCase)) && !Game1.isRaining && !Game1.isLightning && !Game1.isSnowing)
			{
				return true;
			}
		}
		return false;
	}

	private static bool CheckSkillLevel(string value)
	{
		string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2 || !int.TryParse(parts[1], out var minLevel))
		{
			return false;
		}
		int level = parts[0].ToLowerInvariant() switch
		{
			"farming" => Game1.player.FarmingLevel,
			"fishing" => Game1.player.FishingLevel,
			"mining" => Game1.player.MiningLevel,
			"foraging" => Game1.player.ForagingLevel,
			"combat" => Game1.player.CombatLevel,
			"luck" => Game1.player.LuckLevel,
			_ => -1
		};
		return level >= minLevel;
	}

	private static bool CheckStatAtLeast(string value)
	{
		string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2 || !uint.TryParse(parts[1], out var required))
		{
			return false;
		}
		FieldInfo? field = typeof(Stats).GetField(parts[0], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
		object? raw = field?.GetValue(Game1.stats);
		return raw switch
		{
			int v => v >= required,
			uint v => v >= required,
			long v => v >= required,
			ulong v => v >= required,
			_ => false
		};
	}

	private static bool CheckGameStateQuery(string value)
	{
		Type? type = typeof(Game1).Assembly.GetType("StardewValley.GameStateQuery");
		MethodInfo? method = type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.FirstOrDefault(m => m.Name == "CheckConditions" && m.GetParameters().Length >= 1);
		if (method == null)
		{
			return false;
		}

		ParameterInfo[] parameters = method.GetParameters();
		object?[] args = new object?[parameters.Length];
		args[0] = value;
		for (int i = 1; i < args.Length; i++)
		{
			Type parameterType = parameters[i].ParameterType;
			if (parameterType == typeof(GameLocation))
			{
				args[i] = Game1.currentLocation;
			}
			else if (parameterType == typeof(Farmer))
			{
				args[i] = Game1.player;
			}
			else
			{
				args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
			}
		}

		return method.Invoke(null, args) is bool result && result;
	}

	private static bool HasMail(string flag)
	{
		return ((NetHashSet<string>)(object)Game1.MasterPlayer.mailReceived).Contains(flag)
			|| ((NetHashSet<string>)(object)Game1.MasterPlayer.mailForTomorrow).Contains(flag);
	}
}
