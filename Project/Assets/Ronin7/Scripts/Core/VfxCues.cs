using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>P5-03: published just before a Phase Step fade-out, so VFX can capture the pre-blink pose
    /// (afterimage) and puff at the origin. The matching end is the existing <see cref="AbilityActivated"/>.</summary>
    public readonly struct PhaseStepStarted
    {
        public readonly Vector3 Origin;
        public PhaseStepStarted(Vector3 origin) { Origin = origin; }
    }

    /// <summary>P5-03: a skill landed or fired; carries only what its VFX needs. Ids are the <c>Id*</c> consts.</summary>
    public readonly struct SkillVfx
    {
        public const string Riposte = "vfx.riposte";
        public const string Iaido = "vfx.iaido";
        public const string ChargedSlash = "vfx.charged_slash";
        public readonly string Id;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public SkillVfx(string id, Vector3 point, Vector3 direction) { Id = id; Point = point; Direction = direction; }
    }

    /// <summary>P5-03: a pooled enemy was just activated at the end of its spawn telegraph (reverse dissolve).</summary>
    public readonly struct EnemyActivated
    {
        public readonly GameObject Enemy;
        public EnemyActivated(GameObject enemy) { Enemy = enemy; }
    }
}
