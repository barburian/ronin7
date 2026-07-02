using System.Collections.Generic;

namespace Ronin7.World
{
    /// <summary>
    /// Chapter 4's ambient manhunt-pressure logic ("heat"): rises with detection exposure (scan-drones,
    /// Coil patrols), decays over time, and reports each time normalized <see cref="Value"/> (0..1)
    /// crosses one of <c>thresholds</c> going upward. Pure C# (no UnityEngine dependency) so it is
    /// deterministic and unit-testable, mirroring how <see cref="Story.EchoCalloutSelector"/> is kept
    /// separate from the MonoBehaviour that drives it.
    ///
    /// A threshold re-arms once heat decays back below it, so it can fire again on a later rise (the
    /// city's pressure is meant to spike repeatedly, not just once per playthrough).
    /// </summary>
    public class HeatLogic
    {
        private readonly float gainPerDetection;
        private readonly float decayPerSecond;
        private readonly float[] thresholds;
        private readonly bool[] armed;
        private readonly Queue<int> pendingCrossings = new Queue<int>();

        public float Value { get; private set; }

        public HeatLogic(float gainPerDetection, float decayPerSecond, float[] thresholds)
        {
            this.gainPerDetection = gainPerDetection;
            this.decayPerSecond = decayPerSecond;
            this.thresholds = thresholds ?? new float[0];
            armed = new bool[this.thresholds.Length];
            for (int i = 0; i < armed.Length; i++) armed[i] = true;
        }

        /// <summary>Adds detection exposure (e.g. dt-scaled scan-drone contact). Ignores non-positive amounts.</summary>
        public void AddDetection(float amount)
        {
            if (amount <= 0f) return;
            SetValue(Value + amount * gainPerDetection);
        }

        /// <summary>Decays heat over elapsed time. Ignores non-positive dt.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            SetValue(Value - decayPerSecond * dt);
        }

        private void SetValue(float v)
        {
            Value = v < 0f ? 0f : (v > 1f ? 1f : v);
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (armed[i] && Value >= thresholds[i])
                {
                    armed[i] = false;
                    pendingCrossings.Enqueue(i);
                }
                else if (!armed[i] && Value < thresholds[i])
                {
                    armed[i] = true; // dropped back below: allow this threshold to fire again later
                }
            }
        }

        /// <summary>
        /// Pops the next unreported upward threshold crossing, in the order crossed (ascending index
        /// first when several are crossed by the same jump). Returns false when nothing is pending.
        /// </summary>
        public bool ConsumeThresholdCrossing(out int index)
        {
            if (pendingCrossings.Count == 0) { index = -1; return false; }
            index = pendingCrossings.Dequeue();
            return true;
        }
    }
}
