using System.Collections.Generic;

namespace MoonvillageQuestBoard;

public sealed class ObjectiveDef
{
	public string Name { get; set; } = "";

	public string Kind { get; set; } = "VisitLocation";

	public string Target { get; set; } = "";

	public int Count { get; set; } = 1;

	public bool RemoveItemsOnComplete { get; set; } = true;

	public List<string> Requires { get; set; } = new List<string>();
}
