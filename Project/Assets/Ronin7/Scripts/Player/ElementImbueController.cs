using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>P4-04 S8 Element Imbue (pure): with the Off-hand trigger held, sweep the empty left hand along the
    /// blade from base to tip within <see cref="SweepSeconds"/> to imbue it for <see cref="Duration"/> s.</summary>
    public class ImbueLogic
    {
        public const float SweepSeconds = 0.5f, Duration = 8f, NearDistance = 0.15f, BaseEnd = 0.25f, TipStart = 0.85f;

        private float baseTouch = -1f;
        private float until = -1f;

        /// <summary>Where <paramref name="p"/> projects onto the blade (0 = base, 1 = tip) and its distance to it.</summary>
        public static float Along(Vector3 p, Vector3 bladeBase, Vector3 tip, out float distance)
        {
            Vector3 ab = tip - bladeBase;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - bladeBase, ab) / ab.sqrMagnitude) : 0f;
            distance = Vector3.Distance(p, bladeBase + ab * t);
            return t;
        }

        /// <summary>One sample of the sweep. True when the hand reaches the tip after touching the base in time.</summary>
        public bool Tick(float along, float distance, bool triggerHeld, float now)
        {
            if (!triggerHeld) { baseTouch = -1f; return false; }
            if (distance > NearDistance) return false;
            if (along <= BaseEnd) { baseTouch = now; return false; }
            if (along < TipStart || baseTouch < 0f || now - baseTouch > SweepSeconds) return false;
            baseTouch = -1f;
            return true;
        }

        public void Begin(float now) => until = now + Duration;

        public bool Active(float now) => until >= 0f && now < until;

        /// <summary>True on the first call after the imbue ran out.</summary>
        public bool Expired(float now)
        {
            if (until < 0f || now < until) return false;
            until = -1f;
            return true;
        }

        /// <summary>The element to imbue: the only one owned, or (both owned) the one not used last time.</summary>
        public static ImbueElement Pick(ImbueElement owned, ImbueElement last)
        {
            bool bleed = (owned & ImbueElement.Bleed) != 0, storm = (owned & ImbueElement.Storm) != 0;
            if (bleed && storm) return last == ImbueElement.Bleed ? ImbueElement.Storm : ImbueElement.Bleed;
            return bleed ? ImbueElement.Bleed : storm ? ImbueElement.Storm : ImbueElement.None;
        }
    }

    /// <summary>
    /// P4-04 S8 Element Imbue (Off-hand Art <see cref="ArtId.ElementImbue"/>). Sets
    /// <see cref="PlayerCombatModifiers.Imbued"/> (doubling <see cref="PlayerCombatModifiers.ProcRateScale"/>
    /// for the P4-05 procs) and tints the sword's rim through <see cref="RendererTint"/> (MPB, no point light,
    /// no new material). Needs an owned element (<see cref="PlayerCombatModifiers.OwnedElements"/>, set by
    /// P4-05's Bleed/Storm boons); with none the sweep only gives a short "no" tick.
    /// </summary>
    public class ElementImbueController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color BleedRim = new Color(2.5f, 0.15f, 0.1f), StormRim = new Color(0.3f, 0.9f, 2.5f);

        private readonly ImbueLogic logic = new ImbueLogic();
        private PlayerCombatModifiers mods;
        private KatanaHolster holster;
        private BladeDamager blade;
        private Renderer[] swordRenderers;
        private ImbueElement last;

        private void OnEnable() => ArtSlots.Register(ArtId.ElementImbue, OnPress);

        private void OnDisable()
        {
            ArtSlots.Unregister(ArtId.ElementImbue);
            if (logic.Active(Time.time) || (mods != null && mods.Imbued != ImbueElement.None)) Clear();
        }

        private static void OnPress() { } // the sweep is read every frame while the trigger is held

        private void Update()
        {
            float now = Time.time;
            if (logic.Expired(now)) Clear();
            if (!ArtSlots.IsEquipped(ArtSlot.Offhand, ArtId.ElementImbue) || !Resolve()) return;
            var rig = VRRig.Instance;
            if (rig == null || rig.LeftHand == null) return;
            blade.WorldEndpoints(out Vector3 bladeBase, out Vector3 tip);
            float along = ImbueLogic.Along(rig.LeftHand.position, bladeBase, tip, out float dist);
            if (!logic.Tick(along, dist, ArtSlots.OffhandHeld, Time.unscaledTime)) return;

            ImbueElement element = ImbueLogic.Pick(mods.OwnedElements, last);
            if (element == ImbueElement.None) { Haptics.Pulse(XRNode.LeftHand, 0.15f, 0.03f); return; }
            last = element;
            mods.Imbued = element;
            logic.Begin(now);
            Color rim = element == ImbueElement.Bleed ? BleedRim : StormRim;
            for (int i = 0; i < swordRenderers.Length; i++)
            {
                var r = swordRenderers[i];
                if (r == null) continue;
                var m = r.sharedMaterial;
                Color baseColor = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                RendererTint.Apply(r, baseColor, rim);
            }
            Haptics.Pulse(XRNode.LeftHand, 0.8f, 0.1f);
            Haptics.Pulse(XRNode.RightHand, 0.8f, 0.1f);
            EventBus.Publish(new AbilityActivated(ArtId.ElementImbue));
        }

        private bool Resolve()
        {
            if (mods == null) mods = GetComponentInParent<PlayerCombatModifiers>();
            if (holster == null) holster = GetComponentInParent<KatanaHolster>();
            if (blade == null && holster != null && holster.Sword != null)
            {
                blade = holster.Sword.GetComponentInChildren<BladeDamager>(true);
                swordRenderers = holster.Sword.GetComponentsInChildren<Renderer>(true); // once
            }
            return mods != null && blade != null;
        }

        private void Clear()
        {
            if (mods != null) mods.Imbued = ImbueElement.None;
            if (swordRenderers == null) return;
            for (int i = 0; i < swordRenderers.Length; i++)
                if (swordRenderers[i] != null) swordRenderers[i].SetPropertyBlock(null);
        }
    }
}
