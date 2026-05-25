namespace MoonvillageQuestBoard;

internal static class I18n
{
	public static string Text(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		if (!value.StartsWith("i18n:"))
		{
			return value;
		}
		return ModEntry.HelperRef?.Translation.Get(value.Substring(5)).ToString() ?? value;
	}
}
