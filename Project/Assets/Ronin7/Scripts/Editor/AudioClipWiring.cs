using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ronin7.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Auto-assigns the AudioClip assets under <see cref="AudioFolder"/> to the matching serialized
    /// fields on the persistent <see cref="AudioDirector"/>, so the ~dozen clips don't have to be
    /// dragged into the Inspector by hand (and re-dragged every time a scene rebuild nulls them).
    ///
    /// Matching is by name: a clip file's name is matched case-insensitively to a field name
    /// (e.g. <c>ShipGunFire.wav</c> → the <c>shipGunFire</c> field). Because it reflects over the
    /// AudioClip fields rather than hard-coding them, any clip field added to AudioDirector later is
    /// picked up automatically the moment a matching file exists. Fields without a matching file are
    /// left untouched (so a hand-assigned clip with no file on disk is preserved).
    /// </summary>
    public static class AudioClipWiring
    {
        private const string AudioFolder = "Assets/Ronin7/Audio";
        private const string BootScenePath = "Assets/Ronin7/Scenes/Phase6_Boot.unity";

        [MenuItem("Tools/Space Samurai/Audio/Wire Audio Clips To AudioDirector", priority = 30)]
        public static void WireOpenSceneMenu()
        {
            var director = UnityEngine.Object.FindAnyObjectByType<AudioDirector>(FindObjectsInactive.Include);

            // The AudioDirector lives in Phase6_Boot (a DontDestroyOnLoad singleton). If it isn't in
            // the open scene, offer to jump there — saving any current work first.
            if (director == null)
            {
                bool open = EditorUtility.DisplayDialog("Wire Audio Clips",
                    "No AudioDirector in the open scene. It's created in Phase6_Boot.\n\n" +
                    "Open Phase6_Boot and wire it there?", "Open Phase6_Boot", "Cancel");
                if (!open) return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
                director = UnityEngine.Object.FindAnyObjectByType<AudioDirector>(FindObjectsInactive.Include);
                if (director == null)
                {
                    EditorUtility.DisplayDialog("Wire Audio Clips",
                        "Phase6_Boot has no AudioDirector. Build it first via " +
                        "Tools/Space Samurai/Build Phase 6 Boot Scene.", "OK");
                    return;
                }
            }

            var (assigned, missing) = Wire(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            EditorSceneManager.SaveScene(director.gameObject.scene);

            Debug.Log($"[AudioClipWiring] Assigned {assigned} clip(s) to AudioDirector in " +
                      $"'{director.gameObject.scene.name}' and saved the scene." +
                      (missing.Count > 0
                          ? $" No matching file in {AudioFolder} for: {string.Join(", ", missing)} " +
                            "(left as-is — add a same-named clip to wire them)."
                          : " Every clip field was matched."), director);
        }

        /// <summary>
        /// Assigns matching clips onto <paramref name="director"/> via SerializedObject (so it marks
        /// dirty and supports undo). Returns the count assigned and the names of clip fields that had
        /// no matching file. Shared by the menu item and the Boot-scene builder.
        /// </summary>
        public static (int assigned, List<string> missing) Wire(AudioDirector director)
        {
            var byName = LoadClipsByName();

            var clipFields = typeof(AudioDirector)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.FieldType == typeof(AudioClip))
                .ToArray();

            int assigned = 0;
            var missing = new List<string>();
            var so = new SerializedObject(director);
            foreach (var field in clipFields)
            {
                // Skip non-serialized AudioClip fields (e.g. AudioDirector's runtime ambienceTarget/
                // musicTarget): FindProperty returns null for anything Unity doesn't serialize.
                var prop = so.FindProperty(field.Name);
                if (prop == null) continue;

                if (byName.TryGetValue(field.Name, out var clip))
                {
                    prop.objectReferenceValue = clip;
                    assigned++;
                }
                else missing.Add(field.Name);
            }
            so.ApplyModifiedProperties();
            return (assigned, missing);
        }

        /// <summary>All AudioClips under the Audio folder, keyed by file name (case-insensitive).</summary>
        private static Dictionary<string, AudioClip> LoadClipsByName()
        {
            var byName = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                Debug.LogWarning($"[AudioClipWiring] Audio folder not found: {AudioFolder}. " +
                                 "Drop your .wav/.mp3/.ogg clips there (named after the AudioDirector fields).");
                return byName;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (byName.ContainsKey(name))
                    Debug.LogWarning($"[AudioClipWiring] Duplicate clip name '{name}' in {AudioFolder}; " +
                                     $"keeping the first, ignoring {path}.");
                else byName[name] = clip;
            }
            return byName;
        }
    }
}
