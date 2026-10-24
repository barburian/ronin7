using System.IO;
using Ronin7.Audio;
using Ronin7.Editor.Art;
using UnityEditor;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Ronin7.Editor
{
    /// <summary>
    /// P6-01 editor tooling, all idempotent and re-runnable: bake the synthesized run music and cue clips
    /// (<see cref="ProceduralMusicSynth"/>), create <c>Audio/Ronin7.mixer</c>, wire both onto the AudioDirector in
    /// Phase6_Boot additively (never a scene rebuild), and apply the Quest import settings.
    /// </summary>
    public static class RunAudioBuilder
    {
        public const string MixerPath = "Assets/Ronin7/Audio/Ronin7.mixer";
        private const string MusicFolder = "Assets/Ronin7/Audio/Generated/Music";
        private const string CueFolder = "Assets/Ronin7/Audio/Generated/Cues";
        private const string HitFolder = "Assets/Ronin7/Audio/Generated/CombatHits";
        private const string GeneratedRoot = "Assets/Ronin7/Audio/Generated";
        private const string RunVoiceFolder = "Assets/Ronin7/Resources/RunVoice";
        private const string BootScenePath = "Assets/Ronin7/Scenes/Phase6_Boot.unity";

        private static readonly string[] SectorNames = { "Rust", "Program", "Garden" };
        private static readonly string[] IntensityNames = { "Explore", "Combat", "Boss" };

        /// <summary>Cue -> (asset path, AudioDirector field). The first four overwrite the old noise placeholders in
        /// place (same path, so the GUIDs and any scene reference survive).</summary>
        private static readonly (ProceduralMusicSynth.Cue cue, string path, string field)[] Cues =
        {
            (ProceduralMusicSynth.Cue.BoonChosen, HitFolder + "/Hit_BoonChosen.wav", "boonChosen"),
            (ProceduralMusicSynth.Cue.RunLost, HitFolder + "/Hit_RunLost.wav", "runLost"),
            (ProceduralMusicSynth.Cue.RunWon, HitFolder + "/Hit_RunWon.wav", "runWon"),
            (ProceduralMusicSynth.Cue.WindupMelee, HitFolder + "/Hit_AttackWindup.wav", "attackWindup"),
            (ProceduralMusicSynth.Cue.EnemySpawning, CueFolder + "/Cue_EnemySpawning.wav", "enemySpawning"),
            (ProceduralMusicSynth.Cue.RunStarted, CueFolder + "/Cue_RunStarted.wav", "runStarted"),
            (ProceduralMusicSynth.Cue.RoomCleared, CueFolder + "/Cue_RoomCleared.wav", "roomCleared"),
            (ProceduralMusicSynth.Cue.BoonOffered, CueFolder + "/Cue_BoonOffered.wav", "boonOffered"),
            (ProceduralMusicSynth.Cue.BossPhase, CueFolder + "/Cue_BossPhase.wav", "bossPhase"),
            (ProceduralMusicSynth.Cue.WindupCharger, CueFolder + "/Windup_Charger.wav", "windupCharger"),
            (ProceduralMusicSynth.Cue.WindupBlink, CueFolder + "/Windup_Blink.wav", "windupBlink"),
            (ProceduralMusicSynth.Cue.WindupDrone, CueFolder + "/Windup_Drone.wav", "windupDrone"),
            (ProceduralMusicSynth.Cue.WindupRanged, CueFolder + "/Windup_Ranged.wav", "windupRanged"),
            (ProceduralMusicSynth.Cue.LowHealthLoop, CueFolder + "/Loop_LowHealth.wav", "lowHealthLoop"),
            (ProceduralMusicSynth.Cue.MenuAmbience, CueFolder + "/Loop_MenuAmbience.wav", "menuAmbience"),
        };

        private static string MusicPath(int sector, int intensity) =>
            $"{MusicFolder}/Music_{SectorNames[sector]}_{IntensityNames[intensity]}.wav";

        [MenuItem("Tools/Space Samurai/Audio/P6-01 Generate Run Music And Cues")]
        public static void GenerateClips()
        {
            ProceduralAudioClipBuilder.EnsureFolder(MusicFolder);
            ProceduralAudioClipBuilder.EnsureFolder(CueFolder);
            for (int s = 0; s < 3; s++)
                for (int i = 0; i < 3; i++)
                    ProceduralAudioClipBuilder.CreateOrUpdateClip(MusicPath(s, i),
                        ProceduralMusicSynth.GenerateTrack(s, (MusicIntensity)i), ProceduralMusicSynth.MusicRate);
            foreach (var c in Cues)
                ProceduralAudioClipBuilder.CreateOrUpdateClip(c.path, ProceduralMusicSynth.GenerateCue(c.cue), ProceduralMusicSynth.CueRate);
            ApplyImportSettings();
            Debug.Log("[RunAudioBuilder] Generated 9 music loops and " + Cues.Length + " cues, import settings applied.");
        }

        // ------------------------------------------------------------------ mixer
        [MenuItem("Tools/Space Samurai/Audio/P6-01 Create Mixer")]
        public static void CreateMixer()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
            {
                Debug.Log("[RunAudioBuilder] " + MixerPath + " already exists; left as is.");
                return;
            }
            ProceduralAudioClipBuilder.EnsureFolder("Assets/Ronin7/Audio");
            // The mixer controller types are internal to UnityEditor, so this goes through reflection.
            var ctrl = Call(TCtrl, null, "CreateMixerControllerAtPath", MixerPath);
            var master = TCtrl.GetProperty("masterGroup", All).GetValue(ctrl);
            Expose(ctrl, master, "MasterVol");
            foreach (var (name, param) in new[] { ("Music", "MusicVol"), ("SFX", "SfxVol"), ("Ambience", "AmbienceVol"), ("UI", "UiVol") })
            {
                var g = Call(TCtrl, ctrl, "CreateNewGroup", name, true);
                Call(TCtrl, ctrl, "AddChildToParent", g, master);
                Expose(ctrl, g, param);
            }
            EditorUtility.SetDirty((Object)ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[RunAudioBuilder] Created " + MixerPath + " (Master/Music/SFX/Ambience/UI, dB params exposed).");
        }

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static readonly Assembly EditorAsm = typeof(UnityEditor.Editor).Assembly;
        private static readonly System.Type TCtrl = EditorAsm.GetType("UnityEditor.Audio.AudioMixerController");
        private static readonly System.Type TGroup = EditorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");

        private static object Call(System.Type t, object target, string method, params object[] args) =>
            t.GetMethod(method, All).Invoke(target, args);

        /// <summary>Exposes a group's volume as a named parameter (the editor UI's "Expose to script").</summary>
        private static void Expose(object ctrl, object group, string name)
        {
            object guid = Call(TGroup, group, "GetGUIDForVolume");
            object path = System.Activator.CreateInstance(EditorAsm.GetType("UnityEditor.Audio.AudioGroupParameterPath"), group, guid);
            Call(TCtrl, ctrl, "AddExposedParameter", path);
            var prop = TCtrl.GetProperty("exposedParameters", All);
            var arr = (System.Array)prop.GetValue(ctrl);
            for (int i = 0; i < arr.Length; i++)
            {
                object el = arr.GetValue(i);
                var t = el.GetType();
                var gf = t.GetField("guid", All);
                if (gf != null && gf.GetValue(el).Equals(guid)) { t.GetField("name", All).SetValue(el, name); arr.SetValue(el, i); }
            }
            prop.SetValue(ctrl, arr);
            Call(TCtrl, ctrl, "OnChangedExposedParameter");
        }

        // ------------------------------------------------------------------ wiring
        /// <summary>Assigns the generated clips, mixer and groups onto <paramref name="director"/>. Overwrites the four old
        /// noise placeholders; every other field is only set when found. Used by the menu and the Boot builder.</summary>
        public static int Wire(AudioDirector director)
        {
            var so = new SerializedObject(director);
            int n = 0;
            foreach (var c in Cues) n += Set(so, c.field, AssetDatabase.LoadAssetAtPath<AudioClip>(c.path));
            var music = so.FindProperty("runMusic");
            if (music != null)
            {
                music.arraySize = 9;
                for (int s = 0; s < 3; s++)
                    for (int i = 0; i < 3; i++)
                    {
                        music.GetArrayElementAtIndex(s * 3 + i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath(s, i));
                        n++;
                    }
            }
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            n += Set(so, "mixer", mixer);
            if (mixer != null)
            {
                n += Set(so, "musicGroup", mixer.FindMatchingGroups("Master/Music")[0]);
                n += Set(so, "sfxGroup", mixer.FindMatchingGroups("Master/SFX")[0]);
                n += Set(so, "ambienceGroup", mixer.FindMatchingGroups("Master/Ambience")[0]);
                n += Set(so, "uiGroup", mixer.FindMatchingGroups("Master/UI")[0]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
            return n;
        }

        private static int Set(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p == null || value == null) return 0;
            p.objectReferenceValue = value;
            return 1;
        }

        [MenuItem("Tools/Space Samurai/Audio/P6-01 Wire Run Audio (Phase6_Boot)")]
        public static void WireBootScene()
        {
            var open = SceneManager.GetSceneByPath(BootScenePath);
            bool opened = !open.isLoaded;
            var scene = opened ? EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive) : open;
            AudioDirector director = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                director = root.GetComponentInChildren<AudioDirector>(true);
                if (director != null) break;
            }
            if (director == null) { Debug.LogError("[RunAudioBuilder] No AudioDirector in Phase6_Boot."); }
            else
            {
                int n = Wire(director);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[RunAudioBuilder] Wired " + n + " references onto the Phase6_Boot AudioDirector and saved the scene.");
            }
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        // ------------------------------------------------------------------ Quest import settings
        /// <summary>Pure policy. Loops (music / ambience / heartbeat) never preload; music and ambience stay compressed in
        /// memory; everything else under 200 KB decompresses on load, larger stays compressed.</summary>
        public static (AudioClipLoadType load, bool preload) ImportPolicy(long sourceBytes, bool musicOrAmbience, bool loop)
        {
            var load = musicOrAmbience || sourceBytes >= 200 * 1024 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            return (load, !loop);
        }

        [MenuItem("Tools/Space Samurai/Audio/P6-01 Apply Quest Import Settings")]
        public static void ApplyImportSettings()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { GeneratedRoot, RunVoiceFolder }))
                if (ApplyTo(AssetDatabase.GUIDToAssetPath(guid))) n++;
            AssetDatabase.SaveAssets();
            Debug.Log("[RunAudioBuilder] Quest import settings applied to " + n + " clip(s) (Vorbis q0.5, mono).");
        }

        private static bool ApplyTo(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) return false;
            string lower = path.ToLowerInvariant();
            bool musicOrAmb = lower.Contains("/music/") || lower.Contains("/ambience/") || lower.Contains("menuambience");
            bool loop = musicOrAmb || lower.Contains("loop_");
            long bytes = new FileInfo(Path.Combine(Path.GetDirectoryName(Application.dataPath), path)).Length;
            var (load, preload) = ImportPolicy(bytes, musicOrAmb, loop);
            var s = new AudioImporterSampleSettings
            {
                loadType = load,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.5f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                preloadAudioData = preload,
            };
            imp.forceToMono = true;
            imp.defaultSampleSettings = s;
            imp.SetOverrideSampleSettings("Android", s);
            imp.SaveAndReimport();
            return true;
        }
    }
}
