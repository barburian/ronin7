using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Pure distance-filter for weakpoint-sight's marker placement: which candidate positions fall
    /// within range of the player. Kept separate from <see cref="WeakpointSight"/> so the selection
    /// math (an easy off-by-one on squared vs. linear radius) is unit-testable without a scene. Uses
    /// only the <see cref="Vector3"/> value type — no MonoBehaviour/scene dependency — mirroring
    /// <c>BladeDamager.SmoothSpeed</c>'s use of Vector3 in an otherwise pure method.
    /// </summary>
    public static class WeakpointSightMarkers
    {
        /// <summary>
        /// Returns the indices into <paramref name="candidatePositions"/> that lie within
        /// <paramref name="radius"/> of <paramref name="origin"/>, in input order. A null list or a
        /// non-positive radius yields no matches.
        /// </summary>
        public static List<int> SelectInRange(Vector3 origin, IReadOnlyList<Vector3> candidatePositions, float radius)
        {
            var result = new List<int>();
            if (candidatePositions == null || radius <= 0f) return result;

            float radiusSq = radius * radius;
            for (int i = 0; i < candidatePositions.Count; i++)
            {
                if ((candidatePositions[i] - origin).sqrMagnitude <= radiusSq)
                    result.Add(i);
            }
            return result;
        }
    }
}
