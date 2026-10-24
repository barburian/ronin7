using System.IO;
using System.Linq;
using Ronin7.Flow;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>P6-04: exports <see cref="KesslerRunLines"/> as a generate_voice.py manifest (same JSON shape as ChapterVoiceManifest).
    /// Generate with: python generate_voice.py --manifest Voice/runvoice_voice_manifest.json --out ../../../Resources/RunVoice</summary>
    public static class RunVoiceManifest
    {
        public const string ManifestPath = "Assets/Ronin7/Art/Generated/Audio/Voice/runvoice_voice_manifest.json";

        [System.Serializable] private struct Entry { public string file, speaker, text; }
        [System.Serializable] private class Data { public Entry[] lines; }

        [MenuItem("Tools/Space Samurai/Audio/Export Run Voice Manifest")]
        public static void Export()
        {
            var data = new Data
            {
                lines = KesslerRunLines.AllLines().Select(l => new Entry { file = l.Clip, speaker = l.Speaker, text = l.Text }).ToArray()
            };
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(data, true));
            AssetDatabase.Refresh();
            Debug.Log($"[RunVoiceManifest] Exported {data.lines.Length} lines to {ManifestPath}");
        }
    }
}
