#if UNITY_EDITOR

using Game.Level.Runtime;
using UnityEditor;
using UnityEngine;

public static class RoadSegmentBakeMenu
{
	[MenuItem("Tools/Road/Bake all road segments")]
	private static void BakeAll()
	{
		int baked = 0, failed = 0;

		foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (asset == null || asset.GetComponent<RoadSegment>() == null) continue;

			var root = PrefabUtility.LoadPrefabContents(path);
			try
			{
				if (root.GetComponent<RoadSegment>().BakeGrid()) baked++;
				else failed++;
				PrefabUtility.SaveAsPrefabAsset(root, path);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		Debug.Log($"[RoadSegmentBake] Готово: {baked}, с ошибками: {failed}");
	}
}

#endif
