using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P4-08 weapon loadouts: RunDirector.ApplyLoadout, the Twin Wakizashi Off-hand block, the
    /// MetaProgression weapon locks and the appended MetaSaveData fields.</summary>
    public class LoadoutTests
    {
        private GameObject rig;
        private WeaponDefinition katana, nodachi, twin;

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
            katana = Def(WeaponId.Katana);
            nodachi = Def(WeaponId.Nodachi);
            twin = Def(WeaponId.TwinWakizashi);
            rig = new GameObject("Rig");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rig);
            foreach (var d in new[] { katana, nodachi, twin }) Object.DestroyImmediate(d);
            MetaProgression.ResetForTests();
            RunState.Reset();
        }

        private static WeaponDefinition Def(string id)
        {
            var d = ScriptableObject.CreateInstance<WeaponDefinition>();
            d.id = id;
            return d;
        }

        private WeaponDefinition[] All => new[] { katana, nodachi, twin };

        /// <summary>Sword + Blade child (BladeDamager) on a KatanaHolster hosted by <paramref name="holsterHost"/>.</summary>
        private static BladeDamager MakeHolsteredSword(GameObject holsterHost, out Sword sword)
        {
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(holsterHost.transform, false);
            sword = swordGo.AddComponent<Sword>(); // RequireComponent adds the Grabbable
            var blade = new GameObject("Blade", typeof(BoxCollider));
            blade.transform.SetParent(swordGo.transform, false);
            var damager = blade.AddComponent<BladeDamager>();

            var holster = holsterHost.AddComponent<KatanaHolster>();
            var so = new SerializedObject(holster);
            so.FindProperty("sword").objectReferenceValue = swordGo.GetComponent<Grabbable>();
            so.FindProperty("hipAnchor").objectReferenceValue = holsterHost.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return damager;
        }

        private BladeDamager MakeOffhand(out Sword sword)
        {
            var anchor = new GameObject(KatanaHolster.OffhandAnchorName);
            anchor.transform.SetParent(rig.transform, false);
            var d = MakeHolsteredSword(anchor, out sword);
            sword.gameObject.SetActive(false); // as built: inactive until the Twin is equipped
            return d;
        }

        [Test]
        public void Loadout_AppliesDefinitionAfterLoad()
        {
            var damager = MakeHolsteredSword(rig, out var sword);
            damager.SetDefinition(katana); // stands in for Awake caching the scene's katana at load

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi);

            Assert.AreSame(twin, damager.Definition);
            Assert.AreSame(twin, sword.Definition);
        }

        [Test]
        public void Loadout_OffRosterId_PlaysAsKatana()
        {
            var damager = MakeHolsteredSword(rig, out _);
            damager.SetDefinition(twin);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.Nodachi); // A15.1: Nodachi/Tanto left the roster

            Assert.AreSame(katana, damager.Definition);
        }

        [Test]
        public void Loadout_UnknownId_KeepsSceneKatana()
        {
            var damager = MakeHolsteredSword(rig, out _);
            damager.SetDefinition(katana);

            RunDirector.ApplyLoadout(rig.transform, All, "no_such_weapon");

            Assert.AreSame(katana, damager.Definition);
        }

        [Test]
        public void Twin_ArmsLeftBlade_AndLeavesOffhandArtToTheHand()
        {
            var main = MakeHolsteredSword(rig, out _);
            var left = MakeOffhand(out var leftSword);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi);

            RunState.Begin(1u);
            Assert.IsTrue(ArtSlots.CanFire(ArtSlot.Offhand, mainHolding: true, offHolding: false)); // A15.3: only an occupied hand blocks it
            Assert.IsFalse(ArtSlots.CanFire(ArtSlot.Offhand, mainHolding: true, offHolding: true));
            Assert.IsTrue(leftSword.gameObject.activeSelf);
            Assert.AreSame(twin, main.Definition);
            Assert.AreSame(twin, left.Definition);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.Katana);

            Assert.IsFalse(leftSword.gameObject.activeSelf);
            Assert.AreSame(katana, main.Definition);
        }

        private PolearmShieldGear MakeGear(out BladeDamager polearmBlade, out BladeDamager shieldBlade, WeaponDefinition shieldDef)
        {
            var gear = rig.AddComponent<PolearmShieldGear>();
            gear.polearmAnchor = new GameObject("PolearmAnchor").transform;
            gear.polearmAnchor.SetParent(rig.transform, false);
            polearmBlade = MakeHolsteredSword(gear.polearmAnchor.gameObject, out var pole); // reuse: Sword + Blade under the anchor
            Object.DestroyImmediate(gear.polearmAnchor.GetComponent<KatanaHolster>()); // the anchor is not a holster host
            gear.polearm = pole.GetComponent<Grabbable>();
            gear.shieldAnchor = new GameObject("ShieldAnchor").transform;
            gear.shieldAnchor.SetParent(rig.transform, false);
            shieldBlade = MakeHolsteredSword(gear.shieldAnchor.gameObject, out var sh);
            Object.DestroyImmediate(gear.shieldAnchor.GetComponent<KatanaHolster>());
            gear.shield = sh.GetComponent<Grabbable>();
            Object.DestroyImmediate(sh); // the shield has no Sword component, only its own definition
            shieldBlade.SetDefinition(shieldDef);
            gear.polearm.gameObject.SetActive(false);
            gear.shield.gameObject.SetActive(false);
            return gear;
        }

        [Test]
        public void PolearmShield_PointsHolstersAtTheGear_AndSwitchingBackRestoresKatana()
        {
            var polearmDef = Def(WeaponId.PolearmShield);
            var shieldDef = Def("player_shield");
            shieldDef.maxDamage = 0f; shieldDef.minDamage = 0f;
            var mainBlade = MakeHolsteredSword(rig, out var katanaSword);
            mainBlade.SetDefinition(katana);
            var left = MakeOffhand(out var leftSword);
            var gear = MakeGear(out var polearmBlade, out var shieldBlade, shieldDef);
            var main = rig.GetComponent<KatanaHolster>();
            var off = rig.transform.Find(KatanaHolster.OffhandAnchorName).GetComponent<KatanaHolster>();
            var all = new[] { katana, nodachi, twin, polearmDef };
            try
            {
                RunDirector.ApplyLoadout(rig.transform, all, WeaponId.PolearmShield);

                Assert.AreSame(gear.polearm, main.Sword);
                Assert.AreSame(gear.shield, off.Sword);
                Assert.IsTrue(gear.polearm.gameObject.activeSelf); Assert.IsTrue(gear.shield.gameObject.activeSelf);
                Assert.IsFalse(katanaSword.gameObject.activeSelf); Assert.IsFalse(leftSword.gameObject.activeSelf);
                Assert.AreSame(polearmDef, polearmBlade.Definition);
                Assert.AreSame(shieldDef, shieldBlade.Definition, "EquipBlade must not overwrite the shield with the Polearm");
                Assert.IsTrue(shieldBlade.IsShield);
                Assert.IsFalse(polearmBlade.IsShield);

                RunDirector.ApplyLoadout(rig.transform, all, WeaponId.Katana);

                Assert.AreSame(katanaSword.GetComponent<Grabbable>(), main.Sword);
                Assert.AreSame(leftSword.GetComponent<Grabbable>(), off.Sword);
                Assert.IsTrue(katanaSword.gameObject.activeSelf);
                Assert.IsFalse(leftSword.gameObject.activeSelf); Assert.IsFalse(gear.polearm.gameObject.activeSelf); Assert.IsFalse(gear.shield.gameObject.activeSelf);
                Assert.AreSame(katana, mainBlade.Definition);
            }
            finally { Object.DestroyImmediate(polearmDef); Object.DestroyImmediate(shieldDef); }
        }

        [Test]
        public void PolearmShield_OffRosterSaveStillPlaysAsKatana()
        {
            var polearmDef = Def(WeaponId.PolearmShield);
            var mainBlade = MakeHolsteredSword(rig, out var katanaSword);
            mainBlade.SetDefinition(twin);
            var shieldDef = Def("player_shield");
            var gear = MakeGear(out _, out _, shieldDef);
            try
            {
                RunDirector.ApplyLoadout(rig.transform, new[] { katana, polearmDef }, WeaponId.Tanto);

                Assert.AreSame(katana, mainBlade.Definition);
                Assert.IsTrue(katanaSword.gameObject.activeSelf);
                Assert.IsFalse(gear.polearm.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(polearmDef); Object.DestroyImmediate(shieldDef); }
        }

        [Test]
        public void LockedWeapon_CannotBeSelected()
        {
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Katana));
            foreach (var id in new[] { WeaponId.Nodachi, WeaponId.Tanto, WeaponId.TwinWakizashi })
            {
                Assert.IsFalse(MetaProgression.IsWeaponUnlocked(id), id);
                Assert.IsFalse(MetaProgression.TrySelectLoadout(id), id);
            }
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);

            MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.TwinWakizashi));
            RunState.Begin(1u);
            Assert.AreEqual(WeaponId.TwinWakizashi, RunState.LoadoutId);

            // A15.1: an owned Nodachi stays truthful in the shop data but reads as the Katana for the next run.
            MetaProgression.UnlockWeapon(WeaponId.Nodachi);
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.Nodachi));
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Nodachi));
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);
        }

        [Test]
        public void DevUnlock_UnlocksAllWeapons_InMemoryOnly()
        {
            MetaProgression.DevUnlockAllWeapons = true;
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.TwinWakizashi));

            var data = new MetaSaveData();
            MetaProgression.WriteTo(data);
            Assert.IsEmpty(data.unlockedWeapons);

            MetaProgression.DevUnlockAllWeapons = false;
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout, "a pick that is no longer unlocked reads as the katana");
        }

        [Test]
        public void MetaSaveV1_WithoutLoadoutFields_LoadsDefaults()
        {
            MetaProgression.UnlockWeapon(WeaponId.Nodachi);
            MetaProgression.TrySelectLoadout(WeaponId.Nodachi);

            var data = JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaEchoes\":5}");
            MetaProgression.ApplyFrom(data);

            Assert.AreEqual(1, data.version);
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.Nodachi));
        }

        [Test]
        public void MetaSave_LoadoutFields_RoundTrip()
        {
            MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
            MetaProgression.TrySelectLoadout(WeaponId.TwinWakizashi);
            var data = new MetaSaveData();
            MetaProgression.WriteTo(data);
            string json = JsonUtility.ToJson(data);

            MetaProgression.ResetForTests();
            MetaProgression.ApplyFrom(JsonUtility.FromJson<MetaSaveData>(json));

            Assert.AreEqual(1, MetaSaveData.CurrentVersion);
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi));
            Assert.AreEqual(WeaponId.TwinWakizashi, MetaProgression.SelectedLoadout);
        }

        [Test]
        public void LoadoutAssets_MatchRoadmapAndTwinDecision()
        {
            var k = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/Katana.asset");
            var n = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/Nodachi.asset");
            var t = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/Tanto.asset");
            var w = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/TwinWakizashi.asset");
            Assert.IsNotNull(k); Assert.IsNotNull(n); Assert.IsNotNull(t); Assert.IsNotNull(w);

            Assert.AreEqual(WeaponId.Katana, k.id);
            Assert.AreEqual(WeaponId.Nodachi, n.id);
            Assert.AreEqual(WeaponId.Tanto, t.id);
            Assert.AreEqual(WeaponId.TwinWakizashi, w.id);

            AssertStats(n, 2f, 7f, 14f, 60f, 0.35f);
            AssertStats(t, 1f, 4.5f, 6f, 28f, 0.18f);
            // Twin: 0.6x katana damage per blade, 1.25x swing speed (thresholds and cooldown / 1.25).
            AssertStats(w, k.minSwingSpeed / 1.25f, k.referenceSwingSpeed / 1.25f,
                k.minDamage * 0.6f, k.maxDamage * 0.6f, k.hitCooldown / 1.25f);
        }

        [Test]
        public void DoubleKatana_MatchesA155()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/TwinWakizashi.asset");
            Assert.AreEqual("twin_wakizashi", w.id);
            AssertStats(w, 1.2f, 4.8f, 4.8f, 24f, 0.2f);
            Assert.AreEqual(0.837f, w.bladeLength, 1e-4f);
            Assert.AreEqual(0.7f, w.parryStagger, 1e-4f);
            Assert.IsTrue(w.canParry);
        }

        [Test]
        public void DoubleKatana_CrimsonBladeMaterial_IsRedNotTelegraphOrange()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/HeroVariants/CyberSword_Blade_Crimson.mat");
            Assert.IsNotNull(m, "A15.5 crimson material asset");
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/HeroVariants/CyberSword_Blade.mat");
            Assert.AreEqual(src.shader, m.shader, "same shader as the katana blade");
            // MeleeAttacker.TelegraphColor is (1, 0.4, 0.25): orange (g/r 0.4, green over blue). Crimson is red-dominant
            // with green near zero and blue over green.
            foreach (var c in new[] { m.GetColor("_BaseColor"), m.GetColor("_EmissionColor") })
            {
                Assert.Greater(c.r, 3f * c.b, "red-dominant");
                Assert.Less(c.g / c.r, 0.15f, "far below the telegraph green ratio 0.4");
                Assert.Greater(c.b, c.g, "crimson leans blue-ward of orange");
            }
        }

        private static void AssertStats(WeaponDefinition d, float minSpeed, float refSpeed, float minDmg, float maxDmg, float cooldown)
        {
            Assert.AreEqual(minSpeed, d.minSwingSpeed, 1e-4f, d.name);
            Assert.AreEqual(refSpeed, d.referenceSwingSpeed, 1e-4f, d.name);
            Assert.AreEqual(minDmg, d.minDamage, 1e-4f, d.name);
            Assert.AreEqual(maxDmg, d.maxDamage, 1e-4f, d.name);
            Assert.AreEqual(cooldown, d.hitCooldown, 1e-4f, d.name);
        }

        private static readonly (string file, string prefab, float length)[] BladeSpec =
        {
            ("Nodachi", "Sword_Nodachi", 1.14f),
            ("Tanto", "Sword_Tanto", 0.34f),
            // A15.5: Double Katana uses the scene's Katana mesh at 0.837 m (not Sword_Wakizashi), so no variant prefab to match.
        };

        [Test]
        public void LoadoutDefinitions_BladeLength_MatchesA135_AndVariantPrefab()
        {
            foreach (var (file, prefab, length) in BladeSpec)
            {
                var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"Assets/Ronin7/Data/{file}.asset");
                Assert.AreEqual(length, def.bladeLength, 0.05f, file);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Ronin7/Prefabs/Art/{prefab}.prefab");
                Assert.IsNotNull(go, prefab);
                var blade = go.GetComponentInChildren<BladeDamager>(true);
                Assert.IsNotNull(blade, prefab);
                Assert.AreEqual(def.bladeLength, blade.WorldLength, 0.05f, prefab);
                Assert.IsNotNull(go.GetComponentInChildren<BladeTrailDriver>(true), prefab + " trail driver");
            }
        }

        private BladeDamager SizedBlade(GameObject host, out Sword sword)
        {
            var d = MakeHolsteredSword(host, out sword);
            var t = d.transform;
            t.parent.localScale = new Vector3(1f, 1f, 1.35f); // the arena's katana upscale
            var box = t.GetComponent<BoxCollider>();
            box.size = Vector3.one;
            t.localScale = new Vector3(0.04f, 0.012f, 0.62f);
            t.localPosition = new Vector3(0f, 0f, 0.45f);
            return d;
        }

        // Awake (which caches the box for WorldEndpoints) does not run in edit mode, so read the box directly.
        private static Vector3 BaseWorld(BladeDamager d)
        {
            var box = d.GetComponent<BoxCollider>();
            return d.transform.TransformPoint(box.center - new Vector3(0f, 0f, box.size.z * 0.5f));
        }

        [Test]
        public void Loadout_SweepLengthEqualsBladeLength_BaseFixed()
        {
            twin.bladeLength = 1.14f;
            var damager = SizedBlade(rig, out _);
            damager.SetDefinition(katana);
            var baseBefore = BaseWorld(damager);
            Assert.AreEqual(0.62f * 1.35f, damager.WorldLength, 1e-4f);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi);

            var baseAfter = BaseWorld(damager);
            Assert.AreEqual(1.14f, damager.WorldLength, 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(baseBefore, baseAfter), 1e-4f);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi); // idempotent
            Assert.AreEqual(1.14f, damager.WorldLength, 1e-4f);
        }

        [Test]
        public void Loadout_KatanaWithoutBladeLength_LeavesBladeAsBuilt()
        {
            var damager = SizedBlade(rig, out _);
            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.Katana);
            Assert.AreEqual(0.62f * 1.35f, damager.WorldLength, 1e-4f);
        }

        [Test]
        public void Loadout_KatanaDefinition_ResetsLengthToAsBuilt()
        {
            var k = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Ronin7/Data/Katana.asset");
            Assert.AreEqual(0.837f, k.bladeLength, 0.005f);
            twin.bladeLength = 1.14f;
            katana.bladeLength = k.bladeLength;
            var damager = SizedBlade(rig, out _);
            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi);
            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.Katana);
            Assert.AreEqual(0.837f, damager.WorldLength, 0.005f);
        }

        [Test]
        public void Twin_BothBladesGetWakizashiLength()
        {
            twin.bladeLength = 0.54f;
            var main = SizedBlade(rig, out _);
            var anchor = new GameObject(KatanaHolster.OffhandAnchorName);
            anchor.transform.SetParent(rig.transform, false);
            var left = SizedBlade(anchor, out var leftSword);
            leftSword.gameObject.SetActive(false);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi);

            Assert.AreEqual(0.54f, main.WorldLength, 1e-4f);
            Assert.AreEqual(0.54f, left.WorldLength, 1e-4f);
        }

        [Test]
        public void LoadoutDamage_ComposesWithEdgeSensitivity()
        {
            nodachi.minSwingSpeed = 2f; nodachi.referenceSwingSpeed = 7f; nodachi.minDamage = 14f; nodachi.maxDamage = 60f;
            float scale = WeaponDefinition.EdgeThresholdScale(0.3f); // x0.70: max at 4.9 m/s
            Assert.AreEqual(0f, nodachi.DamageForSpeed(1.9f, 1f));
            Assert.AreEqual(14f, nodachi.DamageForSpeed(1.4f, scale), 1e-3f);
            Assert.AreEqual(60f, nodachi.DamageForSpeed(4.9f, scale), 1e-3f);
        }
    }
}
