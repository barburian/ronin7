using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// "GravityRigController" mechanic: a gladiator-pit arena whose gravity cycles
    /// between zero-g and heavy gravity between rounds. Applies deterministic forces to
    /// registered Rigidbody objects via Tick().
    ///
    /// CRITICAL: This component never moves, rotates, or repositions the player's XROrigin/Camera.
    /// It only applies forces to assigned arena props and fighters.
    ///
    /// For tests: call Tick(deltaTime) to drive state transitions and gravity application.
    /// </summary>
    public class GravityRigController : MonoBehaviour
    {
        public enum GravityState { ZeroG, Heavy, Normal }

        [Header("Gravity")]
        [SerializeField] private float roundDuration = 6f;
        [SerializeField] private float heavyGravity = -29.4f;
        [SerializeField] private float normalGravity = -9.81f;
        [SerializeField] private List<Rigidbody> affectedBodies = new();

        [Header("Cycle")]
        [SerializeField] private GravityState[] cycle = new[] { GravityState.ZeroG, GravityState.Heavy, GravityState.ZeroG };
        [SerializeField] private bool autoAdvance = true;

        private int cycleIndex = 0;
        private float stateTimer = 0f;

        /// <summary>The current gravity state.</summary>
        public GravityState CurrentState { get; private set; }

        /// <summary>Fired whenever SetState is called.</summary>
        public event System.Action<GravityState> StateChanged;

        /// <summary>Set the current state and invoke StateChanged.</summary>
        public void SetState(GravityState state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }

        /// <summary>Advance to the next state in the cycle (wrapping). No-op if the cycle is empty
        /// (e.g. a designer cleared it), which would otherwise divide by zero.</summary>
        public void Advance()
        {
            if (cycle == null || cycle.Length == 0) return;
            cycleIndex = (cycleIndex + 1) % cycle.Length;
            SetState(cycle[cycleIndex]);
        }

        /// <summary>Return the gravity value for a given state.</summary>
        public float GravityForState(GravityState state)
        {
            return state switch
            {
                GravityState.ZeroG => 0f,
                GravityState.Heavy => heavyGravity,
                GravityState.Normal => normalGravity,
                _ => 0f
            };
        }

        /// <summary>Accumulate time, auto-advance if triggered, then apply gravity forces to all bodies.</summary>
        public void Tick(float deltaTime)
        {
            if (autoAdvance)
            {
                stateTimer += deltaTime;
                if (stateTimer >= roundDuration)
                {
                    stateTimer = 0f;
                    Advance();
                }
            }

            float gravityAccel = GravityForState(CurrentState);
            foreach (var body in affectedBodies)
            {
                if (body != null)
                {
                    body.linearVelocity += Vector3.up * gravityAccel * deltaTime;
                }
            }
        }

        /// <summary>Register a Rigidbody to receive gravity forces (guards against null and duplicates).</summary>
        public void RegisterBody(Rigidbody rb)
        {
            if (rb == null || affectedBodies.Contains(rb)) return;
            affectedBodies.Add(rb);
            rb.useGravity = false;
        }

        private void Awake()
        {
            cycleIndex = 0;
            // Fall back to Normal when the cycle is empty (e.g. a designer cleared it) instead of
            // crashing on cycle[0].
            CurrentState = (cycle != null && cycle.Length > 0) ? cycle[0] : GravityState.Normal;
            stateTimer = 0f;

            foreach (var body in affectedBodies)
            {
                if (body != null)
                    body.useGravity = false;
            }
        }

        private void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }
    }
}
