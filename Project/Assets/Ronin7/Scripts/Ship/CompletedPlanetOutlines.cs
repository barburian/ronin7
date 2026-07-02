using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Placed in the space scene by the builder. Marks story-completed planets with a green
    /// inverted-hull outline so the player can read progress from orbit. Creates one shared
    /// outline material instance, tinted and scaled by inspector fields.
    /// </summary>
    public class CompletedPlanetOutlines : MonoBehaviour
    {
        [SerializeField] private LandingApproach landing;
        [Tooltip("The SamuraiOutline material; instanced once and tinted green at runtime.")]
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color outlineColor = new Color(0.2f, 1f, 0.4f);
        [SerializeField, Range(0f, 0.1f)] private float outlineWidth = 0.03f;

        private void Start()
        {
            if (landing == null || outlineMaterial == null)
            {
                return;
            }

            // Create one shared instance configured with color and width.
            var mat = new Material(outlineMaterial);
            mat.SetColor("_OutlineColor", outlineColor);
            mat.SetFloat("_OutlineWidth", outlineWidth);

            // Apply outline to all completed planets.
            foreach (var l in landing.Landables)
            {
                if (l == null || l.target == null || !CampaignState.IsCompleted(l.destinationScene))
                {
                    continue;
                }

                Renderer renderer = l.target.GetComponent<Renderer>();
                if (renderer == null)
                {
                    renderer = l.target.GetComponentInChildren<Renderer>();
                }

                if (renderer == null)
                {
                    continue;
                }

                // Defensive: check if outline is already present (scene reloads recreate this component).
                Material[] sharedMats = renderer.sharedMaterials;
                bool alreadyHasOutline = false;
                foreach (var m in sharedMats)
                {
                    if (m != null && m.shader == mat.shader)
                    {
                        alreadyHasOutline = true;
                        break;
                    }
                }

                if (alreadyHasOutline)
                {
                    continue;
                }

                // Append the outline material.
                System.Array.Resize(ref sharedMats, sharedMats.Length + 1);
                sharedMats[sharedMats.Length - 1] = mat;
                renderer.sharedMaterials = sharedMats;
            }
        }
    }
}
