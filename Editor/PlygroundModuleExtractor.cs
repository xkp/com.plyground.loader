using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;

public static class PlygroundModuleExtractor
{
	public static List<string> ExtractModuleIdsFromFile(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
			return new List<string>();

		var jsonContent = File.ReadAllText(filePath);
		var root = JObject.Parse(jsonContent);
		return ExtractModuleIds(root);
	}

	public static List<string> ExtractModuleIds(JObject source)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (source == null)
			return new List<string>();

		// Legacy support
		AddRange(result, source["modules"] as JArray);

		AddRange(result, source["coreModules"] as JArray);
		Add(result, source["selectedGame"]?.ToString());
		AddRange(result, source["selectedCharacters"] as JArray);
		AddRange(result, source["selectedNature"] as JArray);
		AddRange(result, source["selectedProps"] as JArray);
		AddRange(result, source["selectedUserAssets"] as JArray);

		Add(result, source.SelectToken("avatar.moduleId")?.ToString());

		AddRangeFromObjects(result, source["npcs"] as JArray, "moduleId");
		AddRangeFromObjects(result, source["gameFeatures"] as JArray, "moduleId");

		AddStoryModules(result, source.SelectToken("storyMap.gameplay") as JObject);
		AddStoryModules(result, source.SelectToken("storyMap.environment") as JObject);
		AddStoryModules(result, source.SelectToken("storyMap.characters") as JObject);
		AddStoryModules(result, source.SelectToken("storyMap.avatar") as JObject);
		AddStoryModules(result, source.SelectToken("storyMap.vegetation") as JObject);
		AddStoryModules(result, source.SelectToken("storyMap.props") as JObject);

		return result
			.OrderBy(moduleId => moduleId, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static void AddStoryModules(HashSet<string> moduleIds, JObject section)
	{
		if (section == null)
			return;

		AddRange(moduleIds, section["modules"] as JArray);
	}

	private static void AddRangeFromObjects(HashSet<string> moduleIds, JArray items, string propertyName)
	{
		if (items == null)
			return;

		foreach (var item in items.OfType<JObject>())
			Add(moduleIds, item[propertyName]?.ToString());
	}

	private static void AddRange(HashSet<string> moduleIds, JArray values)
	{
		if (values == null)
			return;

		foreach (var value in values)
			Add(moduleIds, value?.ToString());
	}

	private static void Add(HashSet<string> moduleIds, string value)
	{
		if (moduleIds == null || string.IsNullOrWhiteSpace(value))
			return;

		moduleIds.Add(value.Trim());
	}
}
