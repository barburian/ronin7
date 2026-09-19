using System;
using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>Published on the EventBus when any entity takes damage.</summary>
    public readonly struct EntityDamaged
    {
        public readonly GameObject Entity;
        public readonly DamageInfo Info;
        public readonly float Current;
        public readonly float Max;
        public EntityDamaged(GameObject entity, in DamageInfo info, float current, float max)
        {
            Entity = entity; Info = info; Current = current; Max = max;
        }
    }

    /// <summary>Published on the EventBus when an entity's health reaches zero.</summary>
    public readonly struct EntityDied
    {
        public readonly GameObject Entity;
        /// <summary>Source of the lethal <see cref="DamageInfo"/> (the killer), or null when the
        /// death has no attribution (scripted deaths, legacy publishers). Lets campaign-stat
        /// tracking distinguish player feats from NPC-vs-NPC kills (gang wars, friendly fire).</summary>
        public readonly GameObject Killer;

        public EntityDied(GameObject entity) : this(entity, null) { }
        public EntityDied(GameObject entity, GameObject killer)
        {
            Entity = entity;
            Killer = killer;
        }
    }

    /// <summary>Standard health pool. Implements the Core damage contract so any system can hurt it.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        /// <summary>All enabled Health components, for cheap lookups (nearest-target retargeting,
        /// weakpoint-sight scans) without a scene-wide FindObjectsByType every frame/interval. Mirrors
        /// the Ronin7.Ship EnemyShip.Active/Asteroid.Active idiom.</summary>
        public static readonly List<Health> Active = new();

        [SerializeField] private float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;

        /// <summary>Local listeners (VFX, animation). Global listeners use the EventBus events.</summary>
        public event Action<DamageInfo> Damaged;
        public event Action Died;

        /// <summary>Optional death-interceptor hook (e.g. Ch11's Unbroken ward). When set and a lethal
        /// blow lands, this is invoked BEFORE the death fires; returning true survives the blow at
        /// <see cref="ReviveFraction"/> of max HP (floor 1) and Died/EntityDied are never raised for it.
        /// Null (the default) preserves current behavior exactly, so every scene/test that never sets
        /// this is unaffected.</summary>
        public Func<bool> DeathInterceptor { get; set; }

        /// <summary>Fraction of max health restored when <see cref="DeathInterceptor"/> survives a
        /// lethal blow. Default 0 preserves the existing survive-at-1-HP behaviour exactly (A6.2) — so
        /// every scene/test that never sets this is byte-identical to today.</summary>
        public float ReviveFraction { get; set; }

        private void Awake() => Current = maxHealth;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak stale entries from a previous run into the next (mirrors EventBus's
        // reset).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Active.Clear();

        /// <summary>Set the max pool and refill (used by data-driven enemies / respawns).</summary>
        public void Configure(float max)
        {
            maxHealth = max;
            Current = max;
        }

        public void ApplyDamage(in DamageInfo info)
        {
            if (!IsAlive) return;
            // Reject non-positive or NaN "damage": healing only happens via Heal(), and a bad amount
            // must never heal past max or emit spurious hit feedback. !(amount > 0) also rejects NaN.
            if (!(info.Amount > 0f)) return;

            Current = Mathf.Max(0f, Current - info.Amount);
            Damaged?.Invoke(info);
            EventBus.Publish(new EntityDamaged(gameObject, info, Current, maxHealth));

            if (Current <= 0f)
            {
                if (DeathInterceptor != null && DeathInterceptor.Invoke())
                {
                    Current = Mathf.Max(1f, maxHealth * ReviveFraction);
                    return;
                }

                Died?.Invoke();
                EventBus.Publish(new EntityDied(gameObject, info.Source));
            }
        }

        public void ResetHealth() => Current = maxHealth;

        /// <summary>Sets current health directly, clamped to [0, Max], without firing a damage/death
        /// event. Used to restore durable HP carried across a scene reload (e.g. the roguelike mode's
        /// node-to-node attrition, see RunState.PlayerHealth) — a plain damage/heal call would either
        /// fire spurious feedback or be rejected outright (Heal no-ops once dead; ApplyDamage requires
        /// a positive amount).</summary>
        public void SetCurrent(float value) => Current = Mathf.Clamp(value, 0f, maxHealth);

        /// <summary>Increase current health up to max (used by defensive mechanics). No-op once dead
        /// so a late refund/heal event can never resurrect an entity whose death already fired.</summary>
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            // Reject non-positive or NaN amounts: mirrors ApplyDamage's guard. !(amount > 0) also rejects NaN.
            if (!(amount > 0f)) return;
            Current = Mathf.Min(Current + amount, maxHealth);
        }
    }
}
