using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Spawns waves of <see cref="EnemyShip"/>s around the player's virtual position and advances to
    /// the next wave once the current one is cleared. The space-combat analogue of
    /// <see cref="Ronin7.Enemies.EncounterManager"/>, but built for the moving-universe frame.
    ///
    /// FRAME OF REFERENCE: enemy ships must be children of the <c>universe</c> transform so they
    /// share the moving-world frame (see <see cref="EnemyShip"/>). We therefore spawn them at
    /// universe-LOCAL positions ringed around <see cref="ShipController.ShipPosition"/> (the
    /// player's virtual position) and parent them under <c>universe</c>. We DON'T place them in
    /// world space, or they'd appear glued to the cockpit instead of out in the world.
    ///
    /// Clear-tracking listens for <see cref="EnemyShipDestroyed"/> on the EventBus (decoupled), and
    /// publishes <see cref="SpaceEncounterStarted"/>/<see cref="SpaceEncounterCleared"/>.
    /// </summary>
    public class SpaceEncounterManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ShipController player;
        [Tooltip("Moving-world root the spawned ships are parented under.")]
        [SerializeField] private Transform universe;
        [Tooltip("Shared bolt pool handed to every spawned ship so they fire without per-ship pools.")]
        [SerializeField] private ProjectilePool pool;
        [Tooltip("Enemy ship prefab. If null, the manager builds a grey-box fighter at runtime.")]
        [SerializeField] private EnemyShip enemyPrefab;
        [SerializeField] private EnemyShipDefinition definition;

        [Header("Waves")]
        [Tooltip("Number of waves before the encounter is fully cleared. 0 = endless (always respawn).")]
        [SerializeField, Range(0, 10)] private int waveCount = 3;
        [SerializeField, Range(1, 8)] private int enemiesPerWave = 2;
        [Tooltip("Extra enemies added per wave to ramp difficulty.")]
        [SerializeField, Range(0, 4)] private int enemiesAddedPerWave = 1;
        [Tooltip("Extra enemies per galaxy the player has finished (CampaignState.GalaxiesCompleted), " +
                 "so ambient hostiles scale with story progress.")]
        [SerializeField, Range(0, 4)] private int enemiesAddedPerGalaxy = 0;
        [Tooltip("Hard ceiling on ships per wave AFTER the ramp/galaxy bonuses. Caps the computed count " +
                 "so it can never exceed the SHARED bolt pool (every ship fires from one fixed-capacity " +
                 "ProjectilePool) or pile on more attackers than the player can fairly fight.")]
        [SerializeField, Range(1, 12)] private int maxEnemiesPerWave = 8;
        [Tooltip("Seconds of calm between clearing a wave and the next one warping in.")]
        [SerializeField] private float interWaveDelay = 4f;
        [Tooltip("Seconds before the FIRST wave spawns (lets the player settle into flight).")]
        [SerializeField] private float initialDelay = 3f;

        [Header("Elite variety (optional)")]
        [Tooltip("Tougher ship spawned in place of some base ships once a wave qualifies. Authored by the " +
                 "designer as a distinct (stat/visual) EnemyShipDefinition asset, so elites read as a real " +
                 "threat spike rather than 'more of the same'. LEAVE NULL to disable elites entirely — waves " +
                 "then stay pure base ships and behaviour is unchanged.")]
        [SerializeField] private EnemyShipDefinition eliteDefinition;
        [Tooltip("Wave index (0-based) at/after which elites start appearing. Ignored when eliteDefinition is null.")]
        [SerializeField, Range(0, 6)] private int elitesFromWave = 2;
        [Tooltip("How many of a qualifying wave's slots use eliteDefinition instead of the base definition " +
                 "(capped at the wave's total count). Ignored when eliteDefinition is null.")]
        [SerializeField, Range(0, 4)] private int elitesPerWave = 1;

        [Header("Campaign gating")]
        [Tooltip("When set, no waves spawn until Galaxy1Progress.FirstPlanetDeparted is true — the " +
                 "opening flight to the first story planet stays calm; hostiles appear once the " +
                 "player has been down to the surface and returned to space.")]
        [SerializeField] private bool requireFirstPlanetDeparture = false;

        [Header("Tied to travel")]
        [Tooltip("Universe units the player must FLY (since the last wave) before the next wave warps in. " +
                 "This is what makes encounters 'tied to travel' rather than a pure timer: stay put and " +
                 "space stays quiet; cover distance and you run into hostiles. 0 = ignore travel (timer only).")]
        [SerializeField] private float travelBetweenWaves = 150f;

        [Header("Spawn geometry (universe units, around the player's virtual position)")]
        [Tooltip("Distance from the player the ships warp in at — comfortably beyond their preferred range.")]
        [SerializeField] private float spawnDistance = 220f;
        [Tooltip("Random vertical spread so a wave isn't a flat ring.")]
        [SerializeField] private float spawnVerticalSpread = 40f;

        private readonly List<EnemyShip> alive = new();
        private int waveIndex = -1;
        private int aliveCount;
        private float nextWaveTime;
        private bool encounterComplete;
        private bool gateOpened;
        // Player's virtual position when the last wave cleared; the next wave is gated on flying
        // 'travelBetweenWaves' away from this point so encounters are paced by travel, not just time.
        private Vector3 travelAnchor;
        private readonly System.Random rng = new();

        private void Awake()
        {
            if (player == null) player = ShipController.Instance;
            if (universe == null && player != null) universe = player.Universe;
            if (definition == null)
            {
                Debug.LogWarning("[SpaceEncounter] No EnemyShipDefinition assigned — using default stats.", this);
                definition = ScriptableObject.CreateInstance<EnemyShipDefinition>();
            }
        }

        private void OnEnable() => EventBus.Subscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
        private void OnDisable() => EventBus.Unsubscribe<EnemyShipDestroyed>(OnEnemyDestroyed);

        private void Start()
        {
            nextWaveTime = Time.time + initialDelay;
            travelAnchor = player != null ? player.ShipPosition : Vector3.zero;
        }

        private void Update()
        {
            if (encounterComplete) return;

            // Campaign gate: stay silent until the first story planet has been visited. When the
            // gate opens mid-scene, re-arm the timer and travel anchor so the first wave doesn't
            // fire instantly off counters that ran while the manager was idle.
            if (requireFirstPlanetDeparture && !gateOpened)
            {
                if (!Galaxy1Progress.FirstPlanetDeparted) return;
                gateOpened = true;
                nextWaveTime = Time.time + initialDelay;
                travelAnchor = player != null ? player.ShipPosition : travelAnchor;
            }

            // Spawn the next wave when the arena is empty, the calm delay has elapsed, AND the player
            // has covered enough distance since the last wave — so encounters are tied to travel.
            if (aliveCount <= 0 && Time.time >= nextWaveTime && HasTravelledEnough())
                SpawnNextWave();
        }

        /// <summary>True once the player has flown <see cref="travelBetweenWaves"/> from the last wave's anchor.</summary>
        private bool HasTravelledEnough()
        {
            if (travelBetweenWaves <= 0f || player == null) return true;
            return Vector3.Distance(player.ShipPosition, travelAnchor) >= travelBetweenWaves;
        }

        private void SpawnNextWave()
        {
            waveIndex++;

            if (waveCount > 0 && waveIndex >= waveCount)
            {
                encounterComplete = true;
                EventBus.Publish(new SpaceEncounterCleared(waveIndex));
                Log.Info("[SpaceEncounter] All waves cleared.");
                return;
            }

            // Pure composition: ramp + galaxy bonus, clamped to the pool-safe ceiling, plus how many
            // of those slots are elites. A null eliteDefinition forces zero elites (pure base waves).
            int count = ComposeWave(
                waveIndex, CampaignState.GalaxiesCompleted,
                enemiesPerWave, enemiesAddedPerWave, enemiesAddedPerGalaxy,
                maxEnemiesPerWave, elitesFromWave,
                eliteDefinition != null ? elitesPerWave : 0,
                out int eliteCount);
            Vector3 center = player != null ? player.ShipPosition : Vector3.zero;
            travelAnchor = center; // re-anchor: the next wave is gated on travel from HERE

            alive.Clear();
            float angleOffset = (float)rng.NextDouble() * Mathf.PI * 2f; // random ring rotation per wave
            for (int i = 0; i < count; i++)
            {
                float a = angleOffset + (i / Mathf.Max(1f, count)) * Mathf.PI * 2f;
                float y = ((float)rng.NextDouble() - 0.5f) * 2f * spawnVerticalSpread;
                Vector3 localPos = center + new Vector3(Mathf.Cos(a) * spawnDistance, y, Mathf.Sin(a) * spawnDistance);
                // First 'eliteCount' slots fly the tougher hull; the rest are the base definition.
                EnemyShipDefinition def = i < eliteCount ? eliteDefinition : definition;
                var ship = SpawnEnemy(localPos, def);
                if (ship != null) alive.Add(ship);
            }

            aliveCount = alive.Count;
            EventBus.Publish(new SpaceEncounterStarted(waveIndex, aliveCount));
            Log.Info($"[SpaceEncounter] Wave {waveIndex} — {aliveCount} ships incoming.");
        }

        /// <summary>
        /// Pure wave-composition math (no scene state), so the pool-safety clamp and the elite cadence
        /// are unit-testable and can't be bypassed by a runtime path. Returns the wave's total ship
        /// count and, via <paramref name="eliteCount"/>, how many of those slots are elites:
        /// <list type="bullet">
        /// <item><b>total</b> = base + perWave*<paramref name="waveIndex"/> + perGalaxy*galaxies,
        /// clamped to [1, <paramref name="maxPerWave"/>] — never empties a wave, never exceeds the cap.</item>
        /// <item><b>elites</b> appear only at/after <paramref name="elitesFromWave"/>, are capped at
        /// <paramref name="elitesPerWave"/>, and can never exceed the total. Pass
        /// <paramref name="elitesPerWave"/> = 0 (e.g. when no elite asset is assigned) for none.</item>
        /// </list>
        /// </summary>
        public static int ComposeWave(
            int waveIndex, int galaxiesCompleted,
            int basePerWave, int addedPerWave, int addedPerGalaxy,
            int maxPerWave, int elitesFromWave, int elitesPerWave,
            out int eliteCount)
        {
            int total = basePerWave + addedPerWave * waveIndex + addedPerGalaxy * galaxiesCompleted;
            total = Mathf.Min(total, maxPerWave); // pool-safety / fairness ceiling
            total = Mathf.Max(1, total);          // a wave is never empty

            eliteCount = (elitesPerWave > 0 && waveIndex >= elitesFromWave)
                ? Mathf.Min(elitesPerWave, total)
                : 0;
            return total;
        }

        /// <summary>
        /// Spawn one enemy at a universe-LOCAL position under <c>universe</c>, facing roughly toward
        /// the player so it reads as an incoming attacker, and wire its shared refs. The caller picks
        /// which <paramref name="def"/> the ship runs (base or elite) so a single wave can mix hulls.
        /// </summary>
        private EnemyShip SpawnEnemy(Vector3 universeLocalPos, EnemyShipDefinition def)
        {
            EnemyShip ship;
            if (enemyPrefab != null)
            {
                ship = Instantiate(enemyPrefab, universe);
                if (universe != null) ship.transform.position = universe.TransformPoint(universeLocalPos);
                else ship.transform.position = universeLocalPos;
            }
            else
            {
                ship = BuildGreyBoxFighter(universeLocalPos);
            }

            // Face the player at spawn (world-space look toward where the player virtually is).
            if (universe != null)
            {
                Vector3 playerLocal = player != null ? player.ShipPosition : Vector3.zero;
                Vector3 worldTarget = universe.TransformPoint(playerLocal);
                Vector3 look = worldTarget - ship.transform.position;
                if (look.sqrMagnitude > 0.01f) ship.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            }

            ship.Configure(def, universe, pool, player);
            return ship;
        }

        /// <summary>
        /// Runtime grey-box fighter (no prefab needed): a tinted body + nose so the scene functions
        /// from primitives, matching the builder's grey-box style. Lives under <c>universe</c>.
        /// </summary>
        private EnemyShip BuildGreyBoxFighter(Vector3 universeLocalPos)
        {
            var root = new GameObject("Enemy Ship");
            if (universe != null)
            {
                root.transform.SetParent(universe, false);
                root.transform.localPosition = universeLocalPos;
            }
            else root.transform.position = universeLocalPos;

            root.AddComponent<Health>();

            // Body: a stretched cube with a SphereCollider-free box collider for bolt hits.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(6f, 2.5f, 9f);
            var bodyRenderer = body.GetComponent<Renderer>();

            // Nose marker so its facing/forward is legible.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 6f);
            nose.transform.localScale = new Vector3(2f, 1.5f, 4f);

            // Muzzle at the nose tip.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 8f);

            var ship = root.AddComponent<EnemyShip>();
            ship.WireGreyBox(bodyRenderer, muzzle.transform);
            return ship;
        }

        private void OnEnemyDestroyed(EnemyShipDestroyed evt)
        {
            aliveCount = Mathf.Max(0, aliveCount - 1);
            if (aliveCount == 0 && !encounterComplete)
            {
                EventBus.Publish(new SpaceEncounterCleared(waveIndex));
                nextWaveTime = Time.time + interWaveDelay;
                // Re-anchor here so the player must fly onward (not just wait) for the next wave.
                travelAnchor = player != null ? player.ShipPosition : travelAnchor;
                Log.Info($"[SpaceEncounter] Wave {waveIndex} cleared.");
            }
        }
    }
}
