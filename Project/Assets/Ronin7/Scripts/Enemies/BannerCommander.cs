using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P8-06 Banner Commander. Carries a banner (EW_Banner, fixed kit) and, while alive, rallies every other enemy
    /// within <see cref="Radius"/>: their windups run at <see cref="RallyWindupScale"/> (Enemy.WindupScale). One commander
    /// per ally (Enemy.RalliedBy), so two never stack; its death releases every ally at once. Rallied allies show a
    /// small copy of the flag above the head (shape, steady: no colour pulse, no flash, no light). The check runs
    /// every <see cref="TickSeconds"/> over Health.Active, never per frame. Weak in melee (low HP, slow jab) and hangs
    /// back at <see cref="HangBack"/> while it has allies: a priority target.
    /// </summary>
    public class BannerCommander : Enemy
    {
        public const float Radius = 6f, RallyWindupScale = 0.8f, HangBack = 4f, TickSeconds = 0.25f;
        public const string FlagName = "Flag";
        private const float CueScale = 0.35f, CueLift = 0.05f; // body bounds already sit ~0.35 m above the head

        private float nextTick;
        private int allies;
        private Renderer flag;

        protected internal override float HoldRange => allies > 0 ? HangBack : base.HoldRange;

        protected override void Update()
        {
            base.Update();
            if (state == State.Dead || Time.time < nextTick) return;
            nextTick = Time.time + TickSeconds;
            RallyTick();
        }

        /// <summary>Claims allies in radius that no commander holds, releases its own that left (or died).</summary>
        internal void RallyTick()
        {
            allies = 0;
            Vector3 p = transform.position;
            foreach (var h in Health.Active)
            {
                if (!h.TryGetComponent(out Enemy e) || e == this) continue;
                bool alive = h.IsAlive;
                if (alive) allies++;
                bool inside = alive && (e.transform.position - p).sqrMagnitude <= Radius * Radius;
                if (inside && e.RalliedBy == null) Rally(e, this);
                else if (!inside && e.RalliedBy == this) Rally(e, null);
            }
        }

        private void Release()
        {
            allies = 0;
            foreach (var h in Health.Active)
                if (h.TryGetComponent(out Enemy e) && e.RalliedBy == this) Rally(e, null);
        }

        private void Rally(Enemy e, BannerCommander by)
        {
            e.RalliedBy = by;
            if (by != null && e.RallyCue == null) e.RallyCue = MakeCue(e);
            if (e.RallyCue != null) e.RallyCue.SetActive(by != null);
        }

        /// <summary>A small copy of this banner's flag above the ally's head: same mesh + shared palette material
        /// (instanced, no new SetPass), one draw while shown. Null when the banner has no flag (bare test bodies).</summary>
        private GameObject MakeCue(Enemy e)
        {
            if (flag == null)
                foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.name == FlagName) { flag = r; break; }
            if (flag == null) return null;
            var cue = Instantiate(flag.gameObject, e.transform, false);
            cue.name = "RallyCue";
            float top = e.BodyRenderer != null ? e.BodyRenderer.bounds.max.y - e.transform.position.y : 1.8f;
            cue.transform.localPosition = Vector3.up * ((top + CueLift) / Mathf.Max(e.transform.lossyScale.y, 1e-4f));
            cue.transform.localRotation = Quaternion.identity;
            Vector3 s = flag.transform.lossyScale * CueScale, ps = e.transform.lossyScale;
            cue.transform.localScale = new Vector3(s.x / ps.x, s.y / ps.y, s.z / ps.z);
            return cue;
        }

        protected override void OnDied()
        {
            Release(); // the buff ends the moment it dies
            base.OnDied();
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }
    }
}
