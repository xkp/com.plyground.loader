using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlygroundItemFeatureInstaller
{
	private const string FeatureFolderPath = "Assets/plyground/Features";
	private const string PendingSessionKey = "Plyground.PendingItemFeatures";

	[Serializable]
	private class PendingFeatureState
	{
		public List<PendingTargetState> targets = new List<PendingTargetState>();
	}

	[Serializable]
	private class PendingTargetState
	{
		public string targetName;
		public List<string> classNames = new List<string>();
	}

	static PlygroundItemFeatureInstaller()
	{
		EditorApplication.delayCall += TryAttachPendingFeatures;
	}

	public static void Install(GameObject target, IEnumerable<GameFeature> features)
	{
		if (target == null)
			return;

		var itemFeatures = (features ?? Enumerable.Empty<GameFeature>())
			.Where(feature => feature != null && !feature.IsGlobal && !string.IsNullOrWhiteSpace(feature.Code))
			.ToList();

		if (itemFeatures.Count == 0)
		{
			RemovePendingFeatures(target.name);
			return;
		}

		Directory.CreateDirectory(Path.GetFullPath(FeatureFolderPath));

		var classNames = new List<string>();
		var wroteScript = false;
		foreach (var feature in itemFeatures)
		{
			var className = PlygroundGlobalFeatureInstaller.ResolveClassName(feature);
			classNames.Add(className);

			var featurePath = $"{FeatureFolderPath}/{className}.cs";
			if (!File.Exists(featurePath) || File.ReadAllText(featurePath) != feature.Code)
			{
				File.WriteAllText(featurePath, feature.Code);
				wroteScript = true;
			}
		}

		StorePendingFeatures(target.name, classNames);
		if (wroteScript)
			AssetDatabase.Refresh();

		TryAttachPendingFeatures();
	}

	private static void TryAttachPendingFeatures()
	{
		var pending = ReadPendingFeatures();
		if (pending.targets == null || pending.targets.Count == 0)
			return;

		var remainingTargets = new List<PendingTargetState>();
		var attachedAny = false;

		foreach (var pendingTarget in pending.targets)
		{
			if (pendingTarget == null || string.IsNullOrWhiteSpace(pendingTarget.targetName))
				continue;

			var target = GameObject.Find(pendingTarget.targetName);
			if (target == null)
			{
				remainingTargets.Add(pendingTarget);
				continue;
			}

			var remainingClassNames = new List<string>();
			foreach (var className in (pendingTarget.classNames ?? new List<string>()).Distinct(StringComparer.Ordinal))
			{
				var featureType = PlygroundGlobalFeatureInstaller.FindFeatureType(className);
				if (featureType == null)
				{
					remainingClassNames.Add(className);
					continue;
				}

				if (target.GetComponent(featureType) == null)
				{
					target.AddComponent(featureType);
					attachedAny = true;
				}
			}

			if (remainingClassNames.Count > 0)
			{
				remainingTargets.Add(new PendingTargetState
				{
					targetName = pendingTarget.targetName,
					classNames = remainingClassNames
				});
			}
		}

		if (remainingTargets.Count == 0)
		{
			ClearPendingFeatures();
			if (attachedAny)
				EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

			return;
		}

		StorePendingFeatures(remainingTargets);
		if (attachedAny)
			EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
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

	private static void StorePendingFeatures(string targetName, List<string> classNames)
	{
		var pending = ReadPendingFeatures();
		var targets = pending.targets ?? new List<PendingTargetState>();

		targets.RemoveAll(target => string.Equals(target.targetName, targetName, StringComparison.Ordinal));
		targets.Add(new PendingTargetState
		{
			targetName = targetName,
			classNames = (classNames ?? new List<string>())
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.Ordinal)
				.ToList()
		});

		StorePendingFeatures(targets);
	}

	private static void StorePendingFeatures(List<PendingTargetState> targets)
	{
		var state = new PendingFeatureState
		{
			targets = (targets ?? new List<PendingTargetState>())
				.Where(target => target != null && !string.IsNullOrWhiteSpace(target.targetName))
				.ToList()
		};

		SessionState.SetString(PendingSessionKey, JsonUtility.ToJson(state));
	}

	private static void RemovePendingFeatures(string targetName)
	{
		var pending = ReadPendingFeatures();
		if (pending.targets == null || pending.targets.Count == 0)
			return;

		pending.targets.RemoveAll(target => string.Equals(target.targetName, targetName, StringComparison.Ordinal));
		if (pending.targets.Count == 0)
		{
			ClearPendingFeatures();
			return;
		}

		StorePendingFeatures(pending.targets);
	}

	private static void ClearPendingFeatures()
	{
		SessionState.EraseString(PendingSessionKey);
	}
}
