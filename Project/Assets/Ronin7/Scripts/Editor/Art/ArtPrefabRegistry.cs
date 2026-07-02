using System;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Tries to instantiate a prefab at the given path. If the prefab is missing (true during
    /// greybox phases before subject agents land their art), invokes the supplied greybox factory
    /// so scene builders keep producing a valid GameObject either way.
    /// Editor-only: relies on AssetDatabase / PrefabUtility.
    /// </summary>
    public static class ArtPrefabRegistry
    {
        public static GameObject TryInstantiateOrFallback(string prefabPath, Func<GameObject> greyboxFactory, Transform parent)
        {
            if (greyboxFactory == null) throw new ArgumentNullException(nameof(greyboxFactory));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Debug.Log($"[ArtPrefabRegistry] prefab hit: {prefabPath}");
                return instance;
            }

            var greybox = greyboxFactory();
            if (greybox != null && parent != null && greybox.transform.parent != parent)
                greybox.transform.SetParent(parent, false);
            Debug.Log($"[ArtPrefabRegistry] greybox fallback: {prefabPath}");
            return greybox;
        }
    }
}
