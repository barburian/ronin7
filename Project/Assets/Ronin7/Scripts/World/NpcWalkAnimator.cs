using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Procedural walk cycle for auto-rigged NPCs: swings the Rig_LegL/R and Rig_ArmL/R bones
    /// (baked into the character prefabs by the NpcAutoRigger editor tool) in opposite phase while
    /// the NPC is moving, plus a subtle torso bob, and eases everything back to the rest pose when
    /// it stops. Movement is self-measured from the transform's own position delta, so it works no
    /// matter who moves the NPC (NpcWander, StoryNpcWander, NpcWalker, or any scripted beat) — the
    /// movement scripts call <see cref="EnsureOn"/> so existing scenes pick this up without a
    /// rebuild. Disables itself when the character has no rig (e.g. placeholder greybox blobs),
    /// which safely degrades to the old rigid slide. Pure per-frame math on five transforms —
    /// no allocations, Quest-friendly.
    /// </summary>
    public class NpcWalkAnimator : MonoBehaviour
    {
        public const string RootBoneName = "Rig_Root";
        public const string BodyBoneName = "Rig_Body";
        public const string LegLBoneName = "Rig_LegL";
        public const string LegRBoneName = "Rig_LegR";
        public const string ArmLBoneName = "Rig_ArmL";
        public const string ArmRBoneName = "Rig_ArmR";
        public const string JawBoneName = "Rig_Jaw";

        [Tooltip("Speed (m/s) at which the swing reaches full amplitude.")]
        [SerializeField] private float fullSwingSpeed = 1.2f;

        [Tooltip("Length of one step in meters; sets stride frequency from measured speed.")]
        [SerializeField] private float strideLength = 0.65f;

        [SerializeField] private float legSwingDegrees = 32f;
        [SerializeField] private float armSwingDegrees = 18f;

        [Tooltip("BUG-44: enemies' rigged arms start in a T-pose; lower them this many degrees (about the body's forward axis, mirrored L/R). Combatants only. " +
                 "BUG-47: baked per rig from the measured arm (EnemyPrefabBaker.FitArmRest) - 0 on rigs whose arms already hang.")]
        [SerializeField] private float armsDownDegrees = DefaultArmsDownDegrees;
        public const float DefaultArmsDownDegrees = 75f;
        [Tooltip("Share of the right-arm attack swing the left arm follows so it is not frozen.")]
        [SerializeField] private float leftArmAttackScale = 0.4f;

        [Tooltip("Torso bob height in meters at full walk speed.")]
        [SerializeField] private float bobHeight = 0.02f;

        private Transform body, legL, legR, armL, armR;
        private Quaternion legLRest, legRRest, armLRest, armRRest; // arm rests include the arms-down offset
        private Vector3 bodyRestPos;
        private Vector3 lastPos;
        private float phase;
        private float smoothedSpeed;
        private float amplitude;
        internal float Phase => phase; // test seam (P2-11)

        // Right arm doubles as the enemy's weapon bone (MeleeAttacker.weapon); while the attacker is
        // posing it we must not overwrite it, then ease back to the walk swing instead of snapping.
        private const float ArmRBlendTime = 0.15f;
        private MeleeAttacker melee;
        private float armRBlend = 1f;

        /// <summary>BUG-47: mean arm elevation (degrees above the shoulder's horizontal) below which the rig's arms
        /// already hang. Measured: trash rigs -43.5..+18 (T/A-pose), boss rigs -61.5..-73 (arms at the sides).</summary>
        public const float HangingArmElevation = -52f;

        /// <summary>BUG-47 (pure): elevation of a shoulder-to-arm vector in the body's X/Y plane, mirrored so
        /// outward is 0 for both arms (T-pose 0, straight down -90).</summary>
        public static float ArmElevation(Vector3 shoulderToArm, bool right) =>
            Mathf.Atan2(shoulderToArm.y, right ? shoulderToArm.x : -shoulderToArm.x) * Mathf.Rad2Deg;

        /// <summary>BUG-47 (pure): the arms-down roll for a rig. Rolling arms that already hang by the T-pose
        /// amount swings them across the body (hands on the wrong side, the windup lifts the wrong way).</summary>
        public static float ArmsDownFor(float meanArmElevation, float fullDown) =>
            meanArmElevation <= HangingArmElevation ? 0f : fullDown;

        /// <summary>Adds (or finds) a walk animator on the given NPC root.</summary>
        public static NpcWalkAnimator EnsureOn(GameObject npc)
        {
            var anim = npc.GetComponent<NpcWalkAnimator>();
            return anim != null ? anim : npc.AddComponent<NpcWalkAnimator>();
        }

        private void Awake()
        {
            melee = GetComponent<MeleeAttacker>();
            body = FindDeep(transform, BodyBoneName);
            legL = FindDeep(transform, LegLBoneName);
            legR = FindDeep(transform, LegRBoneName);
            armL = FindDeep(transform, ArmLBoneName);
            armR = FindDeep(transform, ArmRBoneName);

            if (legL == null || legR == null)
            {
                enabled = false; // unrigged character (placeholder/non-humanoid): keep the old behavior
                return;
            }

            legLRest = legL.localRotation;
            legRRest = legR.localRotation;
            // BUG-44: character space is +Z forward, +X right, arms along +-X in the T-pose, so a roll about Z lowers
            // them (right arm -Z, left +Z). Swings then run about the parent's X (forward arc), applied BEFORE this rest.
            float down = melee != null && !melee.RigidHull ? armsDownDegrees : 0f;
            if (armL != null) armLRest = Quaternion.AngleAxis(down, Vector3.forward) * armL.localRotation;
            if (armR != null)
            {
                armRRest = Quaternion.AngleAxis(-down, Vector3.forward) * armR.localRotation;
                if (melee != null) melee.RigArmBase = armRRest;
            }
            if (body != null) bodyRestPos = body.localPosition;
            lastPos = transform.position;
        }

        private void OnEnable()
        {
            lastPos = transform.position;
            smoothedSpeed = 0f;
        }

        // LateUpdate so the coroutine-driven movement scripts have already moved us this frame.
        private void LateUpdate() => Tick(Time.deltaTime);

        internal void Tick(float dt)
        {
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos;
            lastPos = pos;
            delta.y = 0f;

            // EMA smoothing so a single stalled/teleported frame doesn't pop the pose.
            float travelled = delta.magnitude;
            float speed = travelled / dt;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-8f * dt));

            float targetAmp = Mathf.Clamp01(smoothedSpeed / fullSwingSpeed);
            amplitude = Mathf.MoveTowards(amplitude, targetAmp, 5f * dt);

            if (amplitude > 0.001f)
            {
                // P2-11: one full cycle (two steps) per 2*strideLength ACTUALLY travelled, so the feet
                // stay planted while the body speeds up or brakes (a lagging speed estimate slid them).
                phase = MovementSmoothing.AdvanceWalkPhase(phase, travelled, strideLength);
            }
            else
            {
                phase = 0f; // settled: snap the cycle so the rest pose is exact
            }

            // P2-11: rigid hulls (drones) glide; swinging their auto-rigged "legs" would crease the hull.
            float amp = melee != null && melee.RigidHull ? 0f : amplitude;
            float swing = Mathf.Sin(phase) * amp;

            legL.localRotation = legLRest * Quaternion.AngleAxis(swing * legSwingDegrees, Vector3.right);
            legR.localRotation = legRRest * Quaternion.AngleAxis(-swing * legSwingDegrees, Vector3.right);
            if (armL != null)
            {
                float atk = melee != null ? melee.ArmPoseAngle * leftArmAttackScale : 0f;
                armL.localRotation = Quaternion.AngleAxis(-swing * armSwingDegrees + atk, Vector3.right) * armLRest;
            }
            bool attacking = melee != null && melee.IsPosingWeapon;
            armRBlend = attacking ? 0f : Mathf.MoveTowards(armRBlend, 1f, dt / ArmRBlendTime);
            if (armR != null && !attacking)
            {
                Quaternion walk = Quaternion.AngleAxis(swing * armSwingDegrees, Vector3.right) * armRRest;
                armR.localRotation = armRBlend >= 1f ? walk : Quaternion.Slerp(armR.localRotation, walk, armRBlend);
            }
            if (body != null)
            {
                // Two bobs per cycle (one per step), rising as the legs pass each other.
                float bob = bobHeight * amp * 0.5f * (1f - Mathf.Cos(2f * phase));
                body.localPosition = bodyRestPos + Vector3.up * bob;
            }
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
