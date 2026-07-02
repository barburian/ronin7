using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Pillar 2 of the sun-navigation system (see <c>Docs/SunNavigation-Design.md</c>): the gravity-assist
    /// slingshot. Placed on the sun visual; defines three concentric radii in universe units —
    /// outer (assist begins) &gt; danger (heat begins) &gt; core (max heat / lethal). The boost/heat curves
    /// are pure statics (unit-tested); the component just exposes the computed values for a queried point.
    ///
    /// ADDITIVE &amp; OFF BY DEFAULT: this component applies NOTHING itself — no boost, no damage. It is a
    /// passive query source. With no consumer reading it, placing it on the sun changes no behaviour. The
    /// flight/health integration is a deliberate in-headset hand-off (see the hook notes below), kept out
    /// of the flight loop so it can be tuned live without destabilising it.
    ///
    /// FRAME OF REFERENCE: distances are universe-frame separations. The sun and any queried object both
    /// render at their universe-swept world positions, so a plain world-space distance between them IS
    /// their universe-frame separation (same trick <see cref="AsteroidHazard"/> relies on: the player hull
    /// sits at the world origin, so query with <c>Vector3.zero</c> for the player).
    /// </summary>
    [DisallowMultipleComponent]
    public class SunGravityWell : MonoBehaviour
    {
        [Header("Radii (universe units) — outer > danger > core")]
        [Tooltip("Slingshot assist begins here; boost ramps 0 → maxBoost from here inward to the danger radius.")]
        [SerializeField] private float outerRadius = 400f;
        [Tooltip("Heat begins here (boost is already at maxBoost); heat ramps 0 → maxHeat from here inward to the core.")]
        [SerializeField] private float dangerRadius = 150f;
        [Tooltip("Max-heat / lethal radius; heat holds at maxHeat at and inside this.")]
        [SerializeField] private float coreRadius = 60f;

        [Header("Magnitudes")]
        [Tooltip("Peak slingshot boost factor at/inside the danger radius (additive multiplier; tuned in-headset for comfort).")]
        [SerializeField] private float maxBoost = 0.5f;
        [Tooltip("Peak hull heat (damage per second) at/inside the core radius.")]
        [SerializeField] private float maxHeat = 30f;

        /// <summary>
        /// Slingshot boost for a ship at <paramref name="distance"/> from the sun: 0 at/outside
        /// <paramref name="outerRadius"/>, ramping monotonically to <paramref name="maxBoost"/> by
        /// <paramref name="dangerRadius"/>, and holding at <paramref name="maxBoost"/> further in.
        /// </summary>
        public static float BoostFactor(float distance, float outerRadius, float dangerRadius, float maxBoost)
            => maxBoost * Mathf.InverseLerp(outerRadius, dangerRadius, distance);

        /// <summary>
        /// Hull heat (damage/second) for a ship at <paramref name="distance"/> from the sun: 0 at/outside
        /// <paramref name="dangerRadius"/>, ramping monotonically to <paramref name="maxHeat"/> by
        /// <paramref name="coreRadius"/>, and clamping at <paramref name="maxHeat"/> inside the core.
        /// </summary>
        public static float HeatPerSecond(float distance, float dangerRadius, float coreRadius, float maxHeat)
            => maxHeat * Mathf.InverseLerp(dangerRadius, coreRadius, distance);

        /// <summary>Universe-frame distance from <paramref name="worldPosition"/> to the sun (see class note on the frame).</summary>
        public float DistanceTo(Vector3 worldPosition) => Vector3.Distance(transform.position, worldPosition);

        /// <summary>Boost a ship/enemy at <paramref name="worldPosition"/> would receive (player → pass <c>Vector3.zero</c>).</summary>
        public float BoostAt(Vector3 worldPosition)
            => BoostFactor(DistanceTo(worldPosition), outerRadius, dangerRadius, maxBoost);

        /// <summary>Heat (damage/second) a ship/enemy at <paramref name="worldPosition"/> would take (player → pass <c>Vector3.zero</c>).</summary>
        public float HeatAt(Vector3 worldPosition)
            => HeatPerSecond(DistanceTo(worldPosition), dangerRadius, coreRadius, maxHeat);

        // ---- Intended integration hooks (in-headset hand-off — deliberately NOT wired here) ----
        // 1. Boost → flight: in ShipController.Update, fold an additive speed/turn multiplier from
        //    well.BoostAt(Vector3.zero) into the universe-frame motion only (never the rig). Tune
        //    maxBoost so peak acceleration stays inside the ComfortVignette-mitigated band.
        // 2. Heat → health: mirror AsteroidHazard's cooldown-damage pattern — each tick apply
        //    new DamageInfo(well.HeatAt(Vector3.zero) * dt, ...) to the player Health (and enemies via
        //    well.HeatAt(enemy.transform.position)). Heat is already a per-second rate, so scale by dt.
    }
}
