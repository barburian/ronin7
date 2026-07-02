using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Spawns a fixed squad of enemy ships (guards) either around a target transform (blockade mode)
    /// or around the player (pursuit mode). Tracks kills and publishes <see cref="SpaceEncounterCleared"/>
    /// when all are destroyed. Gated by campaign milestones (completed scenes, story flags).
    /// Models its spawn/frame conventions closely on <see cref="SpaceEncounterManager"/>.
    /// </summary>
    public class GuardEncounter : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ShipController player;
        [Tooltip("Moving-world root the spawned ships are parented under.")]
        [SerializeField] private Transform universe;
        [Tooltip("Shared bolt pool handed to every spawned ship.")]
        [SerializeField] private ProjectilePool pool;
        [Tooltip("Enemy ship prefab. If null, builds grey-box fighters at runtime.")]
        [SerializeField] private EnemyShip enemyPrefab;
        [SerializeField] private EnemyShipDefinition definition;
        [Tooltip("Optional dialogue played once when this encounter spawns (comms chatter).")]
        [SerializeField] private DialoguePlayer spawnDialogue;

        [Header("Spawn mode")]
        [Tooltip("Target transform to blockade. Null = pursuit mode: spawn around player instead.")]
        [SerializeField] private Transform guardTarget;
        [SerializeField, Range(1, 8)] private int shipCount = 3;
        [SerializeField] private float spawnRadius = 120f;

        [Header("Blockade only")]
        [Tooltip("Blockade: arm when player is within this range of guardTarget (universe units). Ignored in pursuit.")]
        [SerializeField] private float activateRange = 400f;

        [Header("Pursuit only")]
        [Tooltip("Pursuit: delay after scene start before spawning.")]
        [SerializeField] private float initialDelay = 6f;

        [Header("Campaign gating")]
        [Tooltip("Only activate if this scene has been completed. Empty = no requirement.")]
        [SerializeField] private string requiredCompletedScene = "";
        [Tooltip("Never activate if this scene has been completed. Empty = no suppression.")]
        [SerializeField] private string suppressIfCompletedScene = "";
        [Tooltip("Story flag: if set, this encounter never activates. Empty = no flag check.")]
        [SerializeField] private string clearedFlag = "";

        [Header("Non-lethal (disable instead of destroy)")]
        [SerializeField] private bool disableInsteadOfDestroy = false;
        [SerializeField, Range(0f, 1f)] private float disableHealthFraction = 0.2f;

        private List<EnemyShip> alive = new();
        private bool spawned;
        private float pursuitStartTime;
        private readonly System.Random rng = new();

        private void Awake()
        {
            if (player == null) player = ShipController.Instance;
            if (universe == null && player != null) universe = player.Universe;
            if (definition == null)
            {
                Debug.LogWarning("[GuardEncounter] No EnemyShipDefinition assigned — using default stats.", this);
                definition = ScriptableObject.CreateInstance<EnemyShipDefinition>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
            EventBus.Subscribe<EnemyShipDisabled>(OnEnemyDisabled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
            EventBus.Unsubscribe<EnemyShipDisabled>(OnEnemyDisabled);
        }

        private void Start()
        {
            pursuitStartTime = Time.time;
        }

        private void Update()
        {
            if (spawned) return;

            // Check activation gate: required completed, not suppressed, flag not set
            bool requiredDone = string.IsNullOrEmpty(requiredCompletedScene) || CampaignState.IsCompleted(requiredCompletedScene);
            bool suppressedDone = !string.IsNullOrEmpty(suppressIfCompletedScene) && CampaignState.IsCompleted(suppressIfCompletedScene);
            bool alreadyCleared = !string.IsNullOrEmpty(clearedFlag) && CampaignState.HasFlag(clearedFlag);

            if (!ShouldActivate(requiredDone, suppressedDone, alreadyCleared))
                return;

            // Blockade: spawn when player is within range
            if (guardTarget != null)
            {
                Vector3 targetPos = universe != null
                    ? universe.InverseTransformPoint(guardTarget.position)
                    : guardTarget.position;
                Vector3 playerPos = player != null ? player.ShipPosition : Vector3.zero;
                float dist = Vector3.Distance(playerPos, targetPos);
                if (dist <= activateRange)
                    Spawn();
            }
            // Pursuit: spawn after initial delay
            else
            {
                if (Time.time >= pursuitStartTime + initialDelay)
                    Spawn();
            }
        }

        /// <summary>Pure static decision: should this encounter activate?</summary>
        public static bool ShouldActivate(bool requiredDone, bool suppressedDone, bool alreadyCleared)
            => requiredDone && !suppressedDone && !alreadyCleared;

        private void Spawn()
        {
            spawned = true;
            alive.Clear();

            // Determine spawn center: either the guard target (blockade) or player virtual position (pursuit)
            Vector3 center = guardTarget != null
                ? (universe != null ? universe.InverseTransformPoint(guardTarget.position) : guardTarget.position)
                : (player != null ? player.ShipPosition : Vector3.zero);

            // Ring the ships around the center
            float angleOffset = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < shipCount; i++)
            {
                float a = angleOffset + (i / Mathf.Max(1f, shipCount)) * Mathf.PI * 2f;
                Vector3 localPos = center + new Vector3(Mathf.Cos(a) * spawnRadius, 0f, Mathf.Sin(a) * spawnRadius);
                var ship = SpawnEnemy(localPos);
                if (ship != null) alive.Add(ship);
            }

            EventBus.Publish(new SpaceEncounterStarted(0, alive.Count));
            if (spawnDialogue != null) spawnDialogue.Play();
            Log.Info($"[GuardEncounter] {alive.Count} ships spawned.");
        }

        /// <summary>Spawn one enemy at a universe-local position, facing the spawn center.</summary>
        private EnemyShip SpawnEnemy(Vector3 universeLocalPos)
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

            // Face toward the center (where player/target is)
            Vector3 centerLocal = guardTarget != null
                ? (universe != null ? universe.InverseTransformPoint(guardTarget.position) : guardTarget.position)
                : (player != null ? player.ShipPosition : Vector3.zero);
            Vector3 toCenter = centerLocal - universeLocalPos;
            if (universe != null)
            {
                Vector3 worldTarget = universe.TransformPoint(centerLocal);
                Vector3 look = worldTarget - ship.transform.position;
                if (look.sqrMagnitude > 0.01f) ship.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
            else if (toCenter.sqrMagnitude > 0.01f)
            {
                ship.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            }

            ship.Configure(definition, universe, pool, player);
            if (disableInsteadOfDestroy) ship.SetNonLethal(true, disableHealthFraction);
            return ship;
        }

        /// <summary>Runtime grey-box fighter (no prefab needed).</summary>
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

            // Body: stretched cube
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(6f, 2.5f, 9f);
            var bodyRenderer = body.GetComponent<Renderer>();

            // Nose marker
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 6f);
            nose.transform.localScale = new Vector3(2f, 1.5f, 4f);

            // Muzzle
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 8f);

            var ship = root.AddComponent<EnemyShip>();
            ship.WireGreyBox(bodyRenderer, muzzle.transform);
            return ship;
        }

        private void RemoveAndCheckCleared(EnemyShip ship)
        {
            alive.Remove(ship);
            if (alive.Count == 0)
            {
                if (!string.IsNullOrEmpty(clearedFlag))
                    CampaignState.SetFlag(clearedFlag);
                EventBus.Publish(new SpaceEncounterCleared(0));
                Log.Info("[GuardEncounter] All guards cleared.");
            }
        }

        private void OnEnemyDestroyed(EnemyShipDestroyed evt)
        {
            var ship = evt.Ship.GetComponent<EnemyShip>();
            if (!alive.Contains(ship)) return;
            RemoveAndCheckCleared(ship);
        }

        private void OnEnemyDisabled(EnemyShipDisabled evt)
        {
            var ship = evt.Ship.GetComponent<EnemyShip>();
            if (!alive.Contains(ship)) return;
            RemoveAndCheckCleared(ship);
        }
    }
}
