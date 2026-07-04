using System.Collections.Generic;
using System.IO;
using Ronin7.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Editor wrapper around <see cref="ProceduralAudioSynth"/>: bakes its float[] output into real
    /// AudioClip assets under Assets/Ronin7/Audio/Generated/, and wires them onto the
    /// <see cref="ProximityAmbienceLayer"/> / <see cref="NpcFootstepCadence"/> instances in the open
    /// scene. Both menu items are idempotent — regenerating overwrites clip data in place (same
    /// asset GUID survives, so existing references don't break) and assigning skips components that
    /// already point at the generated clips.
    /// </summary>
    public static class ProceduralAudioClipBuilder
    {
        private const int SampleRate = 44100;
        private const float AmbienceDuration = 8f;
        private const float FootstepDuration = 0.25f;

        private const string AmbienceFolder = "Assets/Ronin7/Audio/Generated/Ambience";
        private const string FootstepFolder = "Assets/Ronin7/Audio/Generated/Footstep";

        // One bed per theme. Seeds are arbitrary fixed constants -- only their stability across runs matters.
        private static readonly (string name, AmbienceTheme theme, int seed)[] AmbienceJobs =
        {
            ("Ambience_HangarHum", AmbienceTheme.HangarHum, 201),
            ("Ambience_GardenWind", AmbienceTheme.GardenWind, 202),
            ("Ambience_DreadDrone", AmbienceTheme.DreadDrone, 203),
        };

        // NpcFootstepCadence round-robins whatever clip list it's given; the scenes this ships into
        // today (Medbay/Dock/LabCore/ThroneCore) are all metal/stone ship-interior decks, so the
        // "hard" surface variants are what Assign Generated Clips wires up by default. Soft variants
        // are generated too (a future garden/carpeted zone can reference them) but aren't auto-assigned.
        private static readonly (string name, int seed)[] FootstepHardJobs =
        {
            ("Footstep_Hard_1", 301),
            ("Footstep_Hard_2", 302),
            ("Footstep_Hard_3", 303),
        };

        private static readonly (string name, int seed)[] FootstepSoftJobs =
        {
            ("Footstep_Soft_1", 311),
            ("Footstep_Soft_2", 312),
            ("Footstep_Soft_3", 313),
        };

        [MenuItem("Tools/Space Samurai/Art/Generate Ambience & Footstep Clips")]
        public static void GenerateAmbienceAndFootstepClips()
        {
            EnsureFolder(AmbienceFolder);
            EnsureFolder(FootstepFolder);

            int ambienceCount = 0, footstepCount = 0;
            foreach (var job in AmbienceJobs)
            {
                float[] samples = ProceduralAudioSynth.GenerateAmbienceBed(SampleRate, AmbienceDuration, job.seed, job.theme);
                CreateOrUpdateClip($"{AmbienceFolder}/{job.name}.asset", job.name, samples);
                ambienceCount++;
            }
            foreach (var job in FootstepHardJobs)
            {
                float[] samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, FootstepDuration, job.seed, FootstepSurface.Hard);
                CreateOrUpdateClip($"{FootstepFolder}/{job.name}.asset", job.name, samples);
                footstepCount++;
            }
            foreach (var job in FootstepSoftJobs)
            {
                float[] samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, FootstepDuration, job.seed, FootstepSurface.Soft);
                CreateOrUpdateClip($"{FootstepFolder}/{job.name}.asset", job.name, samples);
                footstepCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ProceduralAudioClipBuilder] Generated {ambienceCount} ambience bed(s) -> {AmbienceFolder}/ " +
                      $"and {footstepCount} footstep variant(s) -> {FootstepFolder}/.");
        }

        [MenuItem("Tools/Space Samurai/Art/Assign Generated Clips (Open Scene)")]
        public static void AssignGeneratedClips()
        {
            var ambienceClips = new Dictionary<AmbienceTheme, AudioClip>();
            foreach (var job in AmbienceJobs)
                ambienceClips[job.theme] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AmbienceFolder}/{job.name}.asset");

            var footstepClips = new List<AudioClip>();
            foreach (var job in FootstepHardJobs)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{FootstepFolder}/{job.name}.asset");
                if (clip != null) footstepClips.Add(clip);
            }

            if (footstepClips.Count == 0 || ambienceClips.Count == 0)
            {
                Debug.LogError("[ProceduralAudioClipBuilder] No generated clips found at " +
                    $"{AmbienceFolder}/ or {FootstepFolder}/ -- run 'Generate Ambience & Footstep Clips' first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Undo.SetCurrentGroupName("Assign Generated Clips");
            int undoGroup = Undo.GetCurrentGroup();
            int ambienceAssigned = 0, footstepAssigned = 0;

            foreach (GameObject root in roots)
            {
                foreach (var layer in root.GetComponentsInChildren<ProximityAmbienceLayer>(true))
                {
                    var source = new SerializedObject(layer).FindProperty("source").objectReferenceValue as AudioSource;
                    if (source == null) continue;

                    AmbienceTheme theme = InferTheme(layer.gameObject.name);
                    if (!ambienceClips.TryGetValue(theme, out var clip) || clip == null) continue;
                    if (source.clip == clip && source.loop) continue; // already assigned

                    Undo.RecordObject(source, "Assign Generated Clips");
                    source.clip = clip;
                    source.loop = true;
                    EditorUtility.SetDirty(source);
                    ambienceAssigned++;
                }

                foreach (var cadence in root.GetComponentsInChildren<NpcFootstepCadence>(true))
                {
                    var so = new SerializedObject(cadence);
                    var clipsProp = so.FindProperty("footstepClips");

                    bool alreadyAssigned = clipsProp.arraySize == footstepClips.Count;
                    if (alreadyAssigned)
                    {
                        for (int i = 0; i < footstepClips.Count; i++)
                        {
                            if (clipsProp.GetArrayElementAtIndex(i).objectReferenceValue != footstepClips[i])
                            {
                                alreadyAssigned = false;
                                break;
                            }
                        }
                    }
                    if (alreadyAssigned) continue;

                    Undo.RecordObject(cadence, "Assign Generated Clips");
                    clipsProp.arraySize = footstepClips.Count;
                    for (int i = 0; i < footstepClips.Count; i++)
                        clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = footstepClips[i];
                    so.ApplyModifiedProperties();
                    footstepAssigned++;
                }
            }

            if (ambienceAssigned + footstepAssigned > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[ProceduralAudioClipBuilder] '{scene.name}': assigned ambience clip to {ambienceAssigned}, " +
                      $"footstep clips to {footstepAssigned} component(s). Scene marked dirty -- save manually.");
        }

        /// <summary>Keys an ambience theme off the ProximityAmbienceLayer GameObject's own name --
        /// the only naming hook available (the component has no theme/zone field of its own).</summary>
        private static AmbienceTheme InferTheme(string gameObjectName)
        {
            string n = gameObjectName.ToLowerInvariant();
            if (n.Contains("dock") || n.Contains("wind") || n.Contains("garden")) return AmbienceTheme.GardenWind;
            if (n.Contains("throne") || n.Contains("dread") || n.Contains("vault")) return AmbienceTheme.DreadDrone;
            return AmbienceTheme.HangarHum;
        }

        /// <summary>Refills an existing clip's PCM data in place when its format still matches
        /// (preserves the asset GUID -- scene/prefab references survive); otherwise recreates it.</summary>
        private static void CreateOrUpdateClip(string path, string clipName, float[] samples)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null && existing.samples == samples.Length && existing.channels == 1 && existing.frequency == SampleRate)
            {
                existing.SetData(samples, 0);
                EditorUtility.SetDirty(existing);
                return;
            }

            if (existing != null) AssetDatabase.DeleteAsset(path);

            var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            AssetDatabase.CreateAsset(clip, path);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }
    }
}
