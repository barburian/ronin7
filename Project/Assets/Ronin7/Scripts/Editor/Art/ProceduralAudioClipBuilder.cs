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
        private const string CombatHitFolder = "Assets/Ronin7/Audio/Generated/CombatHits";

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

        // Placeholder combat-feedback one-shots for the roguelike run arena (AudioDirector's
        // attackWindup/enemyDefeated fields — see the "roguelike audio" pass). Reuses
        // GenerateFootstepThud as a generic short filtered-noise-burst synth (it's a percussive
        // attack/decay shape, not literally a footstep) rather than inventing a second DSP core for
        // what is mechanically the same primitive. Windup is a short, sharp "hard" tick (readable
        // telegraph); enemy-defeated is a longer, darker "soft" thud (a confirming kill cue). Tonal
        // content (music, reward chimes) is deliberately NOT generated this way — see the handoff
        // notes on why placeholder music was left unassigned instead.
        private static readonly (string name, float duration, int seed, FootstepSurface surface)[] CombatHitJobs =
        {
            ("Hit_AttackWindup", 0.15f, 401, FootstepSurface.Hard),
            ("Hit_EnemyDefeated", 0.35f, 402, FootstepSurface.Soft),
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
                CreateOrUpdateClip($"{AmbienceFolder}/{job.name}.wav", samples);
                ambienceCount++;
            }
            foreach (var job in FootstepHardJobs)
            {
                float[] samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, FootstepDuration, job.seed, FootstepSurface.Hard);
                CreateOrUpdateClip($"{FootstepFolder}/{job.name}.wav", samples);
                footstepCount++;
            }
            foreach (var job in FootstepSoftJobs)
            {
                float[] samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, FootstepDuration, job.seed, FootstepSurface.Soft);
                CreateOrUpdateClip($"{FootstepFolder}/{job.name}.wav", samples);
                footstepCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ProceduralAudioClipBuilder] Generated {ambienceCount} ambience bed(s) -> {AmbienceFolder}/ " +
                      $"and {footstepCount} footstep variant(s) -> {FootstepFolder}/.");
        }

        [MenuItem("Tools/Space Samurai/Art/Generate Combat Hit Clips")]
        public static void GenerateCombatHitClips()
        {
            EnsureFolder(CombatHitFolder);

            int count = 0;
            foreach (var job in CombatHitJobs)
            {
                float[] samples = ProceduralAudioSynth.GenerateFootstepThud(SampleRate, job.duration, job.seed, job.surface);
                CreateOrUpdateClip($"{CombatHitFolder}/{job.name}.wav", samples);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ProceduralAudioClipBuilder] Generated {count} combat-hit one-shot(s) -> {CombatHitFolder}/.");
        }

        [MenuItem("Tools/Space Samurai/Art/Assign Generated Clips (Open Scene)")]
        public static void AssignGeneratedClips()
        {
            var ambienceClips = new Dictionary<AmbienceTheme, AudioClip>();
            foreach (var job in AmbienceJobs)
                ambienceClips[job.theme] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AmbienceFolder}/{job.name}.wav");

            var footstepClips = new List<AudioClip>();
            foreach (var job in FootstepHardJobs)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{FootstepFolder}/{job.name}.wav");
                if (clip != null) footstepClips.Add(clip);
            }

            // Optional: only present once "Generate Combat Hit Clips" has been run. Missing entries
            // just mean AudioDirector's attackWindup/enemyDefeated stay unassigned (silent), same as
            // any other not-yet-authored clip field.
            var hitClips = new Dictionary<string, AudioClip>();
            foreach (var job in CombatHitJobs)
                hitClips[job.name] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CombatHitFolder}/{job.name}.wav");

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
            int ambienceAssigned = 0, footstepAssigned = 0, directorsAssigned = 0;

            foreach (GameObject root in roots)
            {
                // AudioDirector is a DontDestroyOnLoad singleton (lives in Phase6_Boot) rather than a
                // per-zone component like the two below, but it's still just a component in "the open
                // scene" so it fits the same walk.
                foreach (var director in root.GetComponentsInChildren<AudioDirector>(true))
                    if (AssignAudioDirectorClips(director, ambienceClips, hitClips)) directorsAssigned++;

                foreach (var layer in root.GetComponentsInChildren<ProximityAmbienceLayer>(true))
                {
                    var source = new SerializedObject(layer).FindProperty("source").objectReferenceValue as AudioSource;
                    if (source == null) continue;

                    AmbienceTheme theme = InferTheme(layer.gameObject.name);
                    if (!ambienceClips.TryGetValue(theme, out var clip) || clip == null) continue;
                    if (source.clip == clip && source.loop) continue; // already assigned
                    if (source.clip != null) continue; // hand-authored clip present -- don't clobber it

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

                    bool hasRealClips = false;
                    for (int i = 0; i < clipsProp.arraySize; i++)
                    {
                        if (clipsProp.GetArrayElementAtIndex(i).objectReferenceValue != null) { hasRealClips = true; break; }
                    }
                    if (hasRealClips) continue; // hand-authored/pipeline clips present -- don't clobber them

                    Undo.RecordObject(cadence, "Assign Generated Clips");
                    clipsProp.arraySize = footstepClips.Count;
                    for (int i = 0; i < footstepClips.Count; i++)
                        clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = footstepClips[i];
                    so.ApplyModifiedProperties();
                    footstepAssigned++;
                }
            }

            if (ambienceAssigned + footstepAssigned + directorsAssigned > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[ProceduralAudioClipBuilder] '{scene.name}': assigned ambience clip to {ambienceAssigned}, " +
                      $"footstep clips to {footstepAssigned}, AudioDirector roguelike clips on {directorsAssigned} " +
                      "component(s). Scene marked dirty -- save manually.");
        }

        /// <summary>
        /// Wires an <see cref="AudioDirector"/>'s roguelike-run clip fields (sector ambience +
        /// attackWindup/enemyDefeated one-shots) to the matching generated clips, skipping any field
        /// that already has one assigned (hand-authored or a previous run) — mirrors the
        /// non-clobbering idiom the ambience/footstep loops above use. Fields are private, so this goes
        /// through <see cref="SerializedObject"/> by name rather than a public setter API that would
        /// otherwise only exist for editor tooling. Returns true iff anything changed.
        /// </summary>
        private static bool AssignAudioDirectorClips(AudioDirector director,
            Dictionary<AmbienceTheme, AudioClip> ambienceClips, Dictionary<string, AudioClip> hitClips)
        {
            var so = new SerializedObject(director);
            AudioClip rust = ambienceClips.TryGetValue(AmbienceTheme.HangarHum, out var r) ? r : null;
            AudioClip program = ambienceClips.TryGetValue(AmbienceTheme.DreadDrone, out var p) ? p : null;
            AudioClip garden = ambienceClips.TryGetValue(AmbienceTheme.GardenWind, out var g) ? g : null;
            AudioClip windup = hitClips.TryGetValue("Hit_AttackWindup", out var w) ? w : null;
            AudioClip defeated = hitClips.TryGetValue("Hit_EnemyDefeated", out var d) ? d : null;

            bool changed = false;
            changed |= AssignIfEmpty(so, "sectorAmbienceRust", rust);
            changed |= AssignIfEmpty(so, "sectorAmbienceProgram", program);
            changed |= AssignIfEmpty(so, "sectorAmbienceGarden", garden);
            changed |= AssignIfEmpty(so, "attackWindup", windup);
            changed |= AssignIfEmpty(so, "enemyDefeated", defeated);

            if (changed)
            {
                Undo.RecordObject(director, "Assign Generated Clips");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(director);
            }
            return changed;
        }

        /// <summary>Sets <paramref name="fieldName"/> to <paramref name="clip"/> iff the field exists,
        /// is currently unassigned, and <paramref name="clip"/> is non-null -- never clobbers a
        /// hand-authored clip. Returns true iff it assigned.</summary>
        private static bool AssignIfEmpty(SerializedObject so, string fieldName, AudioClip clip)
        {
            if (clip == null) return false;
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = clip;
            return true;
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

        /// <summary>Writes a 16-bit PCM .wav file to disk at <paramref name="path"/> and (re)imports it
        /// through Unity's AudioImporter -- AudioClip.Create + AssetDatabase.CreateAsset does NOT persist
        /// PCM data to disk, which previously produced empty (m_Length: 0) clip stubs. Overwriting the same
        /// path on repeat runs keeps the same asset GUID, so existing scene/prefab references survive.</summary>
        private static void CreateOrUpdateClip(string path, float[] samples)
        {
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                const int channels = 1, bitsPerSample = 16;
                int byteRate = SampleRate * channels * bitsPerSample / 8;
                short blockAlign = (short)(channels * bitsPerSample / 8);
                int dataSize = samples.Length * channels * bitsPerSample / 8;

                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // fmt chunk size
                writer.Write((short)1); // PCM
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write((short)bitsPerSample);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                foreach (float s in samples)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * short.MaxValue), short.MinValue, short.MaxValue));
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
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
