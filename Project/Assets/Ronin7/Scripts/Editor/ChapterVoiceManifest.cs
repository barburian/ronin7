using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Ronin7.World.Story;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Exports the per-chapter dialogue manifests consumed by the voice-over generation pipeline
    /// (Art/Generated/Audio/Tools/generate_voice.py). Generalizes Chapter1VoiceManifest across every
    /// built Galaxy 1 chapter: 2-13 and 16 (14/15 have no ChapterNLines in this galaxy; Ch1 keeps its
    /// own dedicated exporter). Each chapter's own ClipName() is used verbatim so the manifest's "file"
    /// field matches exactly what the chapter builder later loads.
    /// </summary>
    public static class ChapterVoiceManifest
    {
        [Serializable]
        private struct VoiceLine
        {
            public string file;
            public string speaker;
            public string text;
        }

        [Serializable]
        private class VoiceManifestData
        {
            public VoiceLine[] lines;
        }

        private const string VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        [MenuItem("Tools/Space Samurai/Audio/Export All Chapter Voice Manifests")]
        public static void ExportAllChapterVoiceManifests()
        {
            int total = 0;
            total += Export("ch2", Chapter2Lines.SetIds, Chapter2Lines.Get, Chapter2Lines.ClipName);
            total += Export("ch3", Chapter3Lines.SetIds, Chapter3Lines.Get, Chapter3Lines.ClipName);
            total += Export("ch4", Chapter4Lines.SetIds, Chapter4Lines.Get, Chapter4Lines.ClipName);
            total += Export("ch5", Chapter5Lines.SetIds, Chapter5Lines.Get, Chapter5Lines.ClipName);
            total += Export("ch6", Chapter6Lines.SetIds, Chapter6Lines.Get, Chapter6Lines.ClipName);
            total += Export("ch7", Chapter7Lines.SetIds, Chapter7Lines.Get, Chapter7Lines.ClipName);
            total += Export("ch8", Chapter8Lines.SetIds, Chapter8Lines.Get, Chapter8Lines.ClipName);
            total += Export("ch9", Chapter9Lines.SetIds, Chapter9Lines.Get, Chapter9Lines.ClipName);
            total += Export("ch10", Chapter10Lines.SetIds, Chapter10Lines.Get, Chapter10Lines.ClipName);
            total += Export("ch11", Chapter11Lines.SetIds, Chapter11Lines.Get, Chapter11Lines.ClipName);
            total += Export("ch12", Chapter12Lines.SetIds, Chapter12Lines.Get, Chapter12Lines.ClipName);
            total += Export("ch13", Chapter13Lines.SetIds, Chapter13Lines.Get, Chapter13Lines.ClipName);
            total += Export("ch16", Chapter16Lines.SetIds, Chapter16Lines.Get, Chapter16Lines.ClipName);

            AssetDatabase.Refresh();
            Debug.Log($"[ChapterVoiceManifest] Exported {total} lines across 13 chapter manifests to {VoiceFolder}");
        }

        /// <summary>Write one chapter's manifest. Returns the number of lines written.</summary>
        private static int Export(string prefix, string[] setIds,
            Func<string, DialogueLine[]> get, Func<string, int, string, string> clipName)
        {
            var lines = new List<VoiceLine>();
            foreach (var setId in setIds)
            {
                var dialogueLines = get(setId);
                for (int i = 0; i < dialogueLines.Length; i++)
                {
                    lines.Add(new VoiceLine
                    {
                        file = clipName(setId, i, dialogueLines[i].speaker),
                        speaker = dialogueLines[i].speaker,
                        text = dialogueLines[i].text
                    });
                }
            }

            var path = $"{VoiceFolder}/{prefix}_voice_manifest.json";
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var manifest = new VoiceManifestData { lines = lines.ToArray() };
            File.WriteAllText(path, JsonUtility.ToJson(manifest, prettyPrint: true));
            Debug.Log($"[ChapterVoiceManifest] {prefix}: {lines.Count} lines -> {path}");
            return lines.Count;
        }
    }
}
