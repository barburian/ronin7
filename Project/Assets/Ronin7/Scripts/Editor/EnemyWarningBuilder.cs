using Ronin7.Ship;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Builder for Kessler's enemy-proximity warning system. Creates a GameObject with audio clips
    /// and wires the EnemyProximityWarning component. Used across all space-scene builders.
    /// </summary>
    internal static class EnemyWarningBuilder
    {
        /// <summary>Adds Kessler's enemy-proximity warning (audio-only) under the given cockpit transform.</summary>
        public static void AddTo(Transform cockpit, Vector3 localPosition)
        {
            var go = new GameObject("EnemyProximityWarning");
            go.transform.SetParent(cockpit, false);
            go.transform.localPosition = localPosition;

            // AudioSource: plays at half spatial blend (cockpit audio).
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;

            // EnemyProximityWarning component.
            var warning = go.AddComponent<EnemyProximityWarning>();
            var warningSo = new SerializedObject(warning);

            // Load the 3 voice clips and populate the clips array.
            var clipsProp = warningSo.FindProperty("clips");
            clipsProp.arraySize = 3;

            for (int i = 0; i < 3; i++)
            {
                string clipName = Ep01Lines.ClipName("space_enemy_warning", i, "Kessler");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Ronin7/Audio/Voice/{clipName}.wav");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Ronin7/Audio/Voice/{clipName}.mp3");
                if (clip == null)
                    Debug.LogWarning($"[EnemyProximityWarning] Missing voice clip: {clipName}");
                else
                    clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }

            // Wire the AudioSource reference.
            warningSo.FindProperty("audioSource").objectReferenceValue = audioSource;

            warningSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Rebuilds every space-combat scene that carries the warning (Galaxy1 + EP06/07/08 space
        /// scenes), then re-runs the input rewire pass. Also a batchmode entry point (-executeMethod).
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build All Space Combat Scenes", priority = 106)]
        public static void BuildAllSpaceCombatScenes()
        {
            XRRigBuilder.BuildGalaxy1Scene();
            XRRigBuilder.BuildEp06Approach();
            XRRigBuilder.BuildEp06Escape();
            XRRigBuilder.BuildEp07Approach();
            XRRigBuilder.BuildEp07Escape();
            XRRigBuilder.BuildEp08OrbitBreak();
            XRRigBuilder.BuildEp08NebulaEdge();
            XRRigBuilder.RewireAllScenes();
            Debug.Log("[Space Samurai] All space-combat scenes rebuilt + inputs rewired.");
        }
    }
}
