using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P3-04 Charger (Coil_Syndicate_Ganger). At <see cref="MinRange"/>..<see cref="MaxRange"/> it commits to a
    /// straight lunge: the Windup is the crouch telegraph (set telegraphTime 1.0 in data), and the body slides
    /// via <see cref="Enemy.Displace"/> as the strike lands. Direction and length are fixed when the crouch
    /// starts, so it can be sidestepped. Closer than MinRange it falls back to the normal chop. Pair with the
    /// Recovery weakness (state mapping lives in Enemy.FilterDamage).
    /// </summary>
    public class ChargerEnemy : Enemy
    {
        public const float MinRange = 3f, MaxRange = 5f, LungeLength = 4f, Overshoot = 1f;
        public const float OpeningDashDelay = 2f; // seconds after activation before the guaranteed opening dash
        const float LungeFraction = 0.35f; // = MeleeAttacker's strike share of Active: the slide ends as the hit resolves

        [Tooltip("Optional ground streak telegraph (world-space, 2 points).")]
        [SerializeField] private LineRenderer streak;

        private bool charging, lunged;
        private Vector3 lungeDir;
        private float lungeDist;
        private float activeSince;
        private bool openingDone, openingNow;

        /// <summary>Pure: 4 m, but never further than the player's distance + 1 m.</summary>
        public static float LungeDistance(float playerDist) => Mathf.Min(LungeLength, playerDist + Overshoot);

        /// <summary>Pure: the opening dash is never capped at <see cref="LungeLength"/> — it closes the whole gap + overshoot.</summary>
        public static float OpeningDashDistance(float playerDist) => playerDist + Overshoot;

        /// <summary>Pure: true once, <see cref="OpeningDashDelay"/> s after activation, until the dash has been taken.</summary>
        public static bool OpeningDashDue(float secondsActive, bool done) => !done && secondsActive >= OpeningDashDelay;

        public static bool InChargeBand(float playerDist) => playerDist >= MinRange && playerDist <= MaxRange;

        protected override void Awake()
        {
            base.Awake();
            if (streak != null) { streak.useWorldSpace = true; streak.positionCount = 2; streak.enabled = false; }
        }

        private void OnEnable() { activeSince = Time.time; openingDone = false; }

        public override void Respawn(Vector3 position)
        {
            base.Respawn(position);
            activeSince = Time.time;
            openingDone = false;
        }

        protected override void OnChaseTick()
        {
            if (OpeningDashDue(Time.time - activeSince, openingDone)) { openingNow = true; BeginAttack(); return; }
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (InChargeBand(to.magnitude) && Time.time >= nextAttackTime) { BeginAttack(); return; }
            base.OnChaseTick();
        }

        // Parry/stagger/flinch/death mid-crouch skip OnActiveEnd, so drop the streak whenever the lunge can't be live.
        private void EndCharge()
        {
            charging = false;
            if (streak != null) streak.enabled = false;
        }

        protected override void BeforeStateTick()
        {
            base.BeforeStateTick();
            if (state != State.Windup && state != State.Active) EndCharge();
        }

        protected override void OnDied()
        {
            EndCharge();
            base.OnDied();
        }

        protected override void OnBeginAttack()
        {
            base.OnBeginAttack();
            EndCharge();
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            bool opening = openingNow;
            openingNow = false;
            if (opening) openingDone = true;
            charging = opening ? dist > 0.01f : InChargeBand(dist);
            lunged = false;
            if (!charging) return;
            lungeDir = to / dist;
            lungeDist = opening ? OpeningDashDistance(dist) : LungeDistance(dist);
            if (streak == null) return;
            Vector3 p = transform.position + Vector3.up * 0.02f;
            streak.SetPosition(0, p);
            streak.SetPosition(1, p + lungeDir * lungeDist);
            streak.enabled = true;
        }

        protected override void Update()
        {
            base.Update();
            if (!charging || lunged || state != State.Active) return;
            lunged = true;
            Displace(lungeDir * lungeDist, ActiveTime * LungeFraction);
        }

        protected override void OnActiveEnd()
        {
            base.OnActiveEnd();
            EndCharge();
        }
    }
}
