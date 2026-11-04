using NUnit.Framework;
using Ronin7.Enemies;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P8-02 (D-17): weapons are composed onto body sockets at spawn, never baked.</summary>
    public class EnemyWeaponComposeTests
    {
        private GameObject body;

        [TearDown] public void TearDown() { if (body != null) Object.DestroyImmediate(body); }

        private Enemy MakeBody(out Transform mainSocket, out Transform offSocket)
        {
            body = new GameObject("body");
            body.SetActive(false); // spawn order: equip before Awake
            var armR = new GameObject("Rig_ArmR").transform; armR.SetParent(body.transform, false);
            var armL = new GameObject("Rig_ArmL").transform; armL.SetParent(body.transform, false);
            mainSocket = new GameObject(EnemyWeaponDefinition.MainSocket).transform; mainSocket.SetParent(armR, false);
            offSocket = new GameObject(EnemyWeaponDefinition.OffSocket).transform; offSocket.SetParent(armL, false);
            var enemy = body.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            so.FindProperty("weapon").objectReferenceValue = armR;
            so.ApplyModifiedPropertiesWithoutUndo();
            return enemy;
        }

        private static EnemyWeaponDefinition Def(GameObject prefab, Vector3 tip)
        {
            var d = ScriptableObject.CreateInstance<EnemyWeaponDefinition>();
            d.prefab = prefab; d.tip = tip;
            return d;
        }

        [Test]
        public void Compose_AttachesToSocket()
        {
            var enemy = MakeBody(out var main, out var off);
            var swordPrefab = new GameObject("TestSword");
            var shieldPrefab = new GameObject("TestShield");
            var shield = GameObject.CreatePrimitive(PrimitiveType.Cube); shield.name = "Shield"; shield.transform.SetParent(shieldPrefab.transform, false);
            try
            {
                enemy.Equip(Def(swordPrefab, new Vector3(0f, 0f, 0.7f)), Def(shieldPrefab, Vector3.zero));
                Assert.AreEqual(1, main.childCount, "main-hand weapon not under Socket_MainHand");
                Assert.AreEqual(1, off.childCount, "off-hand weapon not under Socket_OffHand");
                var tip = new SerializedObject(enemy).FindProperty("bladeTip").objectReferenceValue as Transform;
                Assert.IsNotNull(tip);
                Assert.AreEqual(main.GetChild(0), tip.parent, "bladeTip must be the attached weapon's tip");
                Assert.AreEqual(new Vector3(0f, 0f, 0.7f), tip.localPosition);
                var mark = new SerializedObject(enemy).FindProperty("archetypeMark").objectReferenceValue as Renderer;
                Assert.IsNotNull(mark, "the shield must become the guard mark");
                Assert.AreEqual(off, mark.transform.parent.parent);
            }
            finally { Object.DestroyImmediate(swordPrefab); Object.DestroyImmediate(shieldPrefab); }
        }

        // P8-02c: a forearm off-hand (the shield) straps to Socket_Shield, not the hand, and the left arm holds the guard.
        [Test]
        public void Shield_UsesForearmSocket()
        {
            var enemy = MakeBody(out _, out var off);
            var forearm = new GameObject(EnemyWeaponDefinition.ShieldSocket).transform; forearm.SetParent(off.parent, false);
            var shieldPrefab = new GameObject("TestShield");
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube); plate.name = "Shield"; plate.transform.SetParent(shieldPrefab.transform, false);
            try
            {
                var shield = Def(shieldPrefab, Vector3.zero);
                shield.forearm = true;
                enemy.Equip(Def(null, Vector3.forward), shield);
                Assert.AreEqual(1, forearm.childCount, "forearm off-hand not under Socket_Shield");
                Assert.AreEqual(0, off.childCount, "forearm off-hand must not sit at the hand");
                Assert.IsTrue(enemy.HasShield, "a shield kit must stop the left arm copying the swing");
            }
            finally { Object.DestroyImmediate(shieldPrefab); }
        }

        [Test]
        public void Compose_PatternsComeFromWeapon()
        {
            var enemy = MakeBody(out _, out _);
            Assert.AreEqual(1f, enemy.NextStrike().windup, "no weapon: today's chop");
            var def = Def(null, Vector3.forward);
            def.patterns[0].strikes[0] = new Strike { direction = StrikeDirection.Overhead, windup = 1.5f, active = 0.5f, recovery = 2f };
            enemy.Equip(def);
            Assert.AreSame(def, enemy.WeaponKit);
            Strike s = enemy.NextStrike();
            Assert.AreEqual(1.5f, s.windup); Assert.AreEqual(0.5f, s.active); Assert.AreEqual(2f, s.recovery);
        }

        [Test]
        public void Natural_HasNoMesh()
        {
            var claws = AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>("Assets/Ronin7/Data/Roguelike/Weapons/EW_Claws.asset");
            Assert.IsNotNull(claws, "EW_Claws missing - run Tools/Space Samurai/Roguelike/Build Enemy Weapons (additive).");
            Assert.IsNull(claws.prefab);
            var enemy = MakeBody(out var main, out _);
            enemy.Equip(claws);
            Assert.AreEqual(0, body.GetComponentsInChildren<Renderer>(true).Length, "claws must add no renderer");
            Assert.IsNotNull(main.GetChild(0).Find(EnemyWeaponDefinition.TipName));
        }

        [Test]
        public void EveryWeapon_SharesPaletteMaterials_NoLights()
        {
            var mats = new System.Collections.Generic.HashSet<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyWeaponDefinition", new[] { "Assets/Ronin7/Data/Roguelike/Weapons" }))
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotEmpty(d.patterns, d.name + ": every weapon ships a moveset (P8-03)");
                if (d.prefab == null) continue;
                Assert.IsEmpty(d.prefab.GetComponentsInChildren<Light>(true), d.name);
                Assert.IsEmpty(d.prefab.GetComponentsInChildren<Collider>(true), d.name + ": visual-only");
                foreach (var r in d.prefab.GetComponentsInChildren<Renderer>(true)) mats.Add(r.sharedMaterial);
            }
            // The stand-ins share the 3-material palette; each Tripo mesh (P8 real weapons) brings its own (6 max, the shield's is swapped for the body's at Equip).
            int palette = 0;
            foreach (var m in mats)
            {
                string path = AssetDatabase.GetAssetPath(m);
                if (path.StartsWith("Assets/Ronin7/Art/Materials/EnemyWeapons/")) palette++;
                else StringAssert.StartsWith("Assets/Ronin7/Art/Generated/AI/Weapons/", path);
            }
            Assert.LessOrEqual(palette, 3, "stand-ins share the 3-material palette");
            Assert.LessOrEqual(mats.Count - palette, 6, "at most one material per Tripo weapon");
        }

        // The six Tripo meshes are the kit visuals (nested generated prefabs), not the primitive stand-ins.
        [TestCase("HeavyAxe")] [TestCase("Shield")] [TestCase("Staff")] [TestCase("Bow")] [TestCase("Crossbow")] [TestCase("Blaster")]
        public void Weapon_UsesItsGeneratedMesh(string weapon)
        {
            var d = AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>("Assets/Ronin7/Data/Roguelike/Weapons/EW_" + weapon + ".asset");
            Assert.IsNotNull(d.prefab, weapon);
            string want = "Assets/Ronin7/Art/Generated/AI/Weapons/" + weapon + ".prefab";
            bool found = false;
            foreach (var r in d.prefab.GetComponentsInChildren<Renderer>(true))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject);
                if (src != null && AssetDatabase.GetAssetPath(src) == want) found = true;
            }
            Assert.IsTrue(found, "EW_" + weapon + " does not use " + want + " (run Tools/Space Samurai/Roguelike/Apply Tripo Weapon Meshes)");
        }

        // P8-04 fix: the telegraph line must hug the weapon (the heavy axe's edge once floated 20 cm off its head).
        [Test]
        public void EveryWeapon_EdgeLiesOnItsMesh()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyWeaponDefinition", new[] { "Assets/Ronin7/Data/Roguelike/Weapons" }))
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyWeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d.prefab == null) continue;
                var go = Object.Instantiate(d.prefab);
                try
                {
                    var rs = go.GetComponentsInChildren<Renderer>();
                    bool On(Vector3 p) { foreach (var r in rs) if (r.bounds.SqrDistance(p) < 0.03f * 0.03f) return true; return false; }
                    bool flat = (d.edgeBase - d.tip).sqrMagnitude < 1e-4f; // ranged: the edge runs from the weapon root
                    Assert.IsTrue(On(d.tip), d.name + " tip " + d.tip + " is off its mesh");
                    Assert.IsTrue(On(flat ? Vector3.zero : d.edgeBase), d.name + " edgeBase " + d.edgeBase + " is off its mesh");
                    if (!flat) Assert.IsTrue(On((d.tip + d.edgeBase) * 0.5f), d.name + " edge midpoint is off its mesh");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
