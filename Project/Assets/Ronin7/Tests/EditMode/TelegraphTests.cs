using System.Reflection;
using NUnit.Framework;
using Ronin7.Enemies;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P8-04: the weapon-edge telegraph, the dodge-only pose, and the per-weapon windup pitch.</summary>
    public class TelegraphTests
    {
        private GameObject body, weaponPrefab;
        [TearDown] public void TearDown() { if (body != null) Object.DestroyImmediate(body); if (weaponPrefab != null) Object.DestroyImmediate(weaponPrefab); }

        private LineRenderer Armed(Vector3 tip, Vector3 edgeBase, out Enemy enemy)
        {
            body = new GameObject("body");
            body.SetActive(false);
            var armR = new GameObject("Rig_ArmR").transform; armR.SetParent(body.transform, false);
            new GameObject(EnemyWeaponDefinition.MainSocket).transform.SetParent(armR, false);
            enemy = body.AddComponent<Enemy>();
            var lr = new GameObject("edge").AddComponent<LineRenderer>();
            lr.transform.SetParent(body.transform, false);
            lr.positionCount = 2; lr.useWorldSpace = true; lr.enabled = true;
            var so = new SerializedObject(enemy);
            so.FindProperty("weapon").objectReferenceValue = armR;
            so.FindProperty("bladeEdge").objectReferenceValue = lr;
            so.ApplyModifiedPropertiesWithoutUndo();
            var def = ScriptableObject.CreateInstance<EnemyWeaponDefinition>();
            weaponPrefab = new GameObject("W");
            def.prefab = weaponPrefab; def.tip = tip; def.edgeBase = edgeBase;
            enemy.Equip(def);
            return lr;
        }

        private static void LateUpdate(Enemy e) => typeof(MeleeAttacker).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(e, null);

        [Test]
        public void Edge_FollowsHeldWeapon()
        {
            var lr = Armed(new Vector3(0f, 0f, 0.9f), new Vector3(0f, 0f, 0.12f), out var e);
            body.transform.position = new Vector3(3f, 0f, 1f);
            LateUpdate(e);
            var attached = body.transform.Find("Rig_ArmR/" + EnemyWeaponDefinition.MainSocket).GetChild(0);
            Assert.AreEqual(attached.Find(EnemyWeaponDefinition.EdgeBaseName).position, lr.GetPosition(0));
            Assert.AreEqual(attached.Find(EnemyWeaponDefinition.TipName).position, lr.GetPosition(1));
        }

        [Test]
        public void Edge_RangedKit_RunsFromWeaponRootToTip()
        {
            var tip = new Vector3(0f, -0.7f, 0f);
            var lr = Armed(tip, tip, out var e); // ranged kits author edgeBase == tip
            LateUpdate(e);
            Assert.Greater((lr.GetPosition(1) - lr.GetPosition(0)).magnitude, 0.5f);
        }

        [Test]
        public void Edge_ReduceFlashes_NoPulse()
        {
            float prev = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float v = MeleeAttacker.EdgeIntensity(i * 0.025f, 1f, i * 0.37f, true); // phase wanders; must not matter
                Assert.GreaterOrEqual(v, prev); prev = v;
            }
            Assert.AreEqual(1f, prev, 1e-5f);
        }

        [Test]
        public void Edge_NormalMode_RampsToFullAndMayPulse()
        {
            Assert.AreEqual(0f, MeleeAttacker.EdgeIntensity(0f, 1f, 0.25f, false), 1e-5f);
            Assert.AreEqual(1f, MeleeAttacker.EdgeIntensity(1f, 1f, 0.25f, false), 1e-5f);
            Assert.Less(MeleeAttacker.EdgeIntensity(1f, 1f, 0.75f, false), 0.8f);
        }

        [Test]
        public void Unparryable_WindupPoseIsLarger()
        {
            var rest = new Vector3(15f, 0f, 0f); var wind = new Vector3(15f, -130f, 70f);
            float normal = (MeleeAttacker.WindupPose(rest, wind, false) - rest).magnitude;
            float heavy = (MeleeAttacker.WindupPose(rest, wind, true) - rest).magnitude;
            Assert.AreEqual(normal * 1.5f, heavy, 1e-3f);
        }

        [Test]
        public void WindupPitch_ShortBladesHigherThanPolearms_AndBounded()
        {
            Assert.Greater(MeleeAttacker.WindupPitch(0.34f), MeleeAttacker.WindupPitch(0.875f));
            Assert.Greater(MeleeAttacker.WindupPitch(0.875f), MeleeAttacker.WindupPitch(1.47f));
            Assert.That(MeleeAttacker.WindupPitch(10f), Is.InRange(0.7f, 1.4f));
        }

        [Test]
        public void EnemyPrefabs_HaveNoLights()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Ronin7/Prefabs/Roguelike/Enemies" }))
                Assert.IsEmpty(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponentsInChildren<Light>(true), AssetDatabase.GUIDToAssetPath(g));
        }
    }
}
