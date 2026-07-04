using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Wires already-generated voice-over clips into the Chapter 2-16 scenes. Those scenes were built
    /// before the audio existed, so every DialoguePlayer.lines[i].clip in them serialized as
    /// {fileID: 0} even though each ChapterNBuilder has correct wiring logic (ChNWireVoiceClips) that
    /// only runs at build time. Rebuilding the scenes would wipe post-build work, so this utility opens
    /// each scene directly and wires clips in place instead.
    ///
    /// A DialoguePlayer's setId isn't itself serialized on the component, so each dialogue is identified
    /// by matching its serialized lines[].speaker/text against every ChapterNLines set for that chapter
    /// (built from the same source, so the sequence matches exactly) rather than by parsing the
    /// GameObject name, which follows no single convention across chapters.
    ///
    /// Chapter 1 (Galaxy1_Ch1_Hub) already has its own wiring pass (108/111) and is intentionally
    /// excluded here.
    /// </summary>
    public static class ChapterVoiceWirer
    {
        private const string SceneFolder = "Assets/Ronin7/Scenes";
        private const string VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        private struct ChapterSpec
        {
            public string ScenePath;
            public string[] SetIds;
            public Func<string, DialogueLine[]> Get;
            public Func<string, int, string, string> ClipName;
        }

        [MenuItem("Tools/Space Samurai/Audio/Wire Voice Clips Into Chapter Scenes")]
        public static void WireVoiceClipsIntoChapterScenes()
        {
            var chapters = new[]
            {
                Spec("Ch02_Auction.unity", Chapter2Lines.SetIds, Chapter2Lines.Get, Chapter2Lines.ClipName),
                Spec("Ch03_SwordRemembers.unity", Chapter3Lines.SetIds, Chapter3Lines.Get, Chapter3Lines.ClipName),
                Spec("Ch04_OverseersHunt.unity", Chapter4Lines.SetIds, Chapter4Lines.Get, Chapter4Lines.ClipName),
                Spec("Ch05_DebtOfAshes.unity", Chapter5Lines.SetIds, Chapter5Lines.Get, Chapter5Lines.ClipName),
                Spec("Ch06_IronDojo.unity", Chapter6Lines.SetIds, Chapter6Lines.Get, Chapter6Lines.ClipName),
                Spec("Ch07_ForgottenNames.unity", Chapter7Lines.SetIds, Chapter7Lines.Get, Chapter7Lines.ClipName),
                Spec("Ch08_SilentGarden.unity", Chapter8Lines.SetIds, Chapter8Lines.Get, Chapter8Lines.ClipName),
                Spec("Ch09_PitAndTheDeep.unity", Chapter9Lines.SetIds, Chapter9Lines.Get, Chapter9Lines.ClipName),
                Spec("Ch10_LedgerOfRust.unity", Chapter10Lines.SetIds, Chapter10Lines.Get, Chapter10Lines.ClipName),
                Spec("Ch11_GhostsAndOrigins.unity", Chapter11Lines.SetIds, Chapter11Lines.Get, Chapter11Lines.ClipName),
                Spec("Ch12_TheFracture.unity", Chapter12Lines.SetIds, Chapter12Lines.Get, Chapter12Lines.ClipName),
                Spec("Ch13_SterileReckoning.unity", Chapter13Lines.SetIds, Chapter13Lines.Get, Chapter13Lines.ClipName),
                Spec("Ch16_ThroneOfAshes.unity", Chapter16Lines.SetIds, Chapter16Lines.Get, Chapter16Lines.ClipName),
            };

            int totalResolved = 0, totalLines = 0;
            foreach (var spec in chapters)
            {
                var (resolved, total) = WireScene(spec);
                totalResolved += resolved;
                totalLines += total;
            }

            Debug.Log($"[ChapterVoiceWirer] Done: {totalResolved}/{totalLines} lines wired across {chapters.Length} scenes.");
        }

        private static ChapterSpec Spec(string sceneFileName, string[] setIds,
            Func<string, DialogueLine[]> get, Func<string, int, string, string> clipName)
        {
            return new ChapterSpec
            {
                ScenePath = $"{SceneFolder}/{sceneFileName}",
                SetIds = setIds,
                Get = get,
                ClipName = clipName
            };
        }

        /// <summary>Opens one chapter scene, wires every DialoguePlayer's clips, saves it. Returns (resolved, total) line counts.</summary>
        private static (int resolved, int total) WireScene(ChapterSpec spec)
        {
            var scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);

            // Every dialogue set for this chapter, keyed by setId, built from the same source the scene
            // was originally authored from.
            var setLines = new Dictionary<string, DialogueLine[]>();
            foreach (var setId in spec.SetIds)
                setLines[setId] = spec.Get(setId);

            int resolved = 0, total = 0;
            foreach (var dp in UnityEngine.Object.FindObjectsByType<DialoguePlayer>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(dp);
                var linesProp = so.FindProperty("lines");
                int count = linesProp.arraySize;
                total += count;

                string setId = MatchSetId(linesProp, setLines);
                if (setId == null)
                {
                    Debug.LogWarning($"[ChapterVoiceWirer] {spec.ScenePath}: '{dp.gameObject.name}' ({count} lines) matched no known dialogue set — skipped.");
                    continue;
                }

                bool dirty = false;
                for (int i = 0; i < count; i++)
                {
                    var el = linesProp.GetArrayElementAtIndex(i);
                    var clipProp = el.FindPropertyRelative("clip");
                    if (clipProp.objectReferenceValue != null)
                    {
                        resolved++;
                        continue;
                    }

                    string speaker = el.FindPropertyRelative("speaker").stringValue;
                    string clipName = spec.ClipName(setId, i, speaker);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoiceFolder}/{clipName}.mp3")
                               ?? AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoiceFolder}/{clipName}.wav");
                    if (clip != null)
                    {
                        clipProp.objectReferenceValue = clip;
                        resolved++;
                        dirty = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[ChapterVoiceWirer] {spec.ScenePath}: '{dp.gameObject.name}' line {i} ('{speaker}', set '{setId}') missing clip '{clipName}'.");
                    }
                }

                if (dirty)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ChapterVoiceWirer] {spec.ScenePath}: {resolved}/{total} lines wired.");
            return (resolved, total);
        }

        /// <summary>Finds the setId whose line sequence (speaker+text, in order) matches the serialized
        /// lines exactly. Returns null on no match or an ambiguous (multiple) match.</summary>
        private static string MatchSetId(SerializedProperty linesProp, Dictionary<string, DialogueLine[]> setLines)
        {
            int count = linesProp.arraySize;
            string found = null;
            foreach (var kvp in setLines)
            {
                if (kvp.Value.Length != count) continue;

                bool match = true;
                for (int i = 0; i < count; i++)
                {
                    var el = linesProp.GetArrayElementAtIndex(i);
                    if (el.FindPropertyRelative("speaker").stringValue != kvp.Value[i].speaker ||
                        el.FindPropertyRelative("text").stringValue != kvp.Value[i].text)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    if (found != null) return null; // ambiguous — two sets with identical content
                    found = kvp.Key;
                }
            }

            return found;
        }
    }
}
