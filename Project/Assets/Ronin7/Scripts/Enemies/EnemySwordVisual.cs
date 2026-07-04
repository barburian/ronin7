using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Runtime placeholder-katana builder for melee enemies. Every checked-in scene wires
    /// <see cref="MeleeAttacker.weapon"/> (the ArmR pivot) down through an empty Sword/Blade/BladeTip
    /// chain with no MeshRenderer — so today a chopping enemy has nothing visible to telegraph its
    /// attack range or swing. <see cref="EnsureVisible"/> builds a low-poly Handle/Guard/Blade under
    /// <c>weapon</c> at spawn so <see cref="MeleeAttacker.PoseWeapon"/>'s existing rotation lerp swings
    /// something the player can actually see.
    ///
    /// Sizing mirrors the proportions of the editor-only ArtPrefabBuilder.AttachHeldKatanaRig (which
    /// this runtime code can't call — Ronin7.Enemies has no reference to Ronin7.Editor.Art), scaled down
    /// from that rig's ~1.2 m drawn-sword reach to land the Blade's tip at local (0,0,0.5) — the fixed
    /// BladeTip offset every scene builder already bakes under Sword/Blade (see XRRigBuilder.BuildEnemy /
    /// ChapterSharedBuilders' equivalent). Matching that offset keeps the visual blade exactly as long as
    /// the invisible point Enemy's parry capsule and swing-speed sampling actually use.
    /// </summary>
    public static class EnemySwordVisual
    {
        // AttachHeldKatanaRig's blade tip sits at local Z 1.20 (0.70 blade-centre + 0.5 half-length after
        // its unit-cube Blade is scaled to (0.05, 0.0144, 1.0)). Scale rescales every Z position/length
        // below by BladeReach/SourceReach so this fallback's blade tip lands on the 0.5 m contract point
        // instead — thickness (X/Y) is left as-is.
        private const float BladeReach = 0.5f;
        private const float SourceReach = 1.20f;
        private const float ReachScale = BladeReach / SourceReach;

        private static readonly Color HandleColor = new Color(0.12f, 0.1f, 0.18f);  // indigo wrap
        private static readonly Color GuardColor = new Color(0.78f, 0.55f, 0.18f);  // brass
        private static readonly Color BladeColor = new Color(0.92f, 0.95f, 1f);     // pale steel

        // One shared unlit material for every enemy's sword — parts differ only by MPB tint (see
        // RendererTint), so every placeholder sword in a scene stays on the same shared material
        // (SRP-batcher friendly) instead of allocating a Material instance per enemy.
        private static Material _sharedMaterial;

        /// <summary>
        /// Builds the placeholder katana under <paramref name="weapon"/> unless it's null or already
        /// carries a Renderer (an art-prefab enemy, or a second call) — idempotent, so it's safe to call
        /// unconditionally from <see cref="MeleeAttacker.Awake"/>. Returns true iff it built something.
        /// </summary>
        public static bool EnsureVisible(Transform weapon)
        {
            if (weapon == null) return false;
            if (weapon.GetComponentInChildren<Renderer>(true) != null) return false;

            if (_sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _sharedMaterial = new Material(shader);
            }
            var mat = _sharedMaterial;

            // Handle (tsuka).
            MakePart(weapon, "Handle", PrimitiveType.Cube, mat, HandleColor,
                new Vector3(0f, 0f, 0.08f * ReachScale), new Vector3(0.06f, 0.045f, 0.32f * ReachScale));

            // Guard (tsuba) — cylinder rolled 90° so its flat face is perpendicular to +Z.
            MakePart(weapon, "Guard", PrimitiveType.Cylinder, mat, GuardColor,
                new Vector3(0f, 0f, 0.18f * ReachScale), new Vector3(0.08f, 0.015f, 0.08f),
                Quaternion.Euler(90f, 0f, 0f));

            // Blade — extends along +Z; tip lands at exactly BladeReach (see ReachScale comment above).
            MakePart(weapon, "Blade", PrimitiveType.Cube, mat, BladeColor,
                new Vector3(0f, 0f, 0.70f * ReachScale), new Vector3(0.05f, 0.0144f, 1.0f * ReachScale));

            return true;
        }

        private static void MakePart(Transform parent, string name, PrimitiveType type, Material mat,
            Color color, Vector3 localPos, Vector3 localScale, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            // CreatePrimitive adds a Collider — visual-only geometry must not carry one, or it would
            // pollute Enemy's parry OverlapCapsule / BladeDamager's blade-layer physics probes. Destroy
            // is deferred (correct at runtime); tests call this in EditMode (Application.isPlaying ==
            // false), where Destroy is a no-op error, so fall back to DestroyImmediate there.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            RendererTint.Apply(renderer, color);
        }
    }
}
