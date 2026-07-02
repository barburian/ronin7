using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Exports EP24 dialogue manifest to JSON for voice-over generation.
    /// Iterates all dialogue sets from Ep24Lines and writes speaker/text/clip info
    /// that can be consumed by the voice-over generation pipeline (generate_voice.py).
    /// </summary>
    public static class Ep24VoiceManifest
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

        private const string VoiceManifestPath = "Assets/Ronin7/Audio/Voice/ep24_voice_manifest.json";

        [MenuItem("Tools/Space Samurai/Galaxy 3/Export EP24 Voice Manifest")]
        public static void ExportEp24VoiceManifest()
        {
            var lines = new List<VoiceLine>();

            // Iterate all dialogue sets
            foreach (var setId in Ep24Lines.SetIds)
            {
                var dialogueLines = Ep24Lines.Get(setId);
                for (int i = 0; i < dialogueLines.Length; i++)
                {
                    var line = dialogueLines[i];
                    var clipName = Ep24Lines.ClipName(setId, i, line.speaker);
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

            Debug.Log($"[Ep24VoiceManifest] Exported {lines.Count} lines to {VoiceManifestPath}");
        }
    }
}
