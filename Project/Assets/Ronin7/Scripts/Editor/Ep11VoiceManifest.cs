using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Exports EP11 dialogue manifest to JSON for voice-over generation.
    /// Iterates all dialogue sets from Ep11Lines and writes speaker/text/clip info
    /// that can be consumed by the voice-over generation pipeline.
    /// </summary>
    public static class Ep11VoiceManifest
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

        private const string VoiceManifestPath = "Assets/Ronin7/Audio/Voice/ep11_voice_manifest.json";

        [MenuItem("Tools/Space Samurai/Galaxy 2/Export EP11 Voice Manifest")]
        public static void ExportEp11VoiceManifest()
        {
            var lines = new List<VoiceLine>();

            // Iterate all dialogue sets
            foreach (var setId in Ep11Lines.SetIds)
            {
                var dialogueLines = Ep11Lines.Get(setId);
                for (int i = 0; i < dialogueLines.Length; i++)
                {
                    var line = dialogueLines[i];
                    var clipName = Ep11Lines.ClipName(setId, i, line.speaker);
                    lines.Add(new VoiceLine
                    {
                        file = clipName,
                        speaker = line.speaker,
                        text = line.text
                    });
                }
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(VoiceManifestPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Write JSON using JsonUtility
            var manifest = new VoiceManifestData { lines = lines.ToArray() };
            var json = JsonUtility.ToJson(manifest, prettyPrint: true);
            File.WriteAllText(VoiceManifestPath, json);

            // Refresh asset database
            AssetDatabase.Refresh();

            Debug.Log($"[Ep11VoiceManifest] Exported {lines.Count} lines to {VoiceManifestPath}");
        }
    }
}
