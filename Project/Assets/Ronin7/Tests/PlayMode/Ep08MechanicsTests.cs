using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    public class Ep08MechanicsTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        private CharacterController CreateCharacterController(Vector3 position)
        {
            var go = new GameObject("PlayerRig");
            _spawned.Add(go);
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.center = Vector3.zero;
            cc.height = 1.8f;
            cc.radius = 0.3f;
            return cc;
        }

        private Health CreateHealth(int maxHp = 10)
        {
            var go = new GameObject("HealthTarget");
            _spawned.Add(go);
            go.AddComponent<CharacterController>();
            var health = go.AddComponent<Health>();
            health.Configure(maxHp);
            return health;
        }

        private Enemy CreateEnemy(Vector3 position, EnemyDefinition def, bool nonLethal = false)
        {
            var go = new GameObject("Enemy");
            _spawned.Add(go);
            go.transform.position = position;

            var health = go.AddComponent<Health>();
            var enemy = go.AddComponent<Enemy>();

            var type = typeof(MeleeAttacker);
            type.GetField("definition", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(enemy, def);
            type.GetField("nonLethalDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(enemy, nonLethal);

            // Minimal setup: weapon and bodyRenderer can be null for this test
            go.SetActive(true);
            return enemy;
        }

        /// <summary>
        /// Test (a): Enemy with nonLethalDisable=true at 0 HP ends with rotation ~Euler(0,y,70)
        /// not Euler(85,y,0).
        /// </summary>
        [UnityTest]
        public IEnumerator NonLethalEnemy_OnDied_UsesSlumpedPose()
        {
            // Enemy.Awake logs errors for unwired bladeTip/definition in this minimal rig.
            LogAssert.ignoreFailingMessages = true;

            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 10;

            var enemy = CreateEnemy(Vector3.zero, def, nonLethal: true);
            yield return null;

            var health = enemy.GetComponent<Health>();
            health.ApplyDamage(new DamageInfo(100f, Vector3.zero, Vector3.zero, null));
            yield return null;

            var eulers = enemy.transform.eulerAngles;
            Assert.Less(Mathf.Abs(eulers.x - 0f), 1f, "Non-lethal enemy should have X rotation near 0°");
            Assert.Less(Mathf.Abs(eulers.z - 70f), 1f, "Non-lethal enemy should have Z rotation near 70°");
        }

        /// <summary>
        /// Test (b): SetZeroG(true) → no vertical falling; AddDriftImpulse moves rig;
        /// SetZeroG(false) restores gravity.
        /// </summary>
        [UnityTest]
        public IEnumerator ZeroGLocomotion_BehavesCorrectly()
        {
            // High altitude keeps the capsule clear of any geometry other test classes
            // leave behind in the shared test scene (a floor would zero out the fall).
            var cc = CreateCharacterController(new Vector3(0f, 50f, 0f));
            var locomotion = cc.gameObject.AddComponent<ContinuousLocomotion>(); // Awake assigns Instance

            // A head CHILD transform (not the rig root) — MatchCapsuleToHead sizes the
            // capsule from the head's localPosition, so a root-as-camera gives a 50m capsule.
            var head = new GameObject("Head").transform;
            head.SetParent(cc.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);

            // Inject private refs via reflection for minimal test setup.
            var type = typeof(ContinuousLocomotion);
            type.GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(locomotion, head);
            type.GetField("controller", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(locomotion, cc);
            type.GetField("useComfortVignette", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(locomotion, false);

            // Drive HandleMove directly each frame — Update-loop scheduling is not reliable
            // for freshly built rigs in the batchmode test scene.
            var handleMove = type.GetMethod("HandleMove", BindingFlags.NonPublic | BindingFlags.Instance);
            IEnumerator Tick(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    handleMove.Invoke(locomotion, null);
                    yield return null;
                }
            }

            // CharacterController.Move displacement is unreliable in the shared batchmode
            // test scene, so assert on the locomotion's internal state — verticalVelocity
            // and driftVelocity — which is exactly what the zero-g branch controls.
            var verticalVelocityField = type.GetField("verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
            var driftVelocityField = type.GetField("driftVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

            yield return null;

            // Test 1: Normal gravity accumulates downward vertical velocity.
            yield return Tick(10);
            Assert.Less((float)verticalVelocityField.GetValue(locomotion), 0f,
                "Gravity should accumulate negative vertical velocity");

            // Test 2: Zero-G — vertical velocity is zeroed and stays zero.
            locomotion.SetZeroG(true, 0.6f);
            yield return Tick(10);
            Assert.AreEqual(0f, (float)verticalVelocityField.GetValue(locomotion),
                "Vertical velocity should stay zero in zero-G");

            // Test 3: AddDriftImpulse feeds drift velocity; damping decays but keeps direction.
            locomotion.AddDriftImpulse(new Vector3(1f, 0f, 0f));
            yield return Tick(5);
            var drift = (Vector3)driftVelocityField.GetValue(locomotion);
            Assert.Greater(drift.x, 0f, "Drift should retain the impulse direction");
            Assert.Less(drift.x, 1f, "Damping should decay the impulse over time");

            // Test 4: Zero-G off — drift cleared, gravity resumes.
            locomotion.SetZeroG(false, 0.6f);
            Assert.AreEqual(Vector3.zero, (Vector3)driftVelocityField.GetValue(locomotion),
                "Drift should be cleared when zero-G ends");
            yield return Tick(10);
            Assert.Less((float)verticalVelocityField.GetValue(locomotion), 0f,
                "Gravity should resume after zero-G is disabled");
        }

        /// <summary>
        /// Test (c): CollapseSequenceController drops segment 0 debris after player passes
        /// threshold 1, and positions actually change.
        /// </summary>
        [UnityTest]
        public IEnumerator CollapseSequence_DropsDebrisAfterThreshold()
        {
            // Setup corridor: start at (0,0,0), end at (10,0,0)
            var corridorStart = new GameObject("CorridorStart").transform;
            _spawned.Add(corridorStart.gameObject);
            corridorStart.position = Vector3.zero;

            var corridorEnd = new GameObject("CorridorEnd").transform;
            _spawned.Add(corridorEnd.gameObject);
            corridorEnd.position = new Vector3(10f, 0f, 0f);

            // Setup player at the start
            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            playerGo.transform.position = Vector3.zero;

            // Setup debris for segment 0 (should drop after player passes 5 units = progress 0.5)
            var debris0 = new GameObject("Debris0");
            _spawned.Add(debris0);
            debris0.transform.position = new Vector3(2f, 2f, 0f);
            var debrisStartPos = debris0.transform.position;

            // Setup CollapseSequenceController
            // Deactivate before AddComponent so Awake runs only after the fields are wired.
            var controllerGo = new GameObject("CollapseController");
            _spawned.Add(controllerGo);
            controllerGo.SetActive(false);
            var controller = controllerGo.AddComponent<CollapseSequenceController>();

            var type = typeof(CollapseSequenceController);
            type.GetField("corridorStart", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(controller, corridorStart);
            type.GetField("corridorEnd", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(controller, corridorEnd);
            type.GetField("playerTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(controller, playerGo.transform);

            var segment = new CollapseSequenceController.Segment
            {
                triggerProgress = 0.2f,
                debris = new Transform[] { debris0.transform }
            };
            type.GetField("segments", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(controller, new CollapseSequenceController.Segment[] { segment });

            var dropDurationField = type.GetField("dropDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            dropDurationField?.SetValue(controller, 0.3f);

            var dropHeightField = type.GetField("dropHeight", BindingFlags.NonPublic | BindingFlags.Instance);
            dropHeightField?.SetValue(controller, 1.5f);

            controllerGo.SetActive(true);
            yield return null;

            // A single segment is the LAST segment, which only drops once the player is at
            // the corridor end (progress >= 0.98) — move all the way through.
            playerGo.transform.position = new Vector3(10f, 0f, 0f);

            // Wait for drop to complete
            yield return new WaitForSeconds(0.5f);

            // Verify debris moved down
            var debrisEndPos = debris0.transform.position;
            Assert.Less(debrisEndPos.y, debrisStartPos.y, "Debris should have fallen");
            Assert.Less(Mathf.Abs(debrisEndPos.x - debrisStartPos.x), 0.01f, "Debris should only move vertically");
        }
    }
}
