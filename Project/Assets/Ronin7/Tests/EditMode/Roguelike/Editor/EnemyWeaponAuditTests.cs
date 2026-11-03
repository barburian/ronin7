using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P8-01 (BUG-54) + P8-02 (D-17): every spawnable trash body has empty hand sockets and no baked Sword; its
    /// spawn entry composes a weapon (a visible Blade for armed bodies, meshless claws for natural ones). Bosses keep
    /// their baked kit (blade tip wired, visible blade unless natural).</summary>
    public class EnemyWeaponAuditTests
    {
        private const string SpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";

        [Test]
        public void EveryTrashBody_HasSockets_AndComposesAWeapon()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            var bad = new System.Collections.Generic.List<string>();
            foreach (var e in table.trash)
            {
                Transform armR = null, armL = null;
                foreach (var t in e.prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == NpcWalkAnimator.ArmRBoneName) armR = t;
                    if (t.name == NpcWalkAnimator.ArmLBoneName) armL = t;
                }
                if (armR == null || armR.Find(EnemyWeaponDefinition.MainSocket) == null) bad.Add(e.prefab.name + ": no Socket_MainHand under Rig_ArmR");
                if (armL == null || armL.Find(EnemyWeaponDefinition.OffSocket) == null) bad.Add(e.prefab.name + ": no Socket_OffHand under Rig_ArmL");
                if (armR != null && armR.Find("Sword") != null) bad.Add(e.prefab.name + ": baked Rig_ArmR/Sword (D-17)");
                if (e.mainHand == null) { bad.Add(e.prefab.name + ": no mainHand weapon"); continue; }
                bool visible = e.mainHand.prefab != null && EnemySwordVisual.FindBladeRenderer(e.mainHand.prefab.transform) != null;
                if (e.definition.naturalWeapon == visible)
                    bad.Add($"{e.prefab.name}: natural={e.definition.naturalWeapon} but weapon {e.mainHand.name} visibleBlade={visible}");
            }
            foreach (var b in table.bosses)
            {
                var so = new SerializedObject(b.prefab.GetComponent<Enemy>());
                var weapon = so.FindProperty("weapon").objectReferenceValue as Transform;
                if (so.FindProperty("bladeTip").objectReferenceValue == null) bad.Add(b.prefab.name + ": boss bladeTip");
                if (!b.definition.naturalWeapon && EnemySwordVisual.FindBladeRenderer(weapon) == null) bad.Add(b.prefab.name + ": boss blade");
                foreach (var r in weapon.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterial == null) bad.Add($"{b.prefab.name}: {r.name} has no material (magenta)");
            }
            Assert.IsEmpty(bad, "Weapon kit problems:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, bad));
        }

        [Test]
        public void NaturalFlag_OnBeastsWraithsAndDrones_Only()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            var natural = new System.Collections.Generic.List<string>();
            foreach (var e in table.trash) if (e.definition.naturalWeapon) natural.Add(e.prefab.name);
            foreach (var b in table.bosses) if (b.definition.naturalWeapon) natural.Add(b.prefab.name);
            CollectionAssert.AreEquivalent(new[] { "Hunter_Drone", "Dominion_Scan-Drone", "Redaction_Construct",
                "Spectral_Grave-Guardian", "Dream_Ghost_Manifestation", "Vane_Wraith-6" }, natural);
        }

        private static EnemySpawnTable LoadTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            Assert.AreEqual(6, table.kits?.Length ?? 0, "spawn table kit list - run Compose Enemy Weapons (additive).");
            return table;
        }

        // P8-02b: any armed body can carry any kit; the moveset is the equipped weapon's, not a body default.
        [Test]
        public void AnyArmedBody_CanEquipEveryKit()
        {
            var table = LoadTable();
            var bad = new System.Collections.Generic.List<string>();
            foreach (var e in table.trash)
            {
                if (e.definition.naturalWeapon) continue;
                foreach (var kit in table.kits)
                {
                    var go = Object.Instantiate(e.prefab); // baked inactive: equip before Awake, as the spawner does
                    try
                    {
                        var enemy = go.GetComponent<Enemy>();
                        enemy.Equip(kit.main, kit.off);
                        var so = new SerializedObject(enemy);
                        var main = FindDeep(go.transform, EnemyWeaponDefinition.MainSocket);
                        var off = FindDeep(go.transform, EnemyWeaponDefinition.OffSocket);
                        string tag = e.prefab.name + "+" + kit.main.name + (kit.off != null ? "+" + kit.off.name : "");
                        if (main.childCount != 1) bad.Add(tag + ": main socket children " + main.childCount);
                        if (off.childCount != (kit.off != null ? 1 : 0)) bad.Add(tag + ": off socket children " + off.childCount);
                        var tip = so.FindProperty("bladeTip").objectReferenceValue as Transform;
                        if (tip == null || tip.parent != main.GetChild(0)) bad.Add(tag + ": bladeTip not on the main-hand weapon");
                        if (!ReferenceEquals(enemy.WeaponKit, kit.main) || enemy.WeaponKit.patterns != kit.main.patterns) bad.Add(tag + ": patterns not the weapon's");
                        bool shield = kit.off != null && kit.off.name == "EW_Shield";
                        if ((so.FindProperty("archetypeMark").objectReferenceValue != null) != shield) bad.Add(tag + ": guard mark iff shield");
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            Assert.IsEmpty(bad, string.Join(System.Environment.NewLine, bad));
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        [Test]
        public void KitRoll_IsDeterministicForSeed_AndCoversEveryKit()
        {
            var table = LoadTable();
            var armed = System.Array.Find(table.trash, e => !e.definition.naturalWeapon);
            var seen = new System.Collections.Generic.HashSet<EnemyWeaponDefinition>();
            var a = Ronin7.Core.RunRng.ForNode(2026u ^ 0x4B17u, 3);
            var b = Ronin7.Core.RunRng.ForNode(2026u ^ 0x4B17u, 3);
            for (int i = 0; i < 200; i++)
            {
                var ka = table.RollKit(armed, ref a);
                var kb = table.RollKit(armed, ref b);
                Assert.AreSame(ka.main, kb.main); Assert.AreSame(ka.off, kb.off);
                seen.Add(ka.main);
            }
            Assert.AreEqual(6, seen.Count, "all six kits should be rolled over 200 spawns");
        }

        [Test]
        public void Natural_NeverRollsAHeldWeapon()
        {
            var table = LoadTable();
            var rng = Ronin7.Core.RunRng.ForNode(2026u, 1);
            foreach (var e in table.trash)
            {
                if (!e.definition.naturalWeapon) continue;
                for (int i = 0; i < 20; i++)
                {
                    var k = table.RollKit(e, ref rng);
                    Assert.AreSame(e.mainHand, k.main, e.prefab.name);
                    Assert.IsNull(k.main.prefab, e.prefab.name + " must keep meshless claws");
                    Assert.IsNull(k.off);
                }
            }
        }
    }
}
