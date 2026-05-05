using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlygroundGlobalFeatureInstaller
{
	private const string GlobalObjectName = "plyground";
	private const string FeatureFolderPath = "Assets/plyground/Features";
	private const string PendingSessionKey = "Plyground.PendingGlobalFeatures";

	[Serializable]
	private class PendingFeatureState
	{
		public List<string> classNames = new List<string>();
	}

	static PlygroundGlobalFeatureInstaller()
	{
		EditorApplication.delayCall += TryAttachPendingFeatures;
	}

	public static void Install(IEnumerable<GameFeature> features)
	{
		var globalFeatures = (features ?? Enumerable.Empty<GameFeature>())
			.Where(feature => feature != null && feature.IsGlobal && !string.IsNullOrWhiteSpace(feature.Code))
			.ToList();

		if (globalFeatures.Count == 0)
		{
			ClearPendingFeatures();
			return;
		}

		Directory.CreateDirectory(Path.GetFullPath(FeatureFolderPath));
		GetOrCreatePlygroundObject();

		var classNames = new List<string>();
		var wroteScript = false;
		foreach (var feature in globalFeatures)
		{
			var className = ResolveClassName(feature);
			classNames.Add(className);

			var featurePath = $"{FeatureFolderPath}/{className}.cs";
			if (!File.Exists(featurePath) || File.ReadAllText(featurePath) != feature.Code)
			{
				File.WriteAllText(featurePath, feature.Code);
				wroteScript = true;
			}
		}

		StorePendingFeatures(classNames);
		if (wroteScript)
			AssetDatabase.Refresh();

		TryAttachPendingFeatures();
	}

	private static void TryAttachPendingFeatures()
	{
		var pending = ReadPendingFeatures();
		if (pending.classNames == null || pending.classNames.Count == 0)
			return;

		var plyground = GetOrCreatePlygroundObject();
		var remaining = new List<string>();
		foreach (var className in pending.classNames.Distinct(StringComparer.Ordinal))
		{
			var featureType = FindFeatureType(className);
			if (featureType == null)
			{
				remaining.Add(className);
				continue;
			}

			if (plyground.GetComponent(featureType) == null)
				plyground.AddComponent(featureType);
		}

		if (remaining.Count == 0)
		{
			ClearPendingFeatures();
			EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
			return;
		}

		StorePendingFeatures(remaining);
	}

	private static Type FindFeatureType(string className)
	{
		return TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
			.FirstOrDefault(type => type.Name == className);
	}

	private static GameObject GetOrCreatePlygroundObject()
	{
		var plyground = GameObject.Find(GlobalObjectName);
		if (plyground != null)
			return plyground;

		plyground = new GameObject(GlobalObjectName);
		EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
		return plyground;
	}

	private static string ResolveClassName(GameFeature feature)
	{
		var directMonoBehaviourMatch = Regex.Match(
			feature.Code,
			@"class\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*MonoBehaviour\b",
			RegexOptions.Multiline);
		if (directMonoBehaviourMatch.Success)
			return directMonoBehaviourMatch.Groups[1].Value;

		var anyClassMatch = Regex.Match(
			feature.Code,
			@"class\s+([A-Za-z_][A-Za-z0-9_]*)\b",
			RegexOptions.Multiline);
		if (anyClassMatch.Success)
			return anyClassMatch.Groups[1].Value;

		return SanitizeIdentifier(feature.Name, "PlygroundFeature");
	}

	private static string SanitizeIdentifier(string value, string fallback)
	{
		if (string.IsNullOrWhiteSpace(value))
			return fallback;

		var cleaned = Regex.Replace(value, @"[^A-Za-z0-9_]+", string.Empty);
		if (string.IsNullOrWhiteSpace(cleaned))
			return fallback;

		if (!char.IsLetter(cleaned[0]) && cleaned[0] != '_')
			cleaned = "_" + cleaned;

		return cleaned;
	}

	private static PendingFeatureState ReadPendingFeatures()
	{
		var json = SessionState.GetString(PendingSessionKey, string.Empty);
		if (string.IsNullOrWhiteSpace(json))
			return new PendingFeatureState();

		try
		{
			return JsonUtility.FromJson<PendingFeatureState>(json) ?? new PendingFeatureState();
		}
		catch
		{
			return new PendingFeatureState();
		}
	}

	private static void StorePendingFeatures(List<string> classNames)
	{
		var state = new PendingFeatureState
		{
			classNames = classNames
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.Ordinal)
				.ToList()
		};
		SessionState.SetString(PendingSessionKey, JsonUtility.ToJson(state));
	}

	private static void ClearPendingFeatures()
	{
		SessionState.EraseString(PendingSessionKey);
	}
}
