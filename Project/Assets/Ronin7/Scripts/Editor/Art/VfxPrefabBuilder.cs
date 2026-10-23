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
            // P5-03: look. All optional; defaults = SoftDot billboard with a linear alpha fade.
            public string texture;                // SoftDot (default) | SparkStreak | Ring | Glyph
            public bool stretched;                // Stretched billboard along velocity (sparks)
            public float lengthScale;             // stretched length multiplier (default 2)
            public AnimationCurve sizeCurve;      // size over lifetime multiplier; null = constant
            public Gradient colorCurve;           // colour over lifetime; null = white, alpha 1 -> 0
        }

        private static readonly AnimationCurve Grow = AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);

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
            // P5-03 hit sparks x MaterialClass. Mech: amber streaks (stretched along velocity).
            new Spec { name = "SparkStreakFx", color = NeonPalette.Amber,  life = 0.2f,  speed = 9f,  size = 0.03f, max = 16, gravity = 0.4f, radius = 0.02f, texture = "SparkStreak", stretched = true, lengthScale = 3f },
            // Spectral: cyan/violet wisps drifting up (negative gravity).
            new Spec { name = "SpectralWisp",  color = NeonPalette.Cyan,   life = 0.4f,  speed = 0.8f, size = 0.07f, max = 16, gravity = -0.35f, radius = 0.05f, sizeCurve = Grow },
            // Parry ring: one expanding ring quad, 0.12 s, <= 0.5 m. A billboard particle, so no screen flash.
            new Spec { name = "ParryRing",     color = NeonPalette.Cyan,   life = 0.12f, speed = 0f,  size = 0.5f,  max = 4,  gravity = 0f,   radius = 0.001f, texture = "Ring", sizeCurve = Grow },
            // Posture break shards: short white stretched shards.
            new Spec { name = "PostureShard",  color = Color.white * 2f,   life = 0.35f, speed = 5f,  size = 0.03f, max = 24, gravity = 1.0f, radius = 0.05f, texture = "SparkStreak", stretched = true, lengthScale = 2f },
            // Spawn embers / pickup glints: rise slowly.
            new Spec { name = "SpawnEmbers",   color = NeonPalette.Violet, life = 0.4f,  speed = 0.6f, size = 0.05f, max = 20, gravity = -0.5f, radius = 0.35f, sizeCurve = Grow },
            // Ability glyph puff (Phase Step etc.).
            // Charged Slash: one flat Ring squashed along its travel (velocity is injected per emit) = a travelling crescent edge.
            new Spec { name = "SlashCrescent", color = Color.white * 2.5f, life = 0.3f, speed = 0f, size = 0.9f, max = 4, gravity = 0f, radius = 0.001f, texture = "Ring", stretched = true, lengthScale = 0.45f },
            new Spec { name = "GlyphPuff",     color = NeonPalette.Cyan,   life = 0.3f,  speed = 0.5f, size = 0.12f, max = 20, gravity = 0f,   radius = 0.2f,  texture = "Glyph", sizeCurve = Grow },
        };

        /// <summary>Saved (Resources) materials the runtime loads instead of building with Shader.Find:
        /// the death-dissolve swap target and the Mirror phantom's ghost material.</summary>
        private static void EnsureEffectMaterials()
        {
            string dissolve = VfxResourceFolder + "/EnemyPBR_Dissolve.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(dissolve) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Ronin7/EnemyDissolve")), dissolve);

            string ghost = VfxResourceFolder + "/MirrorGhost.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(ghost) == null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var c = new Color(0.75f, 0.85f, 1.0f, 0.35f);
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, ghost);
            }
        }

        private static readonly Gradient FadeOut = MakeFade();
        private static Gradient MakeFade()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private const string TexFolder = "Assets/Ronin7/Art/Textures/Vfx";

        /// <summary>SoftDot uses the shared material; other textures get their own additive copy
        /// (Vfx_{Texture}_Additive.mat) so sparks, rings and glyphs keep distinct maps.</summary>
        private static Material MaterialFor(string texture, Material soft)
        {
            if (string.IsNullOrEmpty(texture) || texture == "SoftDot") return soft;
            string path = $"{MatFolder}/Vfx_{texture}_Additive.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(soft);
                AssetDatabase.CreateAsset(m, path);
            }
            SetMap(m, texture);
            return m;
        }

        private static void SetMap(Material m, string texture)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{texture}.png");
            if (t != null) { m.SetTexture("_BaseMap", t); EditorUtility.SetDirty(m); }
        }

        /// <summary>Procedural 32-128 px alpha masks (white RGB, alpha = shape); an existing PNG is left alone.</summary>
        private static void EnsureTextures()
        {
            EnsureFolder(TexFolder);
            WriteTex("SoftDot", 64, 64, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 2f));
            // Bright core line fading along x, thin in y.
            WriteTex("SparkStreak", 128, 32, (u, v) => Mathf.Clamp01(1f - Mathf.Abs(u)) * Mathf.Exp(-v * v * 18f));
            WriteTex("Ring", 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.1f, 2f)) * Mathf.Clamp01((1f - r) * 8f);
            });
            // Diamond outline + centre dot.
            WriteTex("Glyph", 128, 128, (u, v) =>
            {
                float d = Mathf.Abs(u) + Mathf.Abs(v);
                return Mathf.Clamp01(Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.07f, 2f)) + Mathf.Exp(-(u * u + v * v) * 60f));
            });
            var soft = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMatPath);
            if (soft != null) SetMap(soft, "SoftDot");
            var trail = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/PlasmaTrail_Additive.mat");
            if (trail != null) SetMap(trail, "SoftDot");
        }

        /// <param name="f">Alpha for (u, v), each in -1..1 across the texture.</param>
        private static void WriteTex(string name, int w, int h, System.Func<float, float, float> f)
        {
            string path = $"{TexFolder}/{name}.png";
            if (System.IO.File.Exists(path)) return;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(f((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }

        [MenuItem("Tools/Space Samurai/Art/Build Combat VFX Prefabs", priority = 5)]
        public static void BuildAll()
        {
            EnsureFolder(VfxResourceFolder);
            var mat = EnsureAdditiveMaterial();
            EnsureTextures();
            EnsureEffectMaterials();
            foreach (var spec in Specs) BuildOne(spec, MaterialFor(spec.texture, mat));
            BuildShell();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[VfxPrefabBuilder] Built {Specs.Length} combat VFX prefab(s) into {VfxResourceFolder}.");
        }

        private const string ShellFolder = "Assets/Ronin7/Resources/VfxShell";
        private const string ShellMeshPath = "Assets/Ronin7/Art/Meshes/UnbrokenShell.asset";

        /// <summary>P5-03 Unbroken: open-front hemisphere mesh (back half of a unit sphere, opening toward +Z),
        /// a fresnel material and the shell prefab. Kept out of Resources/Vfx so the particle-prefab caps test skips it.</summary>
        private static void BuildShell()
        {
            EnsureFolder(ShellFolder);
            EnsureFolder("Assets/Ronin7/Art/Meshes");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ShellMeshPath);
            if (mesh == null) { mesh = MakeHemisphere(8, 24); AssetDatabase.CreateAsset(mesh, ShellMeshPath); }

            string matPath = MatFolder + "/Vfx_FresnelShell.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Ronin7/VfxFresnelShell"));
                mat.SetColor("_Color", new Color(0.3f, 0.9f, 2.5f, 1f));
                mat.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, matPath);
            }

            var go = new GameObject("UnbrokenShell");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(go, ShellFolder + "/UnbrokenShell.prefab");
            Object.DestroyImmediate(go);
        }

        private static Mesh MakeHemisphere(int rings, int segs)
        {
            var v = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= rings; i++)
            {
                float phi = i / (float)rings * Mathf.PI * 0.5f; // 0 = back pole (-Z), 90 deg = the open rim
                for (int j = 0; j < segs; j++)
                {
                    float th = j / (float)segs * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Sin(phi) * Mathf.Sin(th), -Mathf.Cos(phi)));
                }
            }
            var t = new System.Collections.Generic.List<int>();
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = i * segs + j, b = i * segs + (j + 1) % segs, c = a + segs, d = b + segs;
                    t.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = "UnbrokenShell" };
            m.SetVertices(v);
            m.SetNormals(v); // unit sphere: normal = position
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
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

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = spec.colorCurve ?? FadeOut;
            if (spec.sizeCurve != null)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, spec.sizeCurve);
            }

            // Everything heavy stays off for the Quest budget.
            var collision = ps.collision; collision.enabled = false;
            var lights = ps.lights; lights.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            var sub = ps.subEmitters; sub.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = spec.stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (spec.stretched) { renderer.lengthScale = spec.lengthScale > 0f ? spec.lengthScale : 2f; renderer.velocityScale = 0f; }
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
