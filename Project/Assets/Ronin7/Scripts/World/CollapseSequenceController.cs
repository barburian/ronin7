using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World
{
    /// <summary>
    /// Corridor collapse mechanic: debris segments drop progressively as the player moves
    /// along a corridor, but only after the player has safely passed each segment's trigger
    /// threshold. Segments are never dropped at or ahead of the player position.
    /// </summary>
    public class CollapseSequenceController : MonoBehaviour
    {
        [System.Serializable]
        public struct Segment
        {
            [Tooltip("Player progress (0..1 along corridor) at which this segment becomes droppable.")]
            public float triggerProgress;

            [Tooltip("Debris transforms to drop for this segment (animated downward).")]
            public Transform[] debris;
        }

        [Header("Corridor Setup")]
        [SerializeField] private Transform corridorStart;
        [SerializeField] private Transform corridorEnd;
        [SerializeField] private Transform playerTransform;

        [Header("Segments")]
        [SerializeField] private Segment[] segments = new Segment[0];

        [Header("Drop Animation")]
        [Tooltip("Time in seconds for each debris to fall from ceiling to floor.")]
        [SerializeField] private float dropDuration = 0.6f;

        [Tooltip("Distance in world units that debris falls.")]
        [SerializeField] private float dropHeight = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip rumbleClip;

        [Header("Events")]
        [SerializeField] private UnityEvent onAllDropped = new UnityEvent();

        private Vector3[] corridorAxis;
        private float corridorLength;
        private bool[] segmentDropped;
        private int segmentsCompleted = 0;

        private void Awake()
        {
            if (corridorStart == null || corridorEnd == null)
            {
                Debug.LogError("[CollapseSequenceController] Corridor start/end not assigned.", this);
                enabled = false;
                return;
            }

            corridorLength = Vector3.Distance(corridorStart.position, corridorEnd.position);
            if (corridorLength < 0.01f)
            {
                Debug.LogError("[CollapseSequenceController] Corridor start and end are too close.", this);
                enabled = false;
                return;
            }

            segmentDropped = new bool[segments.Length];
        }

        private void Update()
        {
            if (playerTransform == null || corridorStart == null || corridorEnd == null) return;

            // Calculate player progress along the corridor (0..1).
            Vector3 corridorVec = corridorEnd.position - corridorStart.position;
            Vector3 playerRelative = playerTransform.position - corridorStart.position;
            float progress = Mathf.Clamp01(Vector3.Dot(playerRelative, corridorVec.normalized) / corridorLength);

            // Build threshold array from segments.
            float[] thresholds = new float[segments.Length];
            for (int i = 0; i < segments.Length; i++)
                thresholds[i] = segments[i].triggerProgress;

            // Find highest droppable segment.
            int highest = HighestDroppableSegment(progress, thresholds);

            // Drop any segments up to and including the highest droppable.
            for (int i = 0; i <= highest; i++)
            {
                if (!segmentDropped[i])
                {
                    segmentDropped[i] = true;
                    StartCoroutine(DropSegment(i));
                }
            }
        }

        /// <summary>
        /// Pure logic: returns the highest segment index that should be dropped at the given
        /// player progress. A segment is droppable only after the player has passed its threshold.
        /// Segments are never dropped at or ahead of the player.
        ///
        /// Rules:
        /// - Segment i (where i+1 < length) is droppable when playerProgress > thresholds[i+1]
        /// - Segment length-1 is droppable when playerProgress >= 0.98f
        /// - Returns -1 if no segments are droppable.
        /// </summary>
        public static int HighestDroppableSegment(float playerProgress, float[] thresholds)
        {
            if (thresholds.Length == 0) return -1;

            int highest = -1;

            for (int i = 0; i < thresholds.Length; i++)
            {
                bool droppable = false;

                if (i + 1 < thresholds.Length)
                {
                    // Drop when player is past the next segment's threshold.
                    droppable = playerProgress > thresholds[i + 1];
                }
                else
                {
                    // Last segment: drop when player is near the end (0.98f).
                    droppable = playerProgress >= 0.98f;
                }

                if (droppable)
                    highest = i;
            }

            return highest;
        }

        private IEnumerator DropSegment(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= segments.Length) yield break;

            var segment = segments[segmentIndex];

            // Play rumble audio if available.
            if (rumbleClip != null && audioSource != null)
                audioSource.PlayOneShot(rumbleClip);

            // Drop all debris in this segment.
            var dropTasks = new List<Coroutine>();
            foreach (var debris in segment.debris)
            {
                if (debris != null)
                    dropTasks.Add(StartCoroutine(DropDebris(debris)));
            }

            // Wait for all debris to finish dropping.
            foreach (var task in dropTasks)
                yield return task;

            segmentsCompleted++;
            if (segmentsCompleted >= segments.Length)
                onAllDropped?.Invoke();
        }

        private IEnumerator DropDebris(Transform debris)
        {
            Vector3 startPos = debris.position;
            Vector3 endPos = startPos + Vector3.down * dropHeight;
            float elapsed = 0f;

            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                debris.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            debris.position = endPos;
        }
    }
}
