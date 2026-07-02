using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Single source of truth for the project's gameplay layer numbers and matching masks.
    ///
    /// Layers are resolved once at type-init via <see cref="LayerMask.NameToLayer"/>. If a layer
    /// is missing from Tags &amp; Layers (e.g. a scene loaded before the project was set up),
    /// the corresponding mask falls back to <c>~0</c> ("everything") so existing scenes keep
    /// working — the user gets a single warning per missing layer pointing at the setup gap.
    /// </summary>
    public static class Layers
    {
        public static readonly int Blade = Safe("Blade");
        public static readonly int Grabbable = Safe("Grabbable");
        public static readonly int EnemyHurtbox = Safe("EnemyHurtbox");
        public static readonly int Hittable = Safe("Hittable");

        public static readonly int BladeMask = Mask(Blade);
        public static readonly int GrabbableMask = Mask(Grabbable);
        public static readonly int EnemyHurtboxMask = Mask(EnemyHurtbox);
        public static readonly int HittableMask = Mask(Hittable);

        private static int Safe(string name)
        {
            int i = LayerMask.NameToLayer(name);
            if (i < 0)
            {
                Debug.LogWarning($"[Layers] Layer '{name}' is not defined in Tags & Layers. " +
                                 $"Falling back to ~0 (everything) for its mask.");
            }
            return i;
        }

        private static int Mask(int i) => i < 0 ? ~0 : 1 << i;
    }
}
