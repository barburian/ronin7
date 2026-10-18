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
            ArtSlots.OffhandBlocked = false;
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
            ArtSlots.OffhandBlocked = false;
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

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.Nodachi);

            Assert.AreSame(nodachi, damager.Definition);
            Assert.AreSame(nodachi, sword.Definition);
            Assert.IsFalse(ArtSlots.OffhandBlocked);
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
        public void Twin_BlocksOffhandArt_AndArmsLeftBlade()
        {
            var main = MakeHolsteredSword(rig, out _);
            var left = MakeOffhand(out var leftSword);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.TwinWakizashi);

            Assert.IsTrue(ArtSlots.OffhandBlocked);
            Assert.IsFalse(ArtSlots.CanFire(ArtSlot.Offhand, rightHolding: true, leftHolding: false));
            Assert.IsTrue(ArtSlots.CanFire(ArtSlot.Weapon, rightHolding: true, leftHolding: false));
            Assert.IsTrue(leftSword.gameObject.activeSelf);
            Assert.AreSame(twin, main.Definition);
            Assert.AreSame(twin, left.Definition);

            RunDirector.ApplyLoadout(rig.transform, All, WeaponId.Katana);

            Assert.IsFalse(ArtSlots.OffhandBlocked);
            Assert.IsFalse(leftSword.gameObject.activeSelf);
            Assert.AreSame(katana, main.Definition);
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

            MetaProgression.UnlockWeapon(WeaponId.Tanto);
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.Tanto));
            RunState.Begin(1u);
            Assert.AreEqual(WeaponId.Tanto, RunState.LoadoutId);
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

        private static void AssertStats(WeaponDefinition d, float minSpeed, float refSpeed, float minDmg, float maxDmg, float cooldown)
        {
            Assert.AreEqual(minSpeed, d.minSwingSpeed, 1e-4f, d.name);
            Assert.AreEqual(refSpeed, d.referenceSwingSpeed, 1e-4f, d.name);
            Assert.AreEqual(minDmg, d.minDamage, 1e-4f, d.name);
            Assert.AreEqual(maxDmg, d.maxDamage, 1e-4f, d.name);
            Assert.AreEqual(cooldown, d.hitCooldown, 1e-4f, d.name);
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
