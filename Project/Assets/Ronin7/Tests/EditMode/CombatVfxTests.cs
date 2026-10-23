using NUnit.Framework;
using Ronin7.Audio.Vfx;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Phase 3 combat-VFX tests: the pool recycles instead of instantiating per call, the controller
    /// subscribes/unsubscribes cleanly with no leaked handlers, and the built prefabs honour the Quest
    /// caps (maxParticles + playOnAwake=false).
    /// </summary>
    public class CombatVfxTests
    {
        [SetUp] public void SetUp() => EventBus.Clear();
        [TearDown] public void TearDown() => EventBus.Clear();

        [Test]
        public void VfxPool_ReusesInstances_DoesNotInstantiatePerCall()
        {
            var parent = new GameObject("PoolParent").transform;
            var prefab = new GameObject("Fx").AddComponent<ParticleSystem>();
            try
            {
                const int capacity = 4;
                var pool = new VfxPool(parent, capacity);

                for (int i = 0; i < 12; i++) // 3× the capacity
                    pool.Play(prefab, Vector3.zero, 5);

                // One ring of `capacity` instances was allocated once and reused — never 12.
                Assert.AreEqual(capacity, pool.Instantiations,
                    "VfxPool should allocate exactly `capacity` instances per prefab and recycle them.");
            }
            finally
            {
                Object.DestroyImmediate(prefab.gameObject);
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void Controller_SubscribesOnSubscribe_AndUnsubscribesWithNoLeaks()
        {
            var go = new GameObject("CombatVfx");
            var controller = go.AddComponent<CombatVfxController>();
            try
            {
                controller.Subscribe();
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, null));
                Assert.AreEqual(1, controller.HandledCount, "Handler should fire while subscribed.");

                controller.Unsubscribe();
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, null));
                Assert.AreEqual(1, controller.HandledCount, "Handler must not fire after Unsubscribe (no leaked delegate).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Controller_HandlesHitResistedAndWeaknessExploited()
        {
            var go = new GameObject("CombatVfx");
            var controller = go.AddComponent<CombatVfxController>();
            try
            {
                controller.Subscribe();
                EventBus.Publish(new HitResisted(Vector3.zero, null));
                Assert.AreEqual(1, controller.HandledCount);
                EventBus.Publish(new WeaknessExploited(Vector3.zero, null, 1));
                Assert.AreEqual(2, controller.HandledCount);

                controller.Unsubscribe();
                EventBus.Publish(new HitResisted(Vector3.zero, null));
                EventBus.Publish(new WeaknessExploited(Vector3.zero, null, 1));
                Assert.AreEqual(2, controller.HandledCount, "No leaked handlers after Unsubscribe.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AllVfxPrefabs_AreQuestCapped_AdditiveAndZWriteOff()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Ronin7/Resources/Vfx" });
            if (guids.Length == 0) Assert.Inconclusive("VFX prefabs not built yet.");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var ps = go.GetComponent<ParticleSystem>();
                Assert.LessOrEqual(ps.main.maxParticles, 30, path);
                Assert.IsFalse(ps.collision.enabled || ps.lights.enabled || ps.subEmitters.enabled || ps.trails.enabled, path);
                var mat = go.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.IsNotNull(mat, path);
                Assert.AreEqual(0f, mat.GetFloat("_ZWrite"), path + " must be ZWrite Off");
            }
        }

        [Test]
        public void DeathDissolve_Progress_WaitsForTopple_ThenRamps()
        {
            Assert.AreEqual(0f, DeathDissolve.Progress(0.2f));
            Assert.AreEqual(0.5f, DeathDissolve.Progress(DeathDissolve.Delay + DeathDissolve.Duration * 0.5f), 1e-4f);
            Assert.AreEqual(1f, DeathDissolve.Progress(5f));
        }

        [Test]
        public void SavedEffectMaterials_Exist()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Resources/Vfx/MirrorGhost.mat") == null)
                Assert.Inconclusive("Run Build Combat VFX Prefabs.");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Resources/Vfx/EnemyPBR_Dissolve.mat"));
        }

        [Test]
        public void EmitCount_ScalesWithParticleScale()
        {
            float old = GraphicsRuntime.ParticleScale;
            try
            {
                GraphicsRuntime.ParticleScale = 1f;
                Assert.AreEqual(20, CombatVfxController.ScaledCount(20));
                GraphicsRuntime.ParticleScale = 0.4f;
                Assert.AreEqual(8, CombatVfxController.ScaledCount(20));
                Assert.AreEqual(1, CombatVfxController.ScaledCount(1));
            }
            finally { GraphicsRuntime.ParticleScale = old; }
        }

        [Test]
        public void BuiltPrefab_IsQuestCapped_AndNotPlayOnAwake()
        {
            const string path = "Assets/Ronin7/Resources/Vfx/SwordSpark.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Assert.Inconclusive("VFX prefabs not built yet — run Tools/Space Samurai/Art/Build Combat VFX Prefabs.");

            var ps = prefab.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, "Built VFX prefab must carry a ParticleSystem.");
            var main = ps.main;
            Assert.LessOrEqual(main.maxParticles, 30, "maxParticles must stay within the Quest budget (≤30).");
            Assert.IsFalse(main.playOnAwake, "VFX prefab must not play on awake (the pool drives it).");
            Assert.LessOrEqual(main.startLifetime.constantMax, 0.4f, "Lifetime must be short (≤0.4s).");
        }

        [Test]
        public void BladeTrail_EmitsOnlyAbove2ms_AndStaysUnder60Vertices()
        {
            Assert.IsFalse(Ronin7.Player.BladeTrailDriver.ShouldEmit(2f));
            Assert.IsTrue(Ronin7.Player.BladeTrailDriver.ShouldEmit(2.1f));
            // vertices = speed * time / minVertexDistance, at the fastest swing
            float verts = Ronin7.Player.BladeTrailDriver.TopSpeed * Ronin7.Player.BladeTrailDriver.TrailTime / Ronin7.Player.BladeTrailDriver.MinVertexDistance;
            Assert.LessOrEqual(verts, 60.01f);
        }

        [Test]
        public void ImpactDecals_NeverExceedSixteenLive_AndExpire()
        {
            var parent = new GameObject("Decals").transform;
            var mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                var d = new ImpactDecals(parent, mat);
                for (int i = 0; i < 40; i++) d.Place(new Vector3(i, 0, 0), Vector3.up, 0f);
                Assert.AreEqual(16, d.Live);
                Assert.AreEqual(16, parent.childCount, "Pooled: 16 quads, never one per hit.");
                Assert.IsFalse(d.Tick(ImpactDecals.Life + 0.1f));
                Assert.AreEqual(0, d.Live);
            }
            finally { Object.DestroyImmediate(parent.gameObject); Object.DestroyImmediate(mat); }
        }

        [Test]
        public void Afterimage_CapturesMeshes_FadesOutAfterPointThreeSeconds()
        {
            var parent = new GameObject("Ghosts").transform;
            var src = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                var a = new Afterimage(parent, mat);
                a.Capture(new[] { src.transform });
                Assert.AreEqual(1, a.PartCount);
                Assert.IsFalse(a.Visible, "Hidden until the rig lands.");
                a.Show(0f);
                Assert.IsTrue(a.Tick(0.1f));
                Assert.IsFalse(a.Tick(Afterimage.FadeTime + 0.01f));
                Assert.IsFalse(a.Visible);
            }
            finally { Object.DestroyImmediate(parent.gameObject); Object.DestroyImmediate(src); Object.DestroyImmediate(mat); }
        }

        [Test]
        public void BoonTint_EpicIsMagenta_AndShellSpecMatchesRoadmap()
        {
            var epic = CombatVfxController.BoonTint(BoonRarity.Epic);
            Assert.Greater(epic.r, 200); Assert.Less(epic.g, 100); Assert.Greater(epic.b, 140);
            Assert.AreEqual(1.2f, UnbrokenShell.Radius);
            Assert.AreEqual(0.4f, UnbrokenShell.Duration);
        }

        [Test]
        public void Controller_HandlesPhaseStepSkillWorldHitSpawnAndBoon()
        {
            var go = new GameObject("CombatVfx");
            var c = go.AddComponent<CombatVfxController>();
            try
            {
                c.Subscribe();
                EventBus.Publish(new PhaseStepStarted(Vector3.zero));
                EventBus.Publish(new SkillVfx(SkillVfx.ChargedSlash, Vector3.zero, Vector3.forward));
                EventBus.Publish(new SwordWorldHit(Vector3.zero, Vector3.up));
                EventBus.Publish(new EnemyActivated(null));
                EventBus.Publish(new BoonChosen("none"));
                Assert.AreEqual(5, c.HandledCount);
                c.Unsubscribe();
                EventBus.Publish(new SwordWorldHit(Vector3.zero, Vector3.up));
                Assert.AreEqual(5, c.HandledCount);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShellPrefabAndCrescent_AreBuilt()
        {
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ronin7/Resources/VfxShell/UnbrokenShell.prefab");
            if (shell == null) Assert.Inconclusive("Run Build Combat VFX Prefabs.");
            Assert.IsNotNull(shell.GetComponent<MeshFilter>().sharedMesh);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ronin7/Resources/Vfx/SlashCrescent.prefab"));
        }
    }
}
