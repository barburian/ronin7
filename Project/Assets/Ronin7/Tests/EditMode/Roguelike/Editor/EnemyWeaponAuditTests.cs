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
                if (armL == null || armL.Find(EnemyWeaponDefinition.ShieldSocket) == null) bad.Add(e.prefab.name + ": no Socket_Shield under Rig_ArmL");
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
                "Spectral_Grave-Guardian", "Dream_Ghost_Manifestation", "Vane_Wraith-6",
                Ronin7.EditorTools.EnemyPrefabBaker.LeaperBody }, natural); // P8-06 Leaper: natural claws
        }

        private static EnemySpawnTable LoadTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            Assert.AreEqual(10, table.kits?.Length ?? 0, "spawn table kit list - run Compose Enemy Weapons (additive).");
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
                        bool forearm = kit.off != null && kit.off.forearm;
                        if (off.childCount != (kit.off != null && !forearm ? 1 : 0)) bad.Add(tag + ": off socket children " + off.childCount);
                        var strap = FindDeep(go.transform, EnemyWeaponDefinition.ShieldSocket);
                        if (forearm && (strap == null || strap.childCount != 1)) bad.Add(tag + ": shield not on Socket_Shield");
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

        // P8-02c: in the lowered rest the shield stands upright on the outer left forearm, sized to that forearm.
        [Test]
        public void Shield_StandsUprightOnForearm()
        {
            var table = LoadTable();
            var bad = new System.Collections.Generic.List<string>();
            int seen = 0;
            foreach (var e in table.trash)
            {
                if (e.definition.naturalWeapon) continue;
                foreach (var kit in table.kits)
                {
                    if (kit.off == null || !kit.off.forearm) continue;
                    var go = Object.Instantiate(e.prefab);
                    try
                    {
                        go.GetComponent<Enemy>().Equip(kit.main, kit.off);
                        var armL = FindDeep(go.transform, NpcWalkAnimator.ArmLBoneName);
                        float down = new SerializedObject(go.GetComponent<NpcWalkAnimator>()).FindProperty("armsDownDegrees").floatValue;
                        armL.localRotation = Quaternion.AngleAxis(down, Vector3.forward) * armL.localRotation; // NpcWalkAnimator.Awake rest
                        var forearm = ForearmVerts(go, armL);
                        Transform s = null;
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name == "Shield") s = r.transform;
                        string tag = e.prefab.name + "+" + kit.main.name + "+" + kit.off.name;
                        if (s == null) { bad.Add(tag + ": no Shield renderer"); continue; }
                        if (forearm.Count == 0) { bad.Add(tag + ": no Rig_ArmL skin"); continue; }
                        seen++;
                        if (Mathf.Abs(s.forward.y) >= 0.2f || s.up.y <= 0.9f) bad.Add($"{tag}: not upright fwd={s.forward} up={s.up}");
                        Vector3 near = forearm[0], c = Vector3.zero;
                        float minY = float.MaxValue, maxY = float.MinValue;
                        foreach (var v in forearm)
                        {
                            if ((v - s.position).sqrMagnitude < (near - s.position).sqrMagnitude) near = v;
                            c += v; minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                        }
                        c /= forearm.Count;
                        float dist = Vector3.Distance(s.position, near), len = maxY - minY;
                        if (dist > 0.15f) bad.Add($"{tag}: {dist:F2} m off the forearm skin");
                        if (s.position.x >= c.x) bad.Add(tag + ": not on the outer (left) side");
                        if (s.lossyScale.y > 2f * len) bad.Add($"{tag}: {s.lossyScale.y:F2} m tall vs {len:F2} m forearm");
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            Assert.Greater(seen, 0, "no shield kit in the roll list");
            Assert.IsEmpty(bad, string.Join(System.Environment.NewLine, bad));
        }

        /// <summary>World positions of the posed skin verts bound to <paramref name="armL"/>, lower half (forearm + hand).</summary>
        private static System.Collections.Generic.List<Vector3> ForearmVerts(GameObject go, Transform armL)
        {
            var pts = new System.Collections.Generic.List<Vector3>();
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int bi = smr != null ? System.Array.IndexOf(smr.bones, armL) : -1;
            if (bi < 0) return pts;
            var baked = new Mesh();
            smr.BakeMesh(baked, false); // already metric for these rigs; useScale=true double-scales
            var verts = baked.vertices;
            var w = smr.sharedMesh.boneWeights;
            Object.DestroyImmediate(baked);
            for (int i = 0; i < verts.Length; i++)
                if ((w[i].boneIndex0 == bi && w[i].weight0 > 0.6f) || (w[i].boneIndex1 == bi && w[i].weight1 > 0.6f))
                    pts.Add(smr.transform.position + smr.transform.rotation * verts[i]);
            if (pts.Count == 0) return pts;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            pts.RemoveAll(p => p.y > (minY + maxY) * 0.5f);
            return pts;
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
            var seen = new System.Collections.Generic.HashSet<(EnemyWeaponDefinition, EnemyWeaponDefinition)>();
            var a = Ronin7.Core.RunRng.ForNode(2026u ^ 0x4B17u, 3);
            var b = Ronin7.Core.RunRng.ForNode(2026u ^ 0x4B17u, 3);
            for (int i = 0; i < 400; i++)
            {
                var ka = table.RollKit(armed, ref a);
                var kb = table.RollKit(armed, ref b);
                Assert.AreSame(ka.main, kb.main); Assert.AreSame(ka.off, kb.off);
                seen.Add((ka.main, ka.off));
            }
            Assert.AreEqual(table.kits.Length, seen.Count, "every (main, off) kit should be rolled over 400 spawns");
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

        [Test]
        public void RangedCap_NeverExceedsFivePerRoom()
        {
            var table = LoadTable();
            var armed = System.Array.Find(table.trash, e => !e.definition.naturalWeapon && !e.fixedKit);
            Assert.AreEqual(5, EnemySpawnTable.MaxRangedPerRoom);
            for (uint seed = 1; seed <= 100; seed++)
            {
                var rng = Ronin7.Core.RunRng.ForNode(seed ^ 0x4B17u, 3);
                int ranged = 0;
                for (int i = 0; i < 12; i++)
                {
                    var k = table.RollKit(armed, ref rng, ranged);
                    if (k.main.ranged) ranged++;
                }
                Assert.LessOrEqual(ranged, EnemySpawnTable.MaxRangedPerRoom, "seed " + seed);
            }
        }

        [Test]
        public void RangedCap_UnderCap_MatchesUncappedRoll()
        {
            var table = LoadTable();
            var armed = System.Array.Find(table.trash, e => !e.definition.naturalWeapon && !e.fixedKit);
            var a = Ronin7.Core.RunRng.ForNode(2026u ^ 0x4B17u, 3);
            var b = Ronin7.Core.RunRng.ForNode(2026u ^ 0x4B17u, 3);
            for (int i = 0; i < 50; i++)
                Assert.AreSame(table.RollKit(armed, ref a).main, table.RollKit(armed, ref b, EnemySpawnTable.MaxRangedPerRoom - 1).main);
        }

        [Test]
        public void ExemptBodies_KeepSculptedKit()
        {
            var table = LoadTable();
            var rng = Ronin7.Core.RunRng.ForNode(2026u, 1);
            int found = 0;
            foreach (var e in table.trash)
            {
                string want = e.prefab.name == "Humanoid_Automaton" || e.prefab.name == Ronin7.EditorTools.EnemyPrefabBaker.JuggernautBody ? "EW_HeavyAxe" // P8-06 Juggernaut
                    : e.prefab.name == "Iron_Dojo_Warden-Cadre" ? "EW_Staff"
                    : e.prefab.name == Ronin7.EditorTools.EnemyPrefabBaker.BannerBody ? "EW_Banner" : null; // P8-06 Banner Commander
                Assert.AreEqual(want != null, e.fixedKit, e.prefab.name);
                if (want == null) continue;
                found++;
                for (int i = 0; i < 20; i++) Assert.AreEqual(want, table.RollKit(e, ref rng, 99).main.name, e.prefab.name);
            }
            Assert.AreEqual(4, found); // Automaton, Warden-Cadre, P8-06 Juggernaut, Banner Commander
        }
    }
}
