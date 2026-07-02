using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Procedurally builds the combat "particle storm" prefabs into <c>Resources/Vfx</c> so the runtime
    /// <c>CombatVfxController</c> can <see cref="Resources.Load"/> them by name (rebuild-safe — no GUID
    /// or inspector wiring to null). Every effect shares one additive-unlit neon material (so its HDR
    /// per-particle colour routes through Bloom) and is Quest-budgeted: <c>maxParticles</c> ≤ 30,
    /// lifetime ≤ 0.4 s, <c>playOnAwake = false</c>, looped with emission disabled (the pool injects
    /// bursts via Emit), <c>useUnscaledTime</c> so bursts animate through the deflect slow-mo, and no
    /// collision / lights / shadows / trails / sub-emitters.
    ///
    /// Idempotent and GUID-safe: <see cref="PrefabUtility.SaveAsPrefabAsset"/> overwrites in place.
    /// </summary>
    public static class VfxPrefabBuilder
    {
        private const string VfxResourceFolder = "Assets/Ronin7/Resources/Vfx";
        private const string MatFolder = "Assets/Ronin7/Art/Materials";
        private const string AdditiveMatPath = MatFolder + "/VfxNeon_Additive.mat";

        /// <summary>One effect prefab's tuning. Colour is HDR (pre-scaled) so it blooms.</summary>
        private struct Spec
        {
            public string name;
            public Color color;
            public float life;     // ≤ 0.4 s
            public float speed;    // start speed (m/s)
            public float size;
            public int max;        // ≤ 30
            public float gravity;
            public float radius;   // emit shape radius
        }

        private static readonly Spec[] Specs =
        {
            // Sword spark — amber hot sparks, scaled by swing speed at runtime.
            new Spec { name = "SwordSpark",   color = NeonPalette.Amber,   life = 0.25f, speed = 6f,  size = 0.04f, max = 24, gravity = 0.5f, radius = 0.04f },
            // Organic blood — non-HDR deep red so it reads as gore, not neon (still additive).
            new Spec { name = "BloodSplatter", color = new Color(0.8f, 0.04f, 0.04f), life = 0.3f, speed = 3f, size = 0.06f, max = 20, gravity = 1.4f, radius = 0.05f },
            // Deflect — bright cyan fan that pairs with the slow-mo.
            new Spec { name = "DeflectFlash", color = NeonPalette.Cyan,    life = 0.25f, speed = 8f,  size = 0.05f, max = 28, gravity = 0f,   radius = 0.03f },
            // Generic hit flash — small violet puff.
            new Spec { name = "HitFlash",     color = NeonPalette.Violet,  life = 0.15f, speed = 2.5f, size = 0.05f, max = 12, gravity = 0f,  radius = 0.04f },
            // Death — bigger magenta shard burst.
            new Spec { name = "DeathBurst",   color = NeonPalette.Magenta, life = 0.4f,  speed = 7f,  size = 0.07f, max = 30, gravity = 0.4f, radius = 0.08f },
            // Ship muzzle flash — quick cyan.
            new Spec { name = "MuzzleFlash",  color = NeonPalette.Cyan,    life = 0.12f, speed = 4f,  size = 0.06f, max = 14, gravity = 0f,   radius = 0.03f },
            // Bolt impact — amber spark.
            new Spec { name = "BoltImpact",   color = NeonPalette.Amber,   life = 0.2f,  speed = 5f,  size = 0.05f, max = 16, gravity = 0f,   radius = 0.04f },
            // Ship explosion — big amber puff.
            new Spec { name = "ShipExplosion", color = NeonPalette.Amber,  life = 0.4f,  speed = 10f, size = 0.12f, max = 30, gravity = 0f,   radius = 0.1f },
        };

        [MenuItem("Tools/Space Samurai/Art/Build Combat VFX Prefabs", priority = 5)]
        public static void BuildAll()
        {
            EnsureFolder(VfxResourceFolder);
            var mat = EnsureAdditiveMaterial();
            foreach (var spec in Specs) BuildOne(spec, mat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[VfxPrefabBuilder] Built {Specs.Length} combat VFX prefab(s) into {VfxResourceFolder}.");
        }

        private static void BuildOne(Spec spec, Material mat)
        {
            var go = new GameObject(spec.name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;            // stays alive so manually-Emitted bursts simulate
            main.playOnAwake = false;    // the pool / Emit drives it, never auto-play
            main.useUnscaledTime = true; // animate during the 0.25× deflect slow-mo
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = spec.life;
            main.startSpeed = spec.speed;
            main.startSize = spec.size;
            main.startColor = spec.color;
            main.gravityModifier = spec.gravity;
            main.maxParticles = spec.max;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = false;    // no rate/bursts — the pool injects via Emit(count)

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spec.radius;

            // Everything heavy stays off for the Quest budget.
            var collision = ps.collision; collision.enabled = false;
            var lights = ps.lights; lights.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            var sub = ps.subEmitters; sub.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            string path = $"{VfxResourceFolder}/{spec.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        /// <summary>Shared additive, unlit, vertex-coloured particle material — mirrors the plasma-trail
        /// setup so each prefab's HDR start colour routes through Bloom.</summary>
        private static Material EnsureAdditiveMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMatPath);
            if (existing != null) return existing;

            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);     // additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EnsureFolder(MatFolder);
            AssetDatabase.CreateAsset(m, AdditiveMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(AdditiveMatPath);
        }

        /// <summary>Creates <paramref name="folder"/> (and any missing parents) if absent.</summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
