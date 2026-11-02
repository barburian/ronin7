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
    }
}
