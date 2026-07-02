using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.Ship;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Galaxy 1 solar-system builders. Phase 1: scene-path constants, the per-zone theme data
    /// carrier, and the shared visual helpers (sun, themed planets, asteroid ring, starfield).
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all of that
    /// class's private static helpers directly without duplicating them. Menu items and the
    /// actual scene authoring land in later phases.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths mirror the constants near the top of XRRigBuilder.cs. SceneFolder is already
        // defined in the other partial — reuse it, don't redefine it. Names are derived from the
        // filename so the runtime LoadScene name can never drift from the asset on disk.
        private const string Galaxy1ScenePath = SceneFolder + "/Galaxy1.unity";
        private static readonly string Galaxy1SceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1ScenePath);

        private const string Galaxy1JungleScenePath = SceneFolder + "/Galaxy1_Zone_Jungle.unity";
        private const string Galaxy1LavaScenePath = SceneFolder + "/Galaxy1_Zone_Lava.unity";
        private const string Galaxy1DesertScenePath = SceneFolder + "/Galaxy1_Zone_Desert.unity";
        private const string Galaxy1WaterScenePath = SceneFolder + "/Galaxy1_Zone_Water.unity";
        private const string Galaxy1FrostScenePath = SceneFolder + "/Galaxy1_Zone_Frost.unity";
        private const string Galaxy1CorsairScenePath = SceneFolder + "/Galaxy1_Corsair.unity";

        private static readonly string Galaxy1JungleSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1JungleScenePath);
        private static readonly string Galaxy1LavaSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1LavaScenePath);
        private static readonly string Galaxy1DesertSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1DesertScenePath);
        private static readonly string Galaxy1WaterSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1WaterScenePath);
        private static readonly string Galaxy1FrostSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1FrostScenePath);
        private static readonly string Galaxy1CorsairSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1CorsairScenePath);

        private const string Galaxy1Ep02DockingScenePath = SceneFolder + "/Galaxy1_EP02_Docking.unity";
        private const string Galaxy1Ep02PensScenePath = SceneFolder + "/Galaxy1_EP02_Pens.unity";
        private const string Galaxy1Ep02CoreScenePath = SceneFolder + "/Galaxy1_EP02_Core.unity";
        private const string Galaxy1Ep02SafeHouseScenePath = SceneFolder + "/Galaxy1_EP02_SafeHouse.unity";

        private static readonly string Galaxy1Ep02DockingSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep02DockingScenePath);
        private static readonly string Galaxy1Ep02PensSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep02PensScenePath);
        private static readonly string Galaxy1Ep02CoreSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep02CoreScenePath);
        private static readonly string Galaxy1Ep02SafeHouseSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy1Ep02SafeHouseScenePath);

        /// <summary>
        /// Plain data carrier describing one themed on-foot zone: its display label, the scene asset
        /// it builds into, and the palette / key-light setup that gives the zone its mood. The
        /// optional <see cref="propBuilder"/> is invoked in Phase 4 to scatter zone-specific props;
        /// it takes the zone-root parent transform and the zone radius. Null means "no props yet".
        /// </summary>
        private struct GalaxyZoneTheme
        {
            public string displayName;
            public string scenePath;
            public Color floorColor;
            public Color ambientColor;
            public Color lightColor;
            public Vector3 lightEuler;
            public float lightIntensity;
            public Color skyColor;
            public System.Action<Transform, float> propBuilder;
        }

        /// <summary>
        /// Builds a small unlit material in the given <paramref name="color"/> using URP's Unlit
        /// shader, setting whichever colour property the shader exposes. Used for self-luminous
        /// visuals (the sun glow, the starfield) that must read uniformly bright regardless of
        /// scene lighting. Mirrors the material setup in <see cref="BuildSun"/>.
        /// </summary>
        private static Material MakeUnlitMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            return mat;
        }

        /// <summary>
        /// Builds a faint transparent "glass" material (URP Unlit, alpha-blended) for the cockpit
        /// canopy pane. Configured for the transparent surface path so the low-alpha tint lets the
        /// space view read through clearly. No collider is ever attached to glass meshes, so bolts
        /// (which SphereCast ignoring triggers) pass unaffected.
        /// </summary>
        private static Material MakeGlassMaterial(Color color, float alpha)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            color.a = alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.color = color;
            mat.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);     // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>
        /// Spawns a decorative primitive like <see cref="AddVisualTinted"/> but assigns an UNLIT
        /// (self-luminous) material via <see cref="MakeUnlitMaterial"/> so the mesh reads uniformly
        /// bright regardless of scene lighting — used for glowing props (lava cracks, embers, water
        /// shimmer). The collider is stripped so the prop is purely scenery, and it's parented under
        /// <paramref name="parent"/> at <paramref name="pos"/> local position.
        /// </summary>
        private static GameObject AddUnlitVisual(Transform parent, string name, Vector3 pos,
            Vector3 scale, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) { go.transform.SetParent(parent, false); go.transform.localPosition = pos; }
            else go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(color);
            return go;
        }

        /// <summary>
        /// Sleek glass-bubble canopy shared by every galaxy cabin (Galaxy1-4). Replaces the old heavy
        /// pillar/header/sill + single flat pane with a slim dark frame, a wide wraparound 3-pane
        /// windshield (a flat centre flanked by two outward-canted side panes), and cyan self-luminous
        /// edge strips that bloom into a neon frame-light. The opaque frame stays out of the forward
        /// gun/sight box (pillars at |x|≳1.0, header above y2.25, sill below y0.6); the glass panes are
        /// collider-free so gun bolts pass straight through. <paramref name="cabinHalfWidth"/> is the
        /// cabin's wall half-width (1.9 for Galaxy1's wide cabin, 1.55 for the others) so the frame
        /// always meets the walls regardless of cabin size.
        /// </summary>
        private static void BuildGlassCanopyFrame(Transform cockpit, Color frameColor, float cabinHalfWidth)
        {
            var cyan    = NeonPalette.Cyan;     // HDR — blooms into a neon frame-light
            var cyanDim = NeonPalette.CyanDim;  // subtler vertical edge

            float openHalf = cabinHalfWidth - 0.35f;   // glass opening half-width (slim frame margin)
            float pillarW  = 0.35f;
            float pillarX  = cabinHalfWidth - pillarW * 0.5f;
            float fullW    = cabinHalfWidth * 2f;

            // Slim dark frame: pillars meeting the walls, a thin header and sill.
            AddVisualTinted(cockpit, "WindPillarL", new Vector3(-pillarX, 1.3f, 1.22f), new Vector3(pillarW, 2.6f, 0.12f), PrimitiveType.Cube, frameColor);
            AddVisualTinted(cockpit, "WindPillarR", new Vector3(pillarX, 1.3f, 1.22f),  new Vector3(pillarW, 2.6f, 0.12f), PrimitiveType.Cube, frameColor);
            AddVisualTinted(cockpit, "WindHeader",  new Vector3(0f, 2.4f, 1.22f),  new Vector3(fullW, 0.22f, 0.12f), PrimitiveType.Cube, frameColor);
            AddVisualTinted(cockpit, "WindSill",    new Vector3(0f, 0.3f, 1.22f),   new Vector3(fullW, 0.5f, 0.12f),  PrimitiveType.Cube, frameColor);

            // Wraparound windshield: a flat centre pane flanked by two outward-canted side panes so the
            // glass curves at the edges (a faceted bubble). All collider-free.
            var glassTint = new Color(0.45f, 0.6f, 0.8f);
            void Pane(string name, Vector3 pos, Vector3 scale, Quaternion rot)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = name;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(cockpit, false);
                g.transform.localPosition = pos;
                g.transform.localRotation = rot;
                g.transform.localScale = scale;
                g.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(glassTint, 0.12f);
            }
            Pane("WindshieldGlass", new Vector3(0f, 1.42f, 1.24f), new Vector3(openHalf * 1.2f, 1.7f, 0.02f), Quaternion.identity);
            Pane("WindshieldGlassL", new Vector3(-openHalf * 0.78f, 1.42f, 1.14f), new Vector3(openHalf * 0.7f, 1.7f, 0.02f), Quaternion.Euler(0f, -32f, 0f));
            Pane("WindshieldGlassR", new Vector3(openHalf * 0.78f, 1.42f, 1.14f), new Vector3(openHalf * 0.7f, 1.7f, 0.02f), Quaternion.Euler(0f, 32f, 0f));

            // Cyan neon edge-light framing the glass (self-luminous; Bloom turns it into glow).
            AddUnlitVisual(cockpit, "CanopyTrimTop", new Vector3(0f, 2.27f, 1.23f), new Vector3(openHalf * 2f, 0.03f, 0.04f), PrimitiveType.Cube, cyan);
            AddUnlitVisual(cockpit, "CanopyTrimBot", new Vector3(0f, 0.57f, 1.23f), new Vector3(openHalf * 2f, 0.03f, 0.04f), PrimitiveType.Cube, cyan);
            AddUnlitVisual(cockpit, "CanopyTrimL", new Vector3(-openHalf, 1.42f, 1.23f), new Vector3(0.03f, 1.7f, 0.04f), PrimitiveType.Cube, cyanDim);
            AddUnlitVisual(cockpit, "CanopyTrimR", new Vector3(openHalf, 1.42f, 1.23f), new Vector3(0.03f, 1.7f, 0.04f), PrimitiveType.Cube, cyanDim);
        }

        /// <summary>
        /// Places a self-luminous "Sun Visual" sphere at <paramref name="center"/> (universe-local)
        /// and aims <paramref name="sunLight"/> to shine FROM the sun toward the player virtual
        /// origin (universe-local zero). Unlike <see cref="BuildSun"/> the caller picks the exact
        /// position and scale, so this is the variant the Galaxy 1 layout drives. URP Unlit gives a
        /// uniform glowing disc; the collider is stripped so the sun is never a hazard.
        /// </summary>
        private static GameObject BuildGalaxySun(Transform universe, Light sunLight, Vector3 center, float scale)
        {
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun Visual";
            Object.DestroyImmediate(sun.GetComponent<Collider>());
            sun.transform.SetParent(universe, false);
            sun.transform.localPosition = center;
            sun.transform.localScale = Vector3.one * scale;

            var renderer = sun.GetComponent<Renderer>();
            // Dedicated HDR-emissive sun material: the shared LDR MakeUnlitMaterial renders the disc
            // dark/black in the player build, so drive the base color into HDR (×3, components > 1) on
            // URP/Unlit so the sun reads reliably bright AND blooms under the threshold-0.9 grade. Inlined
            // (not via MakeUnlitMaterial) so that shared helper stays LDR for every other prop that uses it.
            var sunMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var sunColor = new Color(1f, 0.96f, 0.85f) * 3f;
            sunColor.a = 1f;
            if (sunMat.HasProperty("_BaseColor")) sunMat.SetColor("_BaseColor", sunColor);
            if (sunMat.HasProperty("_Color")) sunMat.color = sunColor;
            renderer.sharedMaterial = sunMat;

            // Aim the key light so its rays travel from the sun through the origin region: the light
            // points along (origin - sun). RenderSettings.sun pins URP's main directional to this.
            // A SunLightAimer (wired by the scene builder) re-aims it at runtime as the universe moves.
            sunLight.transform.rotation = Quaternion.LookRotation((Vector3.zero - center).normalized);
            sunLight.intensity = 1.3f;
            RenderSettings.sun = sunLight;

            // Warm accent point light riding the sun so nearby asteroids/wrecks catch a glow. Range is
            // world units (unaffected by the parent's scale); shadows off keeps it cheap on Quest.
            var glowGo = new GameObject("Sun Glow Light");
            glowGo.transform.SetParent(sun.transform, false);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.6f);
            glow.intensity = 2f;
            glow.range = 800f;
            glow.shadows = LightShadows.None;

            return sun;
        }

        /// <summary>
        /// Builds one planet at <paramref name="localPos"/> (universe-local) with an EXPLICIT biome
        /// material rather than the position-hash variant <see cref="BuildPlanet"/> picks. Falls back
        /// to a tinted greybox sphere when the art prefab is missing; the biome material at
        /// SamuraiToon_Planet_<paramref name="biomeMatName"/> is applied to the child renderer when it
        /// exists, then tinted by <paramref name="themeColor"/> so the planet reads the zone's palette
        /// on both the prefab and greybox paths.
        /// </summary>
        private static GameObject BuildThemedPlanet(Transform universe, Vector3 localPos, float scale,
            Color themeColor, string biomeMatName)
        {
            System.Func<GameObject> greybox = () =>
                AddVisualTinted(null, "Planet", Vector3.zero, Vector3.one * scale, PrimitiveType.Sphere, themeColor);
            var go = ArtPrefabRegistry.TryInstantiateOrFallback(PlanetPrefabPath, greybox, universe);
            if (go == null) return null;

            go.transform.localPosition = localPos;
            // The prefab instance arrives at its authored scale; force it to the requested size. The
            // greybox already baked `scale` into its localScale, so setting it again is harmless.
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponentInChildren<Renderer>();
            var biomeMat = AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/Ronin7/Art/Materials/SamuraiToon_Planet_{biomeMatName}.mat");
            if (biomeMat != null && renderer != null) renderer.sharedMaterial = biomeMat;
            if (renderer != null) TintShared(renderer, themeColor);
            return go;
        }

        /// <summary>
        /// <see cref="BuildThemedPlanet"/> plus a canon name. Surface detail (caps, clouds, patches)
        /// is layered on by the Add* helpers below — all child primitives in PLANET-LOCAL space
        /// (the planet mesh is a unit-diameter sphere at the root, so surface radius ≈ 0.5), which
        /// keeps them scaling and orbiting with the planet for free.
        /// </summary>
        private static GameObject BuildCanonPlanet(Transform universe, string name, Vector3 localPos,
            float scale, Color themeColor, string biomeMatName)
        {
            var go = BuildThemedPlanet(universe, localPos, scale, themeColor, biomeMatName);
            if (go != null) go.name = name;
            return go;
        }

        /// <summary>Flattened ice spheres over both poles (glacial / ice worlds).</summary>
        private static void AddPolarCaps(GameObject planet, Color color)
        {
            if (planet == null) return;
            AddVisualTinted(planet.transform, "CapN", new Vector3(0f, 0.45f, 0f),
                new Vector3(0.55f, 0.18f, 0.55f), PrimitiveType.Sphere, color);
            AddVisualTinted(planet.transform, "CapS", new Vector3(0f, -0.45f, 0f),
                new Vector3(0.55f, 0.18f, 0.55f), PrimitiveType.Sphere, color);
        }

        /// <summary>Translucent slightly-larger shell so the planet reads as cloud-covered.</summary>
        private static void AddCloudShell(GameObject planet, Color color, float alpha)
        {
            if (planet == null) return;
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "CloudShell";
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(planet.transform, false);
            shell.transform.localScale = Vector3.one * 1.06f;
            shell.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(color, alpha);
        }

        /// <summary>
        /// Flattened tinted spheres hugging random points of the surface — continents, canyon bands,
        /// or biome blotches depending on the tint. Deterministic per <paramref name="seed"/>.
        /// </summary>
        private static void AddSurfacePatches(GameObject planet, int count, Color color, int seed)
        {
            if (planet == null) return;
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomDirection(rng);
                var patch = AddVisualTinted(planet.transform, $"Patch{i}", dir * 0.48f,
                    new Vector3(0.45f, 0.08f, 0.55f), PrimitiveType.Sphere, color);
                patch.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
        }

        /// <summary>Small unlit glow cubes dotted over the surface (industrial city lights).</summary>
        private static void AddCityLights(GameObject planet, int count, Color color, int seed)
        {
            if (planet == null) return;
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomDirection(rng);
                AddUnlitVisual(planet.transform, $"CityLight{i}", dir * 0.5f,
                    Vector3.one * 0.05f, PrimitiveType.Cube, color);
            }
        }

        private static Vector3 RandomDirection(System.Random rng)
        {
            Vector3 dir = new Vector3(
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f);
            return dir.sqrMagnitude < 1e-6f ? Vector3.up : dir.normalized;
        }

        /// <summary>A point on a circular orbit — used so the authored pose matches PlanetOrbit's t=0 pose.</summary>
        private static Vector3 OrbitPos(Vector3 center, float radius, float angleDeg, float yOffset = 0f)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(rad) * radius, yOffset, Mathf.Sin(rad) * radius);
        }

        /// <summary>Attach + wire a <see cref="PlanetOrbit"/> so the body circles <paramref name="center"/> at runtime.</summary>
        private static void AddPlanetOrbit(GameObject body, Transform center, float radius,
            float angularSpeedDeg, float startAngleDeg, float yOffset = 0f)
        {
            if (body == null) return;
            var orbit = body.AddComponent<PlanetOrbit>();
            var so = new SerializedObject(orbit);
            SetObjectRef(so, "center", center);
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("angularSpeedDeg").floatValue = angularSpeedDeg;
            so.FindProperty("startAngleDeg").floatValue = startAngleDeg;
            so.FindProperty("yOffset").floatValue = yOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The Rust Collective: derelict warship hulks scattered through the debris ring — stacked
        /// rust-tinted primitives, purely decorative (collider-free) so they never interact with the
        /// asteroid hazard. Deterministic via a fixed seed.
        /// </summary>
        private static void BuildWreckHulks(Transform universe, Vector3 center, float ringRadius,
            float halfWidth, int count)
        {
            var rng = new System.Random(20260611);
            var rust = new Color(0.45f, 0.30f, 0.22f);
            var darkSteel = new Color(0.28f, 0.28f, 0.31f);
            var scorched = new Color(0.18f, 0.15f, 0.13f);

            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = ringRadius + ((float)rng.NextDouble() * 2f - 1f) * halfWidth;
                float y = ((float)rng.NextDouble() * 2f - 1f) * 30f;
                var root = new GameObject($"Wreck Hulk {i}");
                root.transform.SetParent(universe, false);
                root.transform.localPosition = center + new Vector3(
                    Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                root.transform.localRotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);

                // Broken spine + a few hull chunks, sized to read at ring distances.
                float len = 20f + (float)rng.NextDouble() * 20f;
                AddVisualTinted(root.transform, "Spine", Vector3.zero,
                    new Vector3(4f, 3f, len), PrimitiveType.Cube, rust);
                AddVisualTinted(root.transform, "ChunkA", new Vector3(2.5f, 1.5f, len * 0.2f),
                    new Vector3(5f, 4f, 6f), PrimitiveType.Cube, darkSteel);
                AddVisualTinted(root.transform, "ChunkB", new Vector3(-2f, -1f, -len * 0.25f),
                    new Vector3(3f, 5f, 5f), PrimitiveType.Cube, scorched);
                if (i % 2 == 0)
                    AddVisualTinted(root.transform, "Mast", new Vector3(0f, 4f, len * 0.1f),
                        new Vector3(0.6f, 5f, 0.6f), PrimitiveType.Cylinder, darkSteel);
            }
        }

        /// <summary>
        /// The Corsair — Kessler's salvage-converted fighter, canon "a patchwork vessel that
        /// shouldn't fly but does" — as a BIG dockable exterior. Authored ~20 local units long at
        /// root scale 6 → ~120 universe units, i.e. roughly 2× a planet's radius. Same primitive
        /// recipe as <see cref="BuildKesslerMothership"/> (collider-free parts under the universe);
        /// the port-side docking bay carries a green unlit strip so the approach reads visually.
        /// </summary>
        private static GameObject BuildCorsairExterior(Transform universe, Vector3 localPos, float scale)
        {
            var root = new GameObject("Corsair");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = localPos;
            root.transform.localScale = Vector3.one * scale;

            var worn = new Color(0.50f, 0.45f, 0.40f);   // weathered base hull
            var steel = new Color(0.55f, 0.58f, 0.62f);
            var brass = new Color(0.70f, 0.55f, 0.25f);
            var rust = new Color(0.45f, 0.30f, 0.22f);
            var darkSteel = new Color(0.32f, 0.34f, 0.38f);
            var dark = new Color(0.06f, 0.06f, 0.08f);
            var glow = new Color(0.3f, 0.7f, 1f);

            GameObject Part(string n, PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = n;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p;
                go.transform.localScale = s;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                TintShared(go.GetComponent<Renderer>(), c);
                return go;
            }

            var faceZ = Quaternion.Euler(90f, 0f, 0f); // cylinders point +Z instead of +Y

            // Long central fuselage + nose wedge + raised cockpit with a glowing canopy.
            Part("Fuselage", PrimitiveType.Cube, Vector3.zero, new Vector3(4f, 3f, 18f), worn);
            Part("Nose", PrimitiveType.Cube, new Vector3(0f, -0.2f, 9.6f), new Vector3(2.6f, 2.2f, 3f), steel);
            Part("CockpitHull", PrimitiveType.Cube, new Vector3(0f, 1.7f, 6.5f), new Vector3(2.4f, 1.4f, 3.2f), steel);
            var canopy = Part("Canopy", PrimitiveType.Cube, new Vector3(0f, 2.1f, 7.2f), new Vector3(1.8f, 0.8f, 1.6f), glow);
            canopy.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(glow, 0.5f);

            // Mismatched salvage armor patches, slightly proud of the hull in varied tints.
            Part("PatchA", PrimitiveType.Cube, new Vector3(1.6f, 0.8f, 3f), new Vector3(1.2f, 1.0f, 3.5f), rust);
            Part("PatchB", PrimitiveType.Cube, new Vector3(-1.7f, -0.5f, -1f), new Vector3(1.0f, 1.4f, 4f), steel);
            Part("PatchC", PrimitiveType.Cube, new Vector3(0.4f, 1.6f, -3f), new Vector3(2.2f, 0.4f, 2.8f), brass);
            Part("PatchD", PrimitiveType.Cube, new Vector3(-1.2f, 1.4f, 1.5f), new Vector3(1.4f, 0.5f, 2.2f), rust);
            Part("PatchE", PrimitiveType.Cube, new Vector3(1.8f, -1.0f, -5f), new Vector3(0.8f, 1.2f, 2.6f), brass);

            // Stub wings with tip pods.
            Part("WingL", PrimitiveType.Cube, new Vector3(-4f, 0f, -2f), new Vector3(4.5f, 0.5f, 4f), worn);
            Part("WingR", PrimitiveType.Cube, new Vector3(4f, 0f, -2f), new Vector3(4.5f, 0.5f, 4f), worn);
            Part("PodL", PrimitiveType.Cube, new Vector3(-6f, 0f, -2f), new Vector3(1.2f, 1.2f, 5f), steel);
            Part("PodR", PrimitiveType.Cube, new Vector3(6f, 0f, -2f), new Vector3(1.2f, 1.2f, 5f), steel);

            // Dorsal turret: ring base, housing, twin barrels.
            Part("TurretBase", PrimitiveType.Cylinder, new Vector3(0f, 1.7f, 1f), new Vector3(1.4f, 0.25f, 1.4f), steel);
            Part("TurretHousing", PrimitiveType.Cube, new Vector3(0f, 2.2f, 1f), new Vector3(1.0f, 0.7f, 1.4f), darkSteel);
            Part("TurretBarrelL", PrimitiveType.Cylinder, new Vector3(-0.25f, 2.2f, 2.2f), new Vector3(0.12f, 0.9f, 0.12f), steel, faceZ);
            Part("TurretBarrelR", PrimitiveType.Cylinder, new Vector3(0.25f, 2.2f, 2.2f), new Vector3(0.12f, 0.9f, 0.12f), steel, faceZ);

            // Engine block: wide stern + three exhausts with unlit glow discs.
            Part("EngineBlock", PrimitiveType.Cube, new Vector3(0f, 0f, -9.5f), new Vector3(5f, 3.5f, 3f), steel);
            for (int i = -1; i <= 1; i++)
            {
                Part($"Exhaust{i + 1}", PrimitiveType.Cylinder, new Vector3(i * 1.5f, 0f, -11.2f), new Vector3(0.9f, 0.5f, 0.9f), darkSteel, faceZ);
                var disc = Part($"ExhaustGlow{i + 1}", PrimitiveType.Cylinder, new Vector3(i * 1.5f, 0f, -11.8f), new Vector3(0.7f, 0.05f, 0.7f), glow, faceZ);
                disc.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(glow);
            }

            // Ventral cargo pod with a brass strap (the salvage-hauler half of the silhouette).
            Part("CargoPod", PrimitiveType.Cube, new Vector3(0f, -2.4f, -2f), new Vector3(3f, 2.2f, 8f), worn);
            Part("CargoStrap", PrimitiveType.Cube, new Vector3(0f, -2.4f, -2f), new Vector3(3.2f, 0.6f, 1.4f), brass);

            // Antenna masts.
            Part("MastA", PrimitiveType.Cylinder, new Vector3(0.9f, 2.9f, 4.5f), new Vector3(0.06f, 1.1f, 0.06f), steel);
            Part("MastB", PrimitiveType.Cylinder, new Vector3(-1.1f, 2.7f, -6f), new Vector3(0.06f, 0.9f, 0.06f), steel);

            // Port-side docking bay: a dark inset mouth + green unlit guide strip — the visual cue
            // for where the landing/docking approach reads.
            Part("DockBay", PrimitiveType.Cube, new Vector3(-2.05f, -0.4f, 1f), new Vector3(0.5f, 1.8f, 3.2f), dark);
            var strip = Part("DockStrip", PrimitiveType.Cube, new Vector3(-2.35f, -0.4f, 1f), new Vector3(0.06f, 0.15f, 2.8f), Color.green);
            strip.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.2f, 1f, 0.4f));

            return root;
        }

        /// <summary>
        /// Builds a chrome orbital ring station near the frozen Velorum moon: a hub sphere with
        /// radiating spokes connecting to ring segments, plus a docking arm and antenna. The station
        /// orbits the moon at ~70u radius with slow angular speed. Returns the docking-arm transform
        /// for landable wiring. No colliders (visual only — station is a static prop).
        /// </summary>
        private static GameObject BuildVelorumStation(Transform universe, GameObject velorumMoon)
        {
            var root = new GameObject("Velorum Station");
            root.transform.SetParent(universe, false);
            // Position station near the moon (this will be refined by the orbit component)
            if (velorumMoon != null)
            {
                root.transform.localPosition = velorumMoon.transform.localPosition + new Vector3(70f, 0f, 0f);
            }

            var chrome = new Color(0.75f, 0.78f, 0.82f);  // bright metallic light-grey
            var darkChrome = new Color(0.55f, 0.58f, 0.62f);

            GameObject Part(string n, PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = n;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p;
                go.transform.localScale = s;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                TintShared(go.GetComponent<Renderer>(), c);
                return go;
            }

            // Central hub sphere.
            Part("Hub", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 8f, chrome);

            // Four box spokes connecting hub to ring.
            var spokeLength = 27f;  // reaches past the hub to the ring
            var spokeScale = new Vector3(1.5f, 1.5f, spokeLength);
            Part("Spoke1", PrimitiveType.Cube, new Vector3(spokeLength / 2f, 0f, 0f), spokeScale, darkChrome);
            Part("Spoke2", PrimitiveType.Cube, new Vector3(-spokeLength / 2f, 0f, 0f), spokeScale, darkChrome);
            Part("Spoke3", PrimitiveType.Cube, new Vector3(0f, 0f, spokeLength / 2f), spokeScale, darkChrome);
            Part("Spoke4", PrimitiveType.Cube, new Vector3(0f, 0f, -spokeLength / 2f), spokeScale, darkChrome);

            // Orbital ring: 20 cube segments placed on a circle in the XZ plane, tangentially rotated.
            int ringSegments = 20;
            float ringRadius = 25f;
            var ringSegmentScale = new Vector3(9f, 2.5f, 3.5f);  // thin boxes that overlap into a ring
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = (i / (float)ringSegments) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
                // Rotate segment to be tangent to the circle
                Quaternion rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up);
                Part($"RingSegment{i}", PrimitiveType.Cube, pos, ringSegmentScale, chrome, rot);
            }

            // Docking arm: a long box protruding outward from the ring past the hub.
            var dockingArm = Part("DockingArm", PrimitiveType.Cube, new Vector3(35f, 0f, 0f), new Vector3(4f, 4f, 14f), chrome);
            var dockingArmTransform = dockingArm.transform;

            // Antenna: two small cylinders on the hub.
            var faceZ = Quaternion.Euler(90f, 0f, 0f);
            Part("Antenna1", PrimitiveType.Cylinder, new Vector3(0f, 5f, 0f), new Vector3(0.3f, 2f, 0.3f), darkChrome, faceZ);
            Part("Antenna2", PrimitiveType.Cylinder, new Vector3(-3f, 4f, 0f), new Vector3(0.3f, 1.8f, 0.3f), darkChrome, faceZ);

            // Accent light on the docking arm (parented so it follows the station's orbit).
            var dockLight = new GameObject("DockingLight");
            dockLight.transform.SetParent(root.transform, false);
            dockLight.transform.localPosition = new Vector3(42f, 0f, 0f);
            var dockLightComp = dockLight.AddComponent<Light>();
            dockLightComp.type = LightType.Point;
            dockLightComp.color = new Color(0.2f, 0.8f, 1f);
            dockLightComp.intensity = 1.5f;
            dockLightComp.range = 20f;

            // Add orbit: station orbits the moon at ~70u radius, slow angular speed.
            if (velorumMoon != null)
            {
                AddPlanetOrbit(root, velorumMoon.transform, 70f, 0.5f, 0f);
            }

            return dockingArmTransform.gameObject;  // Return docking arm for landable target
        }

        /// <summary>
        /// Builds the Grimdock hauler station for EP03: a rust-dark freight hub squatting in the
        /// Rust Collective debris belt — a boxy hub with cargo arms and a single docking spine, no
        /// chrome polish. Static (no orbit): derelict-station country. Returns the docking-spine
        /// GameObject for landable wiring. No colliders (visual only).
        /// </summary>
        private static GameObject BuildGrimdockStation(Transform universe, Vector3 position)
        {
            var root = new GameObject("Grimdock Station");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var rust = new Color(0.45f, 0.32f, 0.22f);
            var darkRust = new Color(0.3f, 0.22f, 0.16f);

            GameObject Part(string n, PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = n;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p;
                go.transform.localScale = s;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                TintShared(go.GetComponent<Renderer>(), c);
                return go;
            }

            // Central freight hub: a heavy box, not a sphere — this is a working dock, not a ring station.
            Part("Hub", PrimitiveType.Cube, Vector3.zero, new Vector3(14f, 8f, 14f), rust);

            // Stacked cargo decks above and below the hub.
            Part("DeckTop", PrimitiveType.Cube, new Vector3(0f, 6f, 0f), new Vector3(10f, 3f, 10f), darkRust);
            Part("DeckBottom", PrimitiveType.Cube, new Vector3(0f, -6f, 0f), new Vector3(11f, 3f, 11f), darkRust);

            // Four cargo arms with container clusters on the ends.
            var armScale = new Vector3(22f, 2f, 2f);
            Part("ArmE", PrimitiveType.Cube, new Vector3(14f, 0f, 0f), armScale, darkRust);
            Part("ArmW", PrimitiveType.Cube, new Vector3(-14f, 0f, 0f), armScale, darkRust);
            Part("ArmN", PrimitiveType.Cube, new Vector3(0f, 0f, 14f), new Vector3(2f, 2f, 22f), darkRust);
            Part("ArmS", PrimitiveType.Cube, new Vector3(0f, 0f, -14f), new Vector3(2f, 2f, 22f), darkRust);
            Part("ContainersE", PrimitiveType.Cube, new Vector3(24f, 0f, 0f), new Vector3(5f, 4f, 6f), rust);
            Part("ContainersW", PrimitiveType.Cube, new Vector3(-24f, 0f, 0f), new Vector3(5f, 4f, 6f), rust);
            Part("ContainersN", PrimitiveType.Cube, new Vector3(0f, 0f, 24f), new Vector3(6f, 4f, 5f), rust);

            // Docking spine: the long landing target protruding south, with a green guide strip.
            var dockingSpine = Part("DockingSpine", PrimitiveType.Cube, new Vector3(0f, 0f, -28f), new Vector3(4f, 4f, 16f), rust);
            var strip = Part("DockStrip", PrimitiveType.Cube, new Vector3(0f, 2.2f, -28f), new Vector3(0.5f, 0.15f, 14f), Color.green);
            strip.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.2f, 1f, 0.4f));

            // Antenna masts on the top deck.
            var faceZ = Quaternion.Euler(90f, 0f, 0f);
            Part("Antenna1", PrimitiveType.Cylinder, new Vector3(2f, 9f, 2f), new Vector3(0.3f, 2.5f, 0.3f), darkRust, faceZ);
            Part("Antenna2", PrimitiveType.Cylinder, new Vector3(-3f, 8.5f, -1f), new Vector3(0.3f, 2f, 0.3f), darkRust, faceZ);

            // Warm work light over the docking spine.
            var dockLight = new GameObject("DockingLight");
            dockLight.transform.SetParent(root.transform, false);
            dockLight.transform.localPosition = new Vector3(0f, 4f, -32f);
            var dockLightComp = dockLight.AddComponent<Light>();
            dockLightComp.type = LightType.Point;
            dockLightComp.color = new Color(1f, 0.7f, 0.3f);
            dockLightComp.intensity = 1.5f;
            dockLightComp.range = 25f;

            return dockingSpine;  // Return docking spine for landable target
        }

        /// <summary>
        /// Builds the Archive Station in the sun's corona: a greybox ring hub with an amber tint,
        /// dockable for EP04 content. Named "CoronaArchive" and returns the hub for the landable target.
        /// </summary>
        private static GameObject BuildArchiveStation(Transform universe, Vector3 position)
        {
            var root = new GameObject("CoronaArchive");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var amber = new Color(0.65f, 0.50f, 0.25f);
            var darkAmber = new Color(0.45f, 0.35f, 0.18f);

            GameObject Part(string n, PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = n;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p;
                go.transform.localScale = s;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                TintShared(go.GetComponent<Renderer>(), c);
                return go;
            }

            // Central ring hub: torus-like structure (approximate with stacked cylinders)
            var faceY = Quaternion.Euler(90f, 0f, 0f);
            Part("RingHub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(16f, 2f, 16f), amber, faceY);

            // Spokes extending inward/outward from the ring
            Part("Spoke1", PrimitiveType.Cube, new Vector3(10f, 0f, 0f), new Vector3(20f, 1.5f, 1.5f), darkAmber);
            Part("Spoke2", PrimitiveType.Cube, new Vector3(-10f, 0f, 0f), new Vector3(20f, 1.5f, 1.5f), darkAmber);
            Part("Spoke3", PrimitiveType.Cube, new Vector3(0f, 0f, 10f), new Vector3(1.5f, 1.5f, 20f), darkAmber);
            Part("Spoke4", PrimitiveType.Cube, new Vector3(0f, 0f, -10f), new Vector3(1.5f, 1.5f, 20f), darkAmber);

            // External docking nodes on the rim
            Part("DockNode1", PrimitiveType.Cube, new Vector3(12f, 2f, 0f), new Vector3(3f, 3f, 3f), amber);
            Part("DockNode2", PrimitiveType.Cube, new Vector3(-12f, 2f, 0f), new Vector3(3f, 3f, 3f), amber);
            Part("DockNode3", PrimitiveType.Cube, new Vector3(0f, 2f, 12f), new Vector3(3f, 3f, 3f), amber);

            // Ambient archive light: a cool archive glow
            var archiveLight = new GameObject("ArchiveLight");
            archiveLight.transform.SetParent(root.transform, false);
            archiveLight.transform.localPosition = new Vector3(0f, 2f, 0f);
            var archiveLightComp = archiveLight.AddComponent<Light>();
            archiveLightComp.type = LightType.Point;
            archiveLightComp.color = new Color(0.6f, 0.75f, 1f);
            archiveLightComp.intensity = 1.2f;
            archiveLightComp.range = 30f;

            return root;  // Return the hub for landable target
        }

        /// <summary>
        /// Builds the Rust Collective station for EP05: stacked decommissioned warship hulks parked
        /// in the existing debris belt, orbiting the dying neutron star. Rust palette matches
        /// Grimdock; an orange "accretion glow" light sells the dying-star radiation bleed.
        /// Named "RustCollective" and returns the root for the landable/guard target.
        /// </summary>
        private static GameObject BuildRustCollectiveStation(Transform universe, Vector3 position)
        {
            var root = new GameObject("RustCollective");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var rust = new Color(0.55f, 0.35f, 0.22f);
            var darkRust = new Color(0.38f, 0.24f, 0.16f);

            GameObject Part(string n, PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = n;
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p;
                go.transform.localScale = s;
                if (rot.HasValue) go.transform.localRotation = rot.Value;
                TintShared(go.GetComponent<Renderer>(), c);
                return go;
            }

            // Stacked warship hulks: long offset boxes piled into a labyrinthine mass.
            Part("Hulk1", PrimitiveType.Cube, Vector3.zero, new Vector3(30f, 5f, 8f), darkRust);
            Part("Hulk2", PrimitiveType.Cube, new Vector3(4f, 5.5f, 2f), new Vector3(22f, 4f, 7f), rust,
                Quaternion.Euler(0f, 18f, 3f));
            Part("Hulk3", PrimitiveType.Cube, new Vector3(-6f, -5f, -3f), new Vector3(26f, 4.5f, 6f), rust,
                Quaternion.Euler(0f, -25f, -4f));
            Part("Hulk4", PrimitiveType.Cube, new Vector3(2f, 10.5f, -4f), new Vector3(16f, 3.5f, 6f), darkRust,
                Quaternion.Euler(2f, 40f, 0f));
            Part("Hulk5", PrimitiveType.Cube, new Vector3(-3f, 0f, 8f), new Vector3(10f, 8f, 10f), rust);

            // Broken masts jutting from the pile.
            var faceZ = Quaternion.Euler(90f, 0f, 0f);
            Part("Mast1", PrimitiveType.Cylinder, new Vector3(6f, 14f, 0f), new Vector3(0.4f, 3f, 0.4f), darkRust, faceZ);
            Part("Mast2", PrimitiveType.Cylinder, new Vector3(-9f, 8f, 4f), new Vector3(0.35f, 2.2f, 0.35f), darkRust,
                Quaternion.Euler(70f, 20f, 0f));

            // Docking spine: the landing target protruding south, with a green guide strip.
            Part("DockingSpine", PrimitiveType.Cube, new Vector3(0f, 0f, -22f), new Vector3(4f, 4f, 16f), rust);
            var strip = Part("DockStrip", PrimitiveType.Cube, new Vector3(0f, 2.2f, -22f), new Vector3(0.5f, 0.15f, 14f), Color.green);
            strip.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.2f, 1f, 0.4f));

            // Accretion glow: the dying neutron star's radiation bleed washing the hulks orange.
            var glow = new GameObject("AccretionGlow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, -12f, 6f);
            var glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.55f, 0.25f);
            glowLight.intensity = 2f;
            glowLight.range = 60f;

            return root;  // Return the root for landable/guard target
        }

        /// <summary>
        /// The Galaxy 2 Jump Gate: a broken ember-lit ring of arc segments with a glowing core —
        /// the landable that carries the player from Galaxy 1 to the Galaxy 2 hub once EP08 is
        /// complete. Visual only (collider-free); the LandingApproach entry does the actual jump.
        /// </summary>
        private static GameObject BuildGalaxy2JumpGate(Transform universe, Vector3 localPos)
        {
            var root = new GameObject("Galaxy 2 Jump Gate");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = localPos;

            var darkSteel = new Color(0.25f, 0.24f, 0.27f);
            var ember = new Color(1f, 0.5f, 0.2f);

            // Ring of 12 tangent segments, ~26u across, standing vertically so it reads as a portal.
            int segments = 12;
            float ringRadius = 13f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f);
                var seg = AddVisualTinted(root.transform, $"GateSegment{i}", pos,
                    new Vector3(5f, 1.6f, 1.6f), PrimitiveType.Cube, darkSteel);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                // Every other segment carries an ember glow strip on its inner face.
                if (i % 2 == 0)
                    AddUnlitVisual(seg.transform, "GateGlow", new Vector3(0f, -0.55f, 0f),
                        new Vector3(0.8f, 0.1f, 0.8f), PrimitiveType.Cube, ember);
            }

            // Faint glowing core disc filling the ring mouth.
            var core = AddUnlitVisual(root.transform, "GateCore", Vector3.zero,
                new Vector3(20f, 20f, 0.2f), PrimitiveType.Sphere, new Color(0.9f, 0.45f, 0.2f));
            core.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(ember, 0.18f);

            // Warm beacon so the gate reads from across the system.
            var beaconGo = new GameObject("GateBeacon");
            beaconGo.transform.SetParent(root.transform, false);
            var beacon = beaconGo.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.color = ember;
            beacon.intensity = 2f;
            beacon.range = 60f;
            beacon.shadows = LightShadows.None;

            return root;
        }

        /// <summary>
        /// Scatters <paramref name="count"/> asteroids in a flat annulus (ring) around
        /// <paramref name="center"/> in the XZ-plane: each sits at a random angle, a radius in
        /// [ringRadius - halfWidth, ringRadius + halfWidth], and a y in [-thickness, +thickness].
        /// Uses a fixed seed so rebuilds are deterministic. Delegates each rock to the existing
        /// <see cref="BuildAsteroid"/> so the asteroids stay destructible contact hazards.
        /// </summary>
        private static void BuildAsteroidRing(Transform universe, Vector3 center, float ringRadius,
            float halfWidth, float thickness, int count, float scaleMul = 1f)
        {
            var rng = new System.Random(20260602);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = ringRadius + ((float)rng.NextDouble() * 2f - 1f) * halfWidth;
                float y = ((float)rng.NextDouble() * 2f - 1f) * thickness;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                float scale = ((float)rng.NextDouble() * 1.6f + 0.6f) * scaleMul; // (0.6 .. 2.2) * mul
                BuildAsteroid(universe, pos, scale);
            }
        }

        /// <summary>
        /// Builds a single-draw-call star dome: <paramref name="count"/> small camera-facing quads
        /// (two triangles each) spread uniformly over a sphere of <paramref name="radius"/>, rendered
        /// with an unlit near-white material. Each star is a real triangle quad rather than a
        /// <see cref="MeshTopology.Points"/> vertex — point topology renders invisibly on Quest/URP
        /// (1px or unsupported), which is why the dome looked empty in-headset. Because the player
        /// sits at the universe origin, every quad faces inward (toward the origin) so it always
        /// presents its face to the eyes. A large manual bounds prevents frustum-culling when the
        /// camera is inside the dome. Deterministic via a fixed seed; no collider. ~1500 stars =
        /// ~3k triangles in one draw call, comfortable on Quest.
        /// </summary>
        private static void BuildStarfield(Transform parent, float radius, int count)
        {
            var rng = new System.Random(20260603);
            var vertices = new Vector3[count * 4];
            var indices = new int[count * 6];
            var colors = new Color[count * 4];
            float baseHalf = radius * 0.004f; // star quad half-size scales with the dome (~const angular size)

            for (int i = 0; i < count; i++)
            {
                // Uniform-on-sphere: a normalized direction from three uniform samples is good enough
                // for a star dome; reject the degenerate near-zero vector.
                Vector3 dir = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
                dir.Normalize();
                Vector3 center = dir * radius;

                // Build an inward-facing quad: pick any up not parallel to dir, derive a right/up basis
                // in the plane perpendicular to dir.
                Vector3 up = Mathf.Abs(dir.y) > 0.99f ? Vector3.right : Vector3.up;
                Vector3 right = Vector3.Cross(up, dir).normalized;
                up = Vector3.Cross(dir, right).normalized;

                float s = baseHalf * (0.6f + (float)rng.NextDouble() * 0.8f); // slight size variation
                int v = i * 4;
                vertices[v] = center - right * s - up * s;
                vertices[v + 1] = center + right * s - up * s;
                vertices[v + 2] = center + right * s + up * s;
                vertices[v + 3] = center - right * s + up * s;

                float b = 0.6f + (float)rng.NextDouble() * 0.4f; // slight per-star brightness variation
                var col = new Color(b, b, b, 1f);
                colors[v] = col; colors[v + 1] = col; colors[v + 2] = col; colors[v + 3] = col;

                int t = i * 6;
                // Wind so the face points inward (toward the origin / the player).
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v; indices[t + 4] = v + 3; indices[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "StarfieldMesh" };
            if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetTriangles(indices, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2.2f));

            // Parent = null keeps the dome WORLD-static at the origin: stars sit at infinity and must
            // not parallax, and the rig never moves, so a scene-root dome centred on the rig is always
            // around the player no matter how far they fly within the (moving) universe.
            var go = new GameObject("Starfield");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.95f, 0.95f, 1f));
        }

        /// <summary>
        /// One-click rebuild of every Galaxy 1 scene. Builds the four on-foot zones first, then the
        /// SPACE scene LAST so the space scene is the one left open in the editor and its
        /// EnsureScenesInBuild call registers everything. Each Build* re-runs NewScene(...Single) and
        /// saves before returning, so the calls are safe to chain.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build All Galaxy 1 Scenes", priority = 59)]
        public static void BuildAllGalaxy1Scenes()
        {
            BuildJungleZone();
            BuildLavaZone();
            BuildDesertZone();
            BuildWaterZone();
            BuildFrostZone();
            BuildCorsairInterior();
            BuildEp04JungleMoon();
            BuildEp04ArchiveRing();
            BuildEp04Ledger();
            BuildEp05MarketTier();
            BuildEp05PressureLocks();
            BuildEp05Rotunda();
            BuildEp05CommandHub();
            BuildGalaxy1Scene();
            Debug.Log("[Space Samurai] All Galaxy 1 scenes built. ALSO re-run " +
                      "Tools/Space Samurai/Build Phase 6 Boot Scene so the menu loop picks up " +
                      "Galaxy 1 as the flight scene.");
        }

        /// <summary>
        /// Builds the Galaxy 1 SPACE flight scene: a seated dogfighting rig at the universe origin
        /// in a full solar system of the seven canon story worlds (mainstory galaxy1, EP01-08) on
        /// slow <see cref="PlanetOrbit"/>s around a big light-emitting sun, the Rust Collective
        /// debris ring, and the Corsair — Kessler's big dockable ship. The opening flight is calm:
        /// the <see cref="SpaceEncounterManager"/> is gated on Galaxy1Progress.FirstPlanetDeparted,
        /// so hostiles only start warping in after the player has landed on Aquilane (the EP01
        /// water-mining world) and returned to space. The cockpit arrow prioritizes live enemy
        /// ships over the objective via <see cref="ObjectiveArrowController"/>.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Galaxy 1 Space Scene", priority = 60)]
        public static void BuildGalaxy1Scene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Space is a black void: kill fog, drop ambient to a faint cool fill so unlit faces
            // aren't pure black, and swap the default gradient sky for a solid-black skybox.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig: stationary at the origin, NO locomotion (no walking/gravity). The
            // universe moves around it; the rig never moves.
            var rig = BuildRig(refs, addLocomotion: false);

            // Far clip must cover the WHOLE system: the farthest planet (Frosthold) sits ~3200 from
            // the origin and the star dome at 5000, so 6000 keeps everything visible (the prior
            // space-scene lesson: a too-tight far clip reads as an empty black universe in-headset).
            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            // Player ship Health so enemy bolts can hurt the samurai's craft, and a HUD/event bridge
            // can react to PlayerShipDamaged. The rig already has a Health (added in BuildRig); ensure it.
            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            // A hull collider at the origin so enemy bolts (which converge on the cockpit at the origin
            // in the moving-universe frame) actually strike SOMETHING and damage the ship Health. It's a
            // child of the rig root, so Projectile.TryHit's GetComponentInParent<IDamageable>() resolves
            // to the rig's Health.
            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false; // solid so the bolt's SphereCast (which ignores triggers) hits it

            var cockpit = new GameObject("Cockpit").transform;
            BuildCockpit(cockpit);

            // Long-press right-A re-aligns the cockpit with the current head pose so the ship
            // front follows where the player is looking when the seated rig drifts. Dedicated
            // "Recenter Cockpit" action: the shared "Recenter" (right-B) is taken by the on-foot hack.
            var recenter = cockpit.gameObject.AddComponent<CockpitRecenter>();
            var rcSo = new SerializedObject(recenter);
            SetObjectRef(rcSo, "recenterAction", FindRef(refs, "Right Hand", "Recenter Cockpit"));
            SetObjectRef(rcSo, "head", vrRig != null ? vrRig.Head : null);
            SetObjectRef(rcSo, "cockpit", cockpit);
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            // Wrap the player's chosen exterior hull around the cockpit at runtime. The four hull
            // prefabs are loaded here by their published paths and wired (index-aligned with
            // ShipSelection.SelectedHullIndex) into a ShipHullSelector on the cockpit; it instantiates
            // the picked one on Start. Missing prefabs stay null so indices never shift.
            var hullSelector = cockpit.gameObject.AddComponent<Ronin7.Ship.ShipHullSelector>();
            var hullPaths = ArtPrefabBuilder.ShipHullPrefabPaths;
            var hullObjs = new GameObject[hullPaths.Length];
            int hullsFound = 0;
            for (int i = 0; i < hullPaths.Length; i++)
            {
                hullObjs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(hullPaths[i]);
                if (hullObjs[i] != null) hullsFound++;
            }

            var hsSo = new SerializedObject(hullSelector);
            var arr = hsSo.FindProperty("hullPrefabs");
            arr.arraySize = hullObjs.Length;
            for (int i = 0; i < hullObjs.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = hullObjs[i];
            hsSo.ApplyModifiedPropertiesWithoutUndo();

            if (hullsFound == hullPaths.Length)
                Debug.Log($"[Space Samurai] Wired {hullsFound}/{hullPaths.Length} ship hull prefabs onto the cockpit.");
            else
                Debug.Log($"[Space Samurai] Wired {hullsFound}/{hullPaths.Length} ship hull prefabs onto the cockpit. " +
                          "Some are missing — run Tools/Space Samurai/Art/Build Ship Hull Variants, then rebuild this scene.");

            // Enclose the open cockpit in a solid cabin (front windshield only) and seat Kessler in the
            // back-right as a co-pilot, with an auto-playing destination-planet briefing.
            BuildGalaxy1Cabin(cockpit);

            // The moving world: a big light-emitting sun with the seven canon worlds on slow orbits.
            // The player starts at the universe origin looking +Z toward the sun. Planets are BIG
            // (scale 120 → radius 60), the sun bigger still, and spacing is system-scale: orbits run
            // 850-2500 units out, so worlds read as destinations rather than scenery props.
            var universe = new GameObject("Universe").transform;
            Vector3 sunCenter = new Vector3(0f, 0f, 1500f);
            var sunVisual = BuildGalaxySun(universe, light, sunCenter, 400f);

            // Keep the key light shining from the sun at runtime (a baked direction drifts off the
            // sun as the universe rotates under the stationary rig).
            var aimer = lightGo.AddComponent<SunLightAimer>();
            var aimerSo = new SerializedObject(aimer);
            SetObjectRef(aimerSo, "sunLight", light);
            SetObjectRef(aimerSo, "sunVisual", sunVisual.transform);
            aimerSo.ApplyModifiedPropertiesWithoutUndo();

            // Seven canon worlds (mainstory/galaxy1, EP01-08). Each authored position is its orbit's
            // t=0 pose — OrbitPos(center, r, startAngle) matches what PlanetOrbit.Awake applies.

            // Aquilane — the EP01 water-mining world, silver seas under perpetual cloud. FIRST objective.
            Vector3 aquilanePos = OrbitPos(sunCenter, 850f, 245f, 10f);
            var aquilane = BuildCanonPlanet(universe, "Planet Aquilane (Water-Mining)",
                aquilanePos, 120f, new Color(0.55f, 0.62f, 0.70f), "Ice");
            AddSurfacePatches(aquilane, 4, new Color(0.42f, 0.50f, 0.58f), 50000001);
            AddCloudShell(aquilane, Color.white, 0.18f);
            AddPlanetOrbit(aquilane, sunVisual.transform, 850f, 0.25f, 245f, 10f);

            // Velorum — the frozen water-moon (EP02), orbiting Aquilane.
            var velorum = BuildCanonPlanet(universe, "Moon Velorum (Frozen)",
                OrbitPos(aquilanePos, 180f, 30f), 45f, new Color(0.75f, 0.85f, 0.95f), "Ice");
            AddPolarCaps(velorum, new Color(0.92f, 0.96f, 1f));
            AddPlanetOrbit(velorum, aquilane != null ? aquilane.transform : sunVisual.transform, 180f, 1.0f, 30f);

            // Velorum Station — chrome orbital ring near the frozen moon, dockable for EP02 content.
            var velorumStation = BuildVelorumStation(universe, velorum);

            // The jungle moon of the Veiled Reaches (EP04).
            var jungleMoon = BuildCanonPlanet(universe, "Jungle Moon",
                OrbitPos(sunCenter, 1300f, 10f, -20f), 120f, new Color(0.25f, 0.55f, 0.35f), "Lush");
            AddSurfacePatches(jungleMoon, 5, new Color(0.14f, 0.40f, 0.20f), 50000002);
            AddPlanetOrbit(jungleMoon, sunVisual.transform, 1300f, 0.18f, 10f, -20f);

            // Frosthold — the glacial plague-colony world (EP06).
            var frosthold = BuildCanonPlanet(universe, "Planet Frosthold (Glacial)",
                OrbitPos(sunCenter, 1800f, 120f, 30f), 120f, new Color(0.85f, 0.90f, 1.0f), "Ice");
            AddPolarCaps(frosthold, Color.white);
            AddSurfacePatches(frosthold, 4, new Color(0.70f, 0.78f, 0.92f), 50000003);
            AddPlanetOrbit(frosthold, sunVisual.transform, 1800f, 0.12f, 120f, 30f);

            // Blackveil — the dead industrial moon of the shipbreaker yards (EP07).
            var blackveil = BuildCanonPlanet(universe, "Moon Blackveil (Industrial)",
                OrbitPos(sunCenter, 2100f, 200f, -15f), 120f, new Color(0.35f, 0.35f, 0.38f), "Rocky");
            AddSurfacePatches(blackveil, 4, new Color(0.24f, 0.24f, 0.27f), 50000004);
            AddCityLights(blackveil, 10, new Color(1f, 0.55f, 0.15f), 50000005);
            AddPlanetOrbit(blackveil, sunVisual.transform, 2100f, 0.10f, 200f, -15f);

            // Vel Keth — the red iron-canyon world hiding Apex Station (EP08).
            var velKeth = BuildCanonPlanet(universe, "Planet Vel Keth (Canyon)",
                OrbitPos(sunCenter, 2500f, 320f, 20f), 120f, new Color(0.62f, 0.25f, 0.15f), "Rocky");
            AddSurfacePatches(velKeth, 6, new Color(0.42f, 0.16f, 0.10f), 50000006);
            AddPlanetOrbit(velKeth, sunVisual.transform, 2500f, 0.08f, 320f, 20f);

            // The Rust Collective (EP05): a dense debris belt of asteroids and derelict warship
            // hulks between the jungle-moon and Frosthold orbits.
            BuildAsteroidRing(universe, sunCenter, 1550f, 120f, 40f, 80, scaleMul: 8f);
            BuildWreckHulks(universe, sunCenter, 1550f, 120f, 12);

            // Grimdock Station — the EP03 rust-freight hub, parked in the debris belt slightly above
            // the ring plane so the docking approach stays clear of asteroids.
            var grimdock = BuildGrimdockStation(universe,
                sunCenter + new Vector3(Mathf.Cos(70f * Mathf.Deg2Rad) * 1550f, 60f, Mathf.Sin(70f * Mathf.Deg2Rad) * 1550f));

            // Archive Station — the EP04 amber ring station parked near the sun's corona. The sun sits
            // at z=1500 with radius 400, so the station at (250, 30, 950) is about 605 units from the
            // sun center, well clear of the 450-unit exclusion radius.
            var archiveStation = BuildArchiveStation(universe, new Vector3(250f, 30f, 950f));

            // Rust Collective Station — the EP05 stacked-hulk salvage station, parked in the debris
            // belt well away from Grimdock (200° vs 70°) and slightly above the ring plane.
            var rustCollective = BuildRustCollectiveStation(universe,
                sunCenter + new Vector3(Mathf.Cos(200f * Mathf.Deg2Rad) * 1550f, 70f, Mathf.Sin(200f * Mathf.Deg2Rad) * 1550f));

            // Star dome: WORLD-static at the scene root (not under the universe) so the player can
            // never fly out of it — stars sit at infinity and must not parallax.
            BuildStarfield(null, 5000f, 2200);

            // The Corsair — Kessler's big dockable ship, parked on the spawn→Aquilane route, well
            // clear of every orbit. Dock (land) on it to walk its interior.
            var corsair = BuildCorsairExterior(universe, new Vector3(250f, 20f, 350f), 6f);

            // Galaxy 2 Jump Gate — unlocked by finishing EP08 (Nebula Edge). "Landing" on it jumps
            // to the Galaxy 2 hub. Parked opposite the Corsair so the two docks never read as one.
            var jumpGate = BuildGalaxy2JumpGate(universe, new Vector3(-350f, 30f, 250f));

            // Hazard manager: ONE scene-wide pass that makes the belt deal collision damage to BOTH the
            // player hull (at the origin) and enemy ships, with brief per-victim cooldowns.
            var hazardGo = new GameObject("Asteroid Hazard");
            var hazard = hazardGo.AddComponent<AsteroidHazard>();
            hazard.Configure(rig.GetComponent<Health>(), hullCol.radius);

            // Flight controller, wired identically to the Phase 7 scene (universe + sticks).
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var shipSo = new SerializedObject(shipCtrl);
            SetObjectRef(shipSo, "universe", universe);
            SetObjectRef(shipSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(shipSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            // System-scale speeds: orbits run out to 2500 units, so the default 35 u/s would make
            // the outer worlds a multi-minute slog. The comfort vignette scales with acceleration.
            shipSo.FindProperty("maxSpeed").floatValue = 90f;
            shipSo.FindProperty("acceleration").floatValue = 20f;
            shipSo.ApplyModifiedPropertiesWithoutUndo();

            // Shared bolt pool (player + all enemies draw from this one bounded pool).
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns mounted on the cockpit. Twin muzzles flank the canopy and point forward (+Z),
            // i.e. into real/camera space — bolts fly out and hit the rendered enemy ships directly.
            var gunsGo = new GameObject("Ship Guns");
            gunsGo.transform.SetParent(cockpit, false);
            var muzzleL = new GameObject("Muzzle L").transform;
            muzzleL.SetParent(gunsGo.transform, false);
            muzzleL.localPosition = new Vector3(-0.5f, 1.0f, 0.8f);
            var muzzleR = new GameObject("Muzzle R").transform;
            muzzleR.SetParent(gunsGo.transform, false);
            muzzleR.localPosition = new Vector3(0.5f, 1.0f, 0.8f);

            var guns = gunsGo.AddComponent<ShipWeaponController>();
            var gunsSo = new SerializedObject(guns);
            SetObjectRef(gunsSo, "pool", pool);
            SetObjectRef(gunsSo, "definition", shipWeapon);
            SetObjectRef(gunsSo, "fireAction", FindRef(refs, "Right Hand", "Activate"));
            SetObjectRef(gunsSo, "ownerRoot", rig); // player ship root → bolts skip our own hull
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            BuildCockpitCrosshair(cockpit, guns);

            // Windshield waypoint arrow. The marker renders/points; the ObjectiveArrowController on
            // the same GO decides WHAT it points at: nearest live enemy ship first, mission objective
            // (Aquilane) once the sky is clear.
            var arrowGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowGo.name = "Planet Waypoint Arrow";
            Object.DestroyImmediate(arrowGo.GetComponent<Collider>());
            arrowGo.transform.SetParent(cockpit, false);
            arrowGo.transform.localPosition = new Vector3(0f, 1.35f, 1.2f);
            arrowGo.transform.localScale = new Vector3(0.06f, 0.06f, 0.25f);  // a small forward-pointing dart
            TintShared(arrowGo.GetComponent<Renderer>(), new Color(0.3f, 0.8f, 1f));
            var marker = arrowGo.AddComponent<Ronin7.Ship.PlanetTargetMarker>();
            var mSo = new SerializedObject(marker);
            SetObjectRef(mSo, "arrow", arrowGo.transform);
            SetObjectRef(mSo, "target", aquilane != null ? aquilane.transform : null);
            SetObjectRef(mSo, "universe", universe);
            SetObjectRef(mSo, "arrowRenderer", arrowGo.GetComponent<Renderer>());
            // System-scale fade: visible across most of the system, hidden on final approach.
            mSo.FindProperty("fadeStartRange").floatValue = 3000f;
            mSo.FindProperty("arriveRange").floatValue = 150f;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            var arrowCtrl = arrowGo.AddComponent<Ronin7.Ship.ObjectiveArrowController>();
            var acSo = new SerializedObject(arrowCtrl);
            SetObjectRef(acSo, "marker", marker);
            SetObjectRef(acSo, "defaultObjective", aquilane != null ? aquilane.transform : null);
            acSo.ApplyModifiedPropertiesWithoutUndo();

            // Campaign Objective Selector: picks mission objective based on milestones.
            // Before EP01: target Aquilane. After EP01: target Velorum Station (EP02).
            var selectorGo = new GameObject("Campaign Objective Selector");
            selectorGo.transform.SetParent(arrowGo.transform, false);
            var selector = selectorGo.AddComponent<CampaignObjectiveSelector>();
            var selSo = new SerializedObject(selector);
            var entriesProp = selSo.FindProperty("entries");
            entriesProp.arraySize = 6;
            // Entry 0: Aquilane (always available)
            var entry0 = entriesProp.GetArrayElementAtIndex(0);
            entry0.FindPropertyRelative("requiredCompletedScene").stringValue = "";
            entry0.FindPropertyRelative("objective").objectReferenceValue = aquilane != null ? aquilane.transform : null;
            // Entry 1: Velorum Station (after EP01)
            var entry1 = entriesProp.GetArrayElementAtIndex(1);
            entry1.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy1Ep01PlanetSceneName;
            entry1.FindPropertyRelative("objective").objectReferenceValue = velorumStation != null ? velorumStation.transform : null;
            // Entry 2: Grimdock Station (after EP02)
            var entry2 = entriesProp.GetArrayElementAtIndex(2);
            entry2.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy1Ep02DockingSceneName;
            entry2.FindPropertyRelative("objective").objectReferenceValue = grimdock != null ? grimdock.transform : null;
            // Entry 3: Jungle Moon (after EP03)
            var entry3 = entriesProp.GetArrayElementAtIndex(3);
            entry3.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy1Ep03HaulerSceneName;
            entry3.FindPropertyRelative("objective").objectReferenceValue = jungleMoon != null ? jungleMoon.transform : null;
            // Entry 4: Corona Archive (after EP04 jungle moon)
            var entry4 = entriesProp.GetArrayElementAtIndex(4);
            entry4.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy1Ep04JungleMoonSceneName;
            entry4.FindPropertyRelative("objective").objectReferenceValue = archiveStation != null ? archiveStation.transform : null;
            // Entry 5: Rust Collective (after EP04 archive ring)
            var entry5 = entriesProp.GetArrayElementAtIndex(5);
            entry5.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy1Ep04ArchiveRingSceneName;
            entry5.FindPropertyRelative("objective").objectReferenceValue = rustCollective != null ? rustCollective.transform : null;
            SetObjectRef(selSo, "arrow", arrowCtrl);
            selSo.ApplyModifiedPropertiesWithoutUndo();

            // Combat: ONE wave with all enemies at once, GATED — silent until the player has landed
            // on Aquilane and returned to space (Galaxy1Progress.FirstPlanetDeparted). Once cleared,
            // no respawns this scene-load, so the player is free to land. 2 enemies, single spawn,
            // no waves/scaling.
            var encounterGo = new GameObject("Space Encounters");
            var encounter = encounterGo.AddComponent<SpaceEncounterManager>();
            var encSo = new SerializedObject(encounter);
            SetObjectRef(encSo, "player", shipCtrl);
            SetObjectRef(encSo, "universe", universe);
            SetObjectRef(encSo, "pool", pool);
            SetObjectRef(encSo, "definition", enemyShipDef);
            encSo.FindProperty("requireFirstPlanetDeparture").boolValue = true;
            encSo.FindProperty("waveCount").intValue = 1;            // single wave — no respawns
            encSo.FindProperty("enemiesPerWave").intValue = 2;
            encSo.FindProperty("enemiesAddedPerWave").intValue = 0;
            encSo.FindProperty("enemiesAddedPerGalaxy").intValue = 0;
            encSo.FindProperty("maxEnemiesPerWave").intValue = 2;
            encSo.FindProperty("interWaveDelay").floatValue = 6f;
            encSo.FindProperty("initialDelay").floatValue = 5f;
            encSo.FindProperty("travelBetweenWaves").floatValue = 400f;
            encSo.FindProperty("spawnDistance").floatValue = 260f;
            encSo.ApplyModifiedPropertiesWithoutUndo();

            // On-clear "head to the next objective" line. With the guard encounters disabled, only the
            // ambient SpaceEncounterManager publishes SpaceEncounterCleared, so this fires after the
            // 2-enemy fight.
            var clearedDialogue = BuildDialoguePlayer("Dialogue_Galaxy1ObjectiveCleared", Vector3.zero, "space_objective_cleared");
            var clearedDialogueGo = clearedDialogue.gameObject;
            clearedDialogueGo.transform.SetParent(cockpit, false);
            clearedDialogueGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            clearedDialogueGo.transform.localRotation = Quaternion.identity;

            var clearedTextT = clearedDialogueGo.transform.Find("Text");
            if (clearedTextT != null)
            {
                clearedTextT.localPosition = new Vector3(0f, 0f, 0.02f);
                clearedTextT.localScale = Vector3.one * 0.0045f;
            }
            var clearedPanelT = clearedDialogueGo.transform.Find("Panel");
            if (clearedPanelT != null)
            {
                clearedPanelT.localPosition = Vector3.zero;
                var clearedBg = clearedPanelT.Find("Background");
                if (clearedBg != null) clearedBg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var clearedDpSo = new SerializedObject(clearedDialogue);
            clearedDpSo.FindProperty("playOnStart").boolValue = false;
            clearedDpSo.ApplyModifiedPropertiesWithoutUndo();

            var clearedActivatorGo = new GameObject("Objective Cleared Activator");
            var clearedActivator = clearedActivatorGo.AddComponent<EncounterClearedActivator>();
            var caSo = new SerializedObject(clearedActivator);
            caSo.FindProperty("playOnCleared").objectReferenceValue = clearedDialogue;
            caSo.ApplyModifiedPropertiesWithoutUndo();

            // Pursuit Encounter: enemies chase the player after landing on EP01, gated by EP02 docking.
            var pursuitGo = new GameObject("Pursuit Encounter");
            var pursuit = pursuitGo.AddComponent<GuardEncounter>();
            var pursuitSo = new SerializedObject(pursuit);
            SetObjectRef(pursuitSo, "player", shipCtrl);
            SetObjectRef(pursuitSo, "universe", universe);
            SetObjectRef(pursuitSo, "pool", pool);
            SetObjectRef(pursuitSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            pursuitSo.FindProperty("shipCount").intValue = 3;
            pursuitSo.FindProperty("spawnRadius").floatValue = 260f;
            pursuitSo.FindProperty("initialDelay").floatValue = 6f;
            pursuitSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep01PlanetSceneName;
            pursuitSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy1Ep02DockingSceneName;
            pursuitSo.FindProperty("clearedFlag").stringValue = "ep02_pursuit_cleared";
            pursuitSo.ApplyModifiedPropertiesWithoutUndo();
            pursuitGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Velorum Blockade: enemies guard the station, gated by EP02 docking.
            var blockadeGo = new GameObject("Velorum Blockade");
            var blockade = blockadeGo.AddComponent<GuardEncounter>();
            var blockadeSo = new SerializedObject(blockade);
            SetObjectRef(blockadeSo, "player", shipCtrl);
            SetObjectRef(blockadeSo, "universe", universe);
            SetObjectRef(blockadeSo, "pool", pool);
            SetObjectRef(blockadeSo, "definition", enemyShipDef);
            // Blockade mode: guardTarget = station, spawn around it, activate when player approaches
            SetObjectRef(blockadeSo, "guardTarget", velorumStation != null ? velorumStation.transform : null);
            blockadeSo.FindProperty("shipCount").intValue = 3;
            blockadeSo.FindProperty("spawnRadius").floatValue = 120f;
            blockadeSo.FindProperty("activateRange").floatValue = 400f;
            blockadeSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep01PlanetSceneName;
            blockadeSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy1Ep02DockingSceneName;
            blockadeSo.FindProperty("clearedFlag").stringValue = "ep02_blockade_cleared";
            blockadeSo.ApplyModifiedPropertiesWithoutUndo();
            blockadeGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Hollow Kings Pursuit: corvette hunters chase the player after EP02, gated by EP03 docking.
            var hkPursuitGo = new GameObject("Hollow Kings Pursuit");
            var hkPursuit = hkPursuitGo.AddComponent<GuardEncounter>();
            var hkPursuitSo = new SerializedObject(hkPursuit);
            SetObjectRef(hkPursuitSo, "player", shipCtrl);
            SetObjectRef(hkPursuitSo, "universe", universe);
            SetObjectRef(hkPursuitSo, "pool", pool);
            SetObjectRef(hkPursuitSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            hkPursuitSo.FindProperty("shipCount").intValue = 3;
            hkPursuitSo.FindProperty("spawnRadius").floatValue = 260f;
            hkPursuitSo.FindProperty("initialDelay").floatValue = 6f;
            hkPursuitSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep02DockingSceneName;
            hkPursuitSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy1Ep03HaulerSceneName;
            hkPursuitSo.FindProperty("clearedFlag").stringValue = "ep03_pursuit_cleared";
            hkPursuitSo.ApplyModifiedPropertiesWithoutUndo();
            hkPursuitGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Grimdock Blockade: enemies guard the freight hub, gated by EP03 docking.
            var grimBlockadeGo = new GameObject("Grimdock Blockade");
            var grimBlockade = grimBlockadeGo.AddComponent<GuardEncounter>();
            var grimBlockadeSo = new SerializedObject(grimBlockade);
            SetObjectRef(grimBlockadeSo, "player", shipCtrl);
            SetObjectRef(grimBlockadeSo, "universe", universe);
            SetObjectRef(grimBlockadeSo, "pool", pool);
            SetObjectRef(grimBlockadeSo, "definition", enemyShipDef);
            // Blockade mode: guardTarget = station, spawn around it, activate when player approaches
            SetObjectRef(grimBlockadeSo, "guardTarget", grimdock != null ? grimdock.transform : null);
            grimBlockadeSo.FindProperty("shipCount").intValue = 3;
            grimBlockadeSo.FindProperty("spawnRadius").floatValue = 120f;
            grimBlockadeSo.FindProperty("activateRange").floatValue = 400f;
            grimBlockadeSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep02DockingSceneName;
            grimBlockadeSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy1Ep03HaulerSceneName;
            grimBlockadeSo.FindProperty("clearedFlag").stringValue = "ep03_blockade_cleared";
            grimBlockadeSo.ApplyModifiedPropertiesWithoutUndo();
            grimBlockadeGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Pale Choir Blockade: enemies guard the archive station, gated by EP04 jungle completion.
            var choirBlockadeGo = new GameObject("Pale Choir Blockade");
            var choirBlockade = choirBlockadeGo.AddComponent<GuardEncounter>();
            var choirBlockadeSo = new SerializedObject(choirBlockade);
            SetObjectRef(choirBlockadeSo, "player", shipCtrl);
            SetObjectRef(choirBlockadeSo, "universe", universe);
            SetObjectRef(choirBlockadeSo, "pool", pool);
            SetObjectRef(choirBlockadeSo, "definition", enemyShipDef);
            // Blockade mode: guardTarget = station, spawn around it, activate when player approaches
            SetObjectRef(choirBlockadeSo, "guardTarget", archiveStation != null ? archiveStation.transform : null);
            choirBlockadeSo.FindProperty("shipCount").intValue = 3;
            choirBlockadeSo.FindProperty("spawnRadius").floatValue = 120f;
            choirBlockadeSo.FindProperty("activateRange").floatValue = 400f;
            choirBlockadeSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep04JungleMoonSceneName;
            choirBlockadeSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy1Ep04ArchiveRingSceneName;
            choirBlockadeSo.FindProperty("clearedFlag").stringValue = "ep04_choir_cleared";
            // Build spawn dialogue for this blockade
            var choirDialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep04Drones", Vector3.zero,
                Ep04Lines.Get("space_ep04_drones"), null, "space_ep04_drones", "ep04");
            var choirDialogueGo = choirDialogue.gameObject;
            choirDialogueGo.transform.SetParent(cockpit, false);
            choirDialogueGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            choirDialogueGo.transform.localRotation = Quaternion.identity;
            var choirDpSo = new SerializedObject(choirDialogue);
            choirDpSo.FindProperty("playOnStart").boolValue = false;
            choirDpSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(choirBlockadeSo, "spawnDialogue", choirDialogue);
            choirBlockadeSo.ApplyModifiedPropertiesWithoutUndo();
            choirBlockadeGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Dominion Interceptor Pursuit: corvette hunters chase the player after EP04 archive, gated by EP04 archive ring.
            var interceptorPursuitGo = new GameObject("Dominion Interceptor Pursuit");
            var interceptorPursuit = interceptorPursuitGo.AddComponent<GuardEncounter>();
            var interceptorPursuitSo = new SerializedObject(interceptorPursuit);
            SetObjectRef(interceptorPursuitSo, "player", shipCtrl);
            SetObjectRef(interceptorPursuitSo, "universe", universe);
            SetObjectRef(interceptorPursuitSo, "pool", pool);
            SetObjectRef(interceptorPursuitSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            interceptorPursuitSo.FindProperty("shipCount").intValue = 3;
            interceptorPursuitSo.FindProperty("spawnRadius").floatValue = 260f;
            interceptorPursuitSo.FindProperty("initialDelay").floatValue = 6f;
            interceptorPursuitSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep04ArchiveRingSceneName;
            interceptorPursuitSo.FindProperty("clearedFlag").stringValue = "ep04_escape_cleared";
            interceptorPursuitSo.ApplyModifiedPropertiesWithoutUndo();
            interceptorPursuitGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Rust Picket Blockade: Dominion fighters screen the Rust Collective, gated by EP04 archive completion.
            var picketBlockadeGo = new GameObject("Rust Picket Blockade");
            var picketBlockade = picketBlockadeGo.AddComponent<GuardEncounter>();
            var picketBlockadeSo = new SerializedObject(picketBlockade);
            SetObjectRef(picketBlockadeSo, "player", shipCtrl);
            SetObjectRef(picketBlockadeSo, "universe", universe);
            SetObjectRef(picketBlockadeSo, "pool", pool);
            SetObjectRef(picketBlockadeSo, "definition", enemyShipDef);
            // Blockade mode: guardTarget = station, spawn around it, activate when player approaches
            SetObjectRef(picketBlockadeSo, "guardTarget", rustCollective != null ? rustCollective.transform : null);
            picketBlockadeSo.FindProperty("shipCount").intValue = 3;
            picketBlockadeSo.FindProperty("spawnRadius").floatValue = 120f;
            picketBlockadeSo.FindProperty("activateRange").floatValue = 400f;
            picketBlockadeSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep04ArchiveRingSceneName;
            picketBlockadeSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy1Ep05MarketTierSceneName;
            picketBlockadeSo.FindProperty("clearedFlag").stringValue = "ep05_picket_cleared";
            // Build spawn dialogue for this blockade
            var picketDialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep05Fighters", Vector3.zero,
                Ep05Lines.Get("space_ep05_fighters"), null, "space_ep05_fighters", "ep05");
            var picketDialogueGo = picketDialogue.gameObject;
            picketDialogueGo.transform.SetParent(cockpit, false);
            picketDialogueGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            picketDialogueGo.transform.localRotation = Quaternion.identity;
            var picketDpSo = new SerializedObject(picketDialogue);
            picketDpSo.FindProperty("playOnStart").boolValue = false;
            picketDpSo.ApplyModifiedPropertiesWithoutUndo();
            SetObjectRef(picketBlockadeSo, "spawnDialogue", picketDialogue);
            picketBlockadeSo.ApplyModifiedPropertiesWithoutUndo();
            picketBlockadeGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Khall Retribution Pursuit: Dominion hunters chase the player after EP05 — Khall's parting threat.
            var retributionPursuitGo = new GameObject("Khall Retribution Pursuit");
            var retributionPursuit = retributionPursuitGo.AddComponent<GuardEncounter>();
            var retributionPursuitSo = new SerializedObject(retributionPursuit);
            SetObjectRef(retributionPursuitSo, "player", shipCtrl);
            SetObjectRef(retributionPursuitSo, "universe", universe);
            SetObjectRef(retributionPursuitSo, "pool", pool);
            SetObjectRef(retributionPursuitSo, "definition", enemyShipDef);
            // Pursuit mode: guardTarget = null, spawn around player
            retributionPursuitSo.FindProperty("shipCount").intValue = 3;
            retributionPursuitSo.FindProperty("spawnRadius").floatValue = 260f;
            retributionPursuitSo.FindProperty("initialDelay").floatValue = 6f;
            retributionPursuitSo.FindProperty("requiredCompletedScene").stringValue = Galaxy1Ep05MarketTierSceneName;
            retributionPursuitSo.FindProperty("clearedFlag").stringValue = "ep05_retribution_cleared";
            retributionPursuitSo.ApplyModifiedPropertiesWithoutUndo();
            retributionPursuitGo.SetActive(false); // DISABLED 2026-06-19 — temporarily off, see DISABLED-ENCOUNTERS.md

            // Landing trigger: six landable worlds + the Corsair dock. Fly slowly toward one and hold
            // right-hand Select to commit. LandingApproach gates on HostilesPresent() so the player
            // must clear the current wave before dropping out of space.
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            // Planets drift tangentially at a few u/s on their orbits, so allow a slightly faster
            // "stable" approach than the static-planet default.
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            // Landing on the EP01 water-mining world opens the enemy-wave gate for the rest of the run.
            lso.FindProperty("firstPlanetScene").stringValue = Galaxy1Ep01PlanetSceneName;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 11;
                SetLandable(landables, 0, aquilane, 140f, Galaxy1Ep01PlanetSceneName);
                // Velorum Station: chrome orbital ring, dockable for EP02 content. Requires EP01 completion.
                SetLandable(landables, 1, velorumStation, 90f, Galaxy1Ep02DockingSceneName, Galaxy1Ep01PlanetSceneName);
                SetLandable(landables, 2, jungleMoon, 140f, Galaxy1Ep04JungleMoonSceneName, Galaxy1Ep03HaulerSceneName);
                SetLandable(landables, 3, frosthold, 140f, Galaxy1FrostSceneName);
                SetLandable(landables, 4, blackveil, 140f, Galaxy1DesertSceneName);
                SetLandable(landables, 5, velKeth, 140f, Galaxy1LavaSceneName);
                // The Corsair: dock to walk Kessler's ship. Big approach radius — the hull is ~120 long.
                SetLandable(landables, 6, corsair, 220f, Galaxy1CorsairSceneName);
                // Grimdock Station: rust-freight hub, dockable for EP03 content. Requires EP02 completion.
                SetLandable(landables, 7, grimdock, 90f, Galaxy1Ep03HaulerSceneName, Galaxy1Ep02DockingSceneName);
                // Archive Station: amber ring station, dockable for EP04 content. Requires EP04 jungle moon completion.
                SetLandable(landables, 8, archiveStation, 90f, Galaxy1Ep04ArchiveRingSceneName, Galaxy1Ep04JungleMoonSceneName);
                // Rust Collective: stacked-hulk salvage station, dockable for EP05 content. Requires EP04 archive completion.
                SetLandable(landables, 9, rustCollective, 90f, Galaxy1Ep05MarketTierSceneName, Galaxy1Ep04ArchiveRingSceneName);
                // Galaxy 2 Jump Gate: travel to the Galaxy 2 hub. Requires the EP08 finale (Nebula Edge).
                SetLandable(landables, 10, jumpGate, 120f, Galaxy2SceneName, Galaxy1Ep08NebulaEdgeSceneName);
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Ship Spawn Placer: positions the player ship in space scenes.
            var shipSpawnerGo = new GameObject("Ship Spawn Placer");
            var shipSpawner = shipSpawnerGo.AddComponent<Ronin7.Ship.ShipSpawnPlacer>();
            var spSo = new SerializedObject(shipSpawner);
            SetObjectRef(spSo, "ship", shipCtrl);
            SetObjectRef(spSo, "universe", universe);
            SetObjectRef(spSo, "landing", landing);
            spSo.ApplyModifiedPropertiesWithoutUndo();

            // Completed Planet Outlines: renders outlines on planets that have been visited.
            var outlineGo = new GameObject("Completed Planet Outlines");
            var outlines = outlineGo.AddComponent<Ronin7.Ship.CompletedPlanetOutlines>();
            var outSo = new SerializedObject(outlines);
            SetObjectRef(outSo, "landing", landing);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/SamuraiOutline.mat");
            if (outlineMat != null)
                SetObjectRef(outSo, "outlineMaterial", outlineMat);
            else
                Debug.LogWarning("[Space Samurai] SamuraiOutline.mat not found at Assets/Ronin7/Art/Materials/");
            outSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1ScenePath);
            // Register every scene a landing/dock can load so LandingRequested resolves at runtime.
            EnsureScenesInBuild(Galaxy1ScenePath, Galaxy1JungleScenePath, Galaxy1LavaScenePath,
                Galaxy1DesertScenePath, Galaxy1WaterScenePath, Galaxy1FrostScenePath,
                Galaxy1CorsairScenePath, Galaxy1Ep01PlanetScenePath, Galaxy1Ep01HideoutScenePath,
                Ep01ShipScenePath, Galaxy1Ep02DockingScenePath, Galaxy1Ep02PensScenePath,
                Galaxy1Ep02CoreScenePath, Galaxy1Ep02SafeHouseScenePath,
                Galaxy1Ep03HaulerScenePath, Galaxy1Ep03EngineRoomScenePath,
                Galaxy1Ep03LotusScenePath, Galaxy1Ep03SanctuaryScenePath,
                Galaxy1Ep04JungleMoonScenePath, Galaxy1Ep04ArchiveRingScenePath,
                Galaxy1Ep04LedgerScenePath,
                Galaxy1Ep05MarketTierScenePath, Galaxy1Ep05PressureLocksScenePath,
                Galaxy1Ep05RotundaScenePath,
                Galaxy1Ep05CommandHubScenePath, Galaxy2ScenePath);
            Debug.Log($"[Space Samurai] Galaxy 1 SPACE scene built at {Galaxy1ScenePath}. " +
                      "Seven canon worlds on slow orbits (Aquilane/Velorum/Jungle Moon/Frosthold/" +
                      "Blackveil/Vel Keth + Rust Collective debris ring) and the dockable Corsair. " +
                      "Opening flight is CALM — enemy waves start after landing on Aquilane and " +
                      "returning to space; the cockpit arrow tracks the nearest hostile, then the " +
                      "objective. NOTE: rebuilding re-nulls InputActionReferences " +
                      "(throttle/steer/fire/land) — verify them on Flight Controller, Ship Guns, and " +
                      "Landing Approach in the Inspector before Play.");
        }

        /// <summary>
        /// Builds the Corsair INTERIOR scene — the walkable hub the player enters by docking on the
        /// Corsair in the Galaxy 1 space scene. Reuses the EP01 ship-interior recipe (floor/wall/
        /// sliding-door helpers, locomotion rig): an entry airlock, a corridor with three lived-in
        /// rooms (galley, engine room, bunks), and a cockpit room at the far end where Kessler
        /// greets the player. A handful of crew NPCs wander the rooms (NpcWander). Walking onto the
        /// LAUNCH pad in the airlock publishes ZoneCompleted → the flow returns to space.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Corsair Interior", priority = 66)]
        public static void BuildCorsairInterior()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting: cool key + flat ambient + warm/cool accents, per the EP01 interior recipe.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.9f, 1f);
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.2f);

            BuildAccentPointLight("AirlockLight", new Vector3(0f, 2.6f, 1f),
                new Color(1f, 0.86f, 0.66f), intensity: 2.0f, range: 10f);
            BuildAccentPointLight("CorridorLight", new Vector3(0f, 2.6f, 10f),
                new Color(0.6f, 0.78f, 1f), intensity: 1.6f, range: 12f);
            BuildAccentPointLight("EngineLight", new Vector3(5f, 2.6f, 8f),
                new Color(1f, 0.6f, 0.3f), intensity: 2.0f, range: 10f);
            BuildAccentPointLight("CockpitLight", new Vector3(0f, 2.6f, 21f),
                new Color(0.5f, 0.82f, 1f), intensity: 2.2f, range: 14f);

            // ---- Interior geometry: airlock -> corridor (3 side rooms) -> cockpit room. ----
            var interiorGo = new GameObject("CorsairInterior");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.2f, 0.22f, 0.26f);
            var ceilColor = new Color(0.12f, 0.13f, 0.16f);

            // Airlock: x[-3,3], z[-2,4], door gap in the back wall (z=4) into the corridor.
            BuildFloorCeiling(interior, "Airlock", new Vector3(0f, 0f, 1f), new Vector3(6f, 0f, 6f), floorColor, ceilColor);
            BuildWall(interior, "Airlock_WallW", new Vector3(-3f, 1.5f, 1f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallE", new Vector3(3f, 1.5f, 1f), new Vector3(0.2f, 3f, 6f));
            BuildWall(interior, "Airlock_WallFront", new Vector3(0f, 1.5f, -2f), new Vector3(6f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Airlock_WallBack", new Vector3(0f, 1.5f, 4f), 6f, true, 2.4f);

            // Corridor: x[-2,2], z[4,16]; side door gaps at z=8 (both sides) and z=13 (west).
            BuildFloorCeiling(interior, "Corridor", new Vector3(0f, 0f, 10f), new Vector3(4f, 0f, 12f), floorColor, ceilColor);
            BuildCorridorWall(interior, "Corridor_WallW", -2f, 4f, 16f, new[] { 8f, 13f }, 2.4f);
            BuildCorridorWall(interior, "Corridor_WallE", 2f, 4f, 16f, new[] { 8f }, 2.4f);

            // Three lived-in side rooms.
            BuildRoomShell(interior, "Galley", 2f, true, 8f, 2.5f, 6f, floorColor, ceilColor, "GALLEY", new Color(0.3f, 0.26f, 0.18f));
            BuildRoomShell(interior, "EngineRoom", 2f, false, 8f, 2.5f, 6f, floorColor, ceilColor, "ENGINE ROOM", new Color(0.3f, 0.22f, 0.18f));
            BuildRoomShell(interior, "Bunks", 2f, true, 13f, 2.5f, 6f, floorColor, ceilColor, "BUNKS", new Color(0.22f, 0.24f, 0.3f));

            // Engine-room glow props so it reads hot through the door.
            AddUnlitVisual(interior, "EngineCore", new Vector3(6.5f, 1.0f, 8f),
                new Vector3(0.8f, 1.0f, 0.8f), PrimitiveType.Cylinder, new Color(1f, 0.55f, 0.2f));

            // Cockpit room at the far end: x[-5,5], z[16,24], door gap in the front wall (z=16).
            BuildFloorCeiling(interior, "CockpitRoom", new Vector3(0f, 0f, 20f), new Vector3(10f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Cockpit_WallW", new Vector3(-5f, 1.5f, 20f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Cockpit_WallE", new Vector3(5f, 1.5f, 20f), new Vector3(0.2f, 3f, 8f));
            BuildWall(interior, "Cockpit_WallBack", new Vector3(0f, 1.5f, 24f), new Vector3(10f, 3f, 0.2f));
            BuildDoorwayWall(interior, "Cockpit_WallFront", new Vector3(0f, 1.5f, 16f), 10f, true, 2.4f);

            // Windshield strip on the back wall: glow-glass over a dark space quad, so the cockpit
            // reads as looking out into space without rendering an actual exterior.
            AddUnlitVisual(interior, "SpaceView", new Vector3(0f, 1.7f, 23.85f),
                new Vector3(8f, 1.2f, 0.02f), PrimitiveType.Cube, new Color(0.01f, 0.012f, 0.03f));
            var windGlass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            windGlass.name = "WindshieldGlass";
            Object.DestroyImmediate(windGlass.GetComponent<Collider>());
            windGlass.transform.SetParent(interior, false);
            windGlass.transform.localPosition = new Vector3(0f, 1.7f, 23.7f);
            windGlass.transform.localScale = new Vector3(8f, 1.2f, 0.02f);
            windGlass.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(new Color(0.45f, 0.6f, 0.8f), 0.12f);
            // Pilot console under the windshield.
            BuildProp(interior, "PilotConsole", new Vector3(0f, 0.5f, 22.8f), new Vector3(4f, 1f, 0.8f), new Color(0.15f, 0.18f, 0.22f));

            // Sliding doors: all openable (no mission gating aboard the Corsair).
            BuildSlidingDoor(interior, "AirlockDoor", new Vector3(0f, 0f, 4f), 2.4f, true, startLocked: false);
            BuildSlidingDoor(interior, "CockpitDoor", new Vector3(0f, 0f, 16f), 2.4f, true, startLocked: false);
            BuildSlidingDoor(interior, "DoorW_z8", new Vector3(-2f, 0f, 8f), 2.4f, false, startLocked: false);
            BuildSlidingDoor(interior, "DoorE_z8", new Vector3(2f, 0f, 8f), 2.4f, false, startLocked: false);
            BuildSlidingDoor(interior, "DoorW_z13", new Vector3(-2f, 0f, 13f), 2.4f, false, startLocked: false);

            // Game root + walkable rig (no combat aboard the Corsair).
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            var rig = BuildRig(refs, addLocomotion: true);
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = Vector3.zero;
            bounds.radius = 40f;

            // Kessler in the cockpit room, with a short auto-playing greeting.
            var kesslerPos = new Vector3(1.5f, 1f, 21.5f);
            var kessler = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, kesslerPos, "Kessler");
            if (kessler != null)
            {
                kessler.transform.rotation = Quaternion.LookRotation(new Vector3(-0.5f, 0f, -1f));
                var npc = kessler.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(npc);
                npcSo.FindProperty("displayName").stringValue = "Kessler";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var greeting = BuildDialoguePlayer("Dialogue_CorsairGreeting", kesslerPos, GetCorsairGreetingLines());
            var greetSo = new SerializedObject(greeting);
            greetSo.FindProperty("playOnStart").boolValue = true;
            greetSo.ApplyModifiedPropertiesWithoutUndo();

            // Wandering crew: decorative characters from the baked Generated prefabs, each with an
            // ambient NpcWander (no wiring needed). Positions sit inside the rooms/corridor.
            var crewSpots = new Vector3[]
            {
                new Vector3(0f, 0f, 6f),     // corridor near the airlock
                new Vector3(-4.5f, 0f, 8f),  // galley
                new Vector3(4.5f, 0f, 8f),   // engine room
                new Vector3(-4.5f, 0f, 13f), // bunks
                new Vector3(0f, 0f, 14.5f),  // corridor end
            };
            int placed = PlaceDecorativeCrowd(crewSpots);
            // PlaceDecorativeCrowd names them Decorative_<spec>; give each one an ambient wander.
            foreach (var wanderer in Object.FindObjectsByType<Transform>())
            {
                if (!wanderer.name.StartsWith("Decorative_")) continue;
                if (wanderer.GetComponent<NpcWander>() == null)
                {
                    var wander = wanderer.gameObject.AddComponent<NpcWander>();
                    var wSo = new SerializedObject(wander);
                    var radiusProp = wSo.FindProperty("wanderRadius");
                    if (radiusProp != null) radiusProp.floatValue = 1.8f; // stay inside the small rooms
                    wSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // LAUNCH pad in the airlock corner: glowing pad + label; walking onto it returns to space.
            var pad = AddUnlitVisual(interior, "LaunchPad", new Vector3(2.1f, 0.02f, -0.9f),
                new Vector3(1.2f, 0.04f, 1.2f), PrimitiveType.Cylinder, new Color(0.2f, 1f, 0.4f));
            var padLabel = new GameObject("LaunchLabel");
            padLabel.transform.SetParent(interior, false);
            padLabel.transform.localPosition = new Vector3(2.1f, 1.6f, -1.4f);
            padLabel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            padLabel.transform.localScale = Vector3.one * 0.04f;
            var padText = padLabel.AddComponent<TextMesh>();
            padText.text = "LAUNCH";
            padText.anchor = TextAnchor.MiddleCenter;
            padText.alignment = TextAlignment.Center;
            padText.fontSize = 48;
            padText.color = new Color(0.4f, 1f, 0.55f);
            var exit = pad.AddComponent<ProximityZoneExit>();
            var exitSo = new SerializedObject(exit);
            var exitRadius = exitSo.FindProperty("radius");
            if (exitRadius != null) exitRadius.floatValue = 1.0f;
            exitSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy1CorsairScenePath);
            EnsureScenesInBuild(Galaxy1CorsairScenePath);
            Debug.Log($"[Space Samurai] Corsair interior built at {Galaxy1CorsairScenePath} " +
                      $"({placed} wandering crew placed). Dock on the Corsair in the Galaxy 1 space " +
                      "scene to enter; walk onto the green LAUNCH pad in the airlock to return to space.");
        }

        /// <summary>Kessler's greeting aboard the Corsair (short, auto-playing, lore-flavoured).</summary>
        private static DialogueLine[] GetCorsairGreetingLines()
        {
            return new DialogueLine[]
            {
                new DialogueLine { speaker = "Kessler", text = "Welcome aboard the Corsair. She's a patchwork mess, but she's OUR patchwork mess.", seconds = 4f },
                new DialogueLine { speaker = "Kessler", text = "Stretch your legs, talk to the crew. The launch pad in the airlock will put you back in your fighter.", seconds = 4.5f },
            };
        }

        /// <summary>
        /// Builds one themed on-foot combat zone. The GAMEPLAY recipe (enemies, relics, sword,
        /// boundary, extraction, controllers and their wiring) is a verbatim clone of
        /// <see cref="BuildZoneScene"/> using the SAME definition assets — only the visuals (floor,
        /// ambient, key light, sky) are parameterized by <paramref name="theme"/>, so every Galaxy 1
        /// zone plays identically and differs only in mood. Saves to <c>theme.scenePath</c>.
        /// </summary>
        private static void BuildThemedZoneScene(GalaxyZoneTheme theme)
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();
            var zoneDef = EnsureZoneDefinition();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Themed sky + ambient fill give the zone its palette/mood without touching gameplay.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambientColor;
            RenderSettings.skybox = EnsureThemedSkybox(theme.displayName, theme.skyColor);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(zoneDef.radius * 0.5f, 1f, zoneDef.radius * 0.5f);
            TintShared(floor.GetComponent<Renderer>(), theme.floorColor);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = theme.lightColor;
            light.intensity = theme.lightIntensity;
            lightGo.transform.rotation = Quaternion.Euler(theme.lightEuler);

            PopulateZoneGameplay(refs, weapon, enemyDef, zoneDef);

            // Phase 4 will scatter zone-specific props under this root; propBuilder is null in Phase 3
            // so this is a no-op for now, but the grouping parent is already in place.
            var propRoot = new GameObject("Theme Props").transform;
            theme.propBuilder?.Invoke(propRoot, zoneDef.radius);

            EnsureFolder(SceneFolder);
            // Gameplay scenes carry the settings panel (with the save-slot buttons); build it here
            // so a scene rebuild never silently drops it.
            SettingsPanelBuilder.BuildSettingsPanel();
            // Re-bake input action refs before saving (rebuilds null them — see RewireOpenScene).
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, theme.scenePath);
            Debug.Log($"[Space Samurai] Galaxy 1 {theme.displayName} zone built at {theme.scenePath}. " +
                      "Same enemies/relics/extraction as the reference zone — only the visuals change. " +
                      "Rebuild this zone after Phase 4 props (propBuilder) land.");
        }

        /// <summary>
        /// Scatter helper: returns an XZ point at a random angle and a random radius in
        /// [<paramref name="rMin"/>, <paramref name="rMax"/>] with the given <paramref name="y"/>,
        /// drawn from <paramref name="rng"/> for deterministic rebuilds.
        /// </summary>
        private static Vector3 ScatterXZ(System.Random rng, float rMin, float rMax, float y)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = rMin + (float)rng.NextDouble() * (rMax - rMin);
            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        /// <summary>
        /// Jungle props: lush trees (brown trunk + stacked green foliage) ringing the play area,
        /// low ferns/shrubs inside it, and one small blue-green pond off to the side. Trees and the
        /// pond stay clear of the central enemy ring; small ground detail only inside. Deterministic
        /// via a fixed seed. All props are collider-free decoration.
        /// </summary>
        private static void BuildJungleProps(Transform parent, float radius)
        {
            var rng = new System.Random(40000001);

            // ~8 trees scattered from just outside the boundary out to a few × radius.
            for (int i = 0; i < 8; i++)
            {
                Vector3 basePos = ScatterXZ(rng, radius * 0.9f, radius * 2.2f, 0f);
                var tree = new GameObject("Tree").transform;
                tree.SetParent(parent, false);
                tree.localPosition = basePos;

                float height = 1.0f + (float)rng.NextDouble() * 0.8f; // trunk 1.0 .. 1.8
                AddVisualTinted(tree, "Trunk", new Vector3(0f, height, 0f),
                    new Vector3(0.15f, height, 0.15f), PrimitiveType.Cylinder, new Color(0.35f, 0.22f, 0.12f));

                // Two stacked foliage spheres in varied greens above the trunk.
                float g0 = 0.45f + (float)rng.NextDouble() * 0.25f;
                AddVisualTinted(tree, "Foliage Lower", new Vector3(0f, height * 2f, 0f),
                    Vector3.one * 1.3f, PrimitiveType.Sphere, new Color(0.16f, g0, 0.18f));
                float g1 = 0.45f + (float)rng.NextDouble() * 0.25f;
                AddVisualTinted(tree, "Foliage Upper", new Vector3(0f, height * 2f + 0.7f, 0f),
                    Vector3.one * 0.95f, PrimitiveType.Sphere, new Color(0.14f, g1, 0.16f));
            }

            // ~6 low shrubs/ferns inside the boundary (small flattened green spheres).
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.5f, radius * 0.85f, 0.15f);
                float g = 0.4f + (float)rng.NextDouble() * 0.25f;
                AddVisualTinted(parent, "Shrub", pos,
                    new Vector3(0.4f, 0.25f, 0.4f), PrimitiveType.Sphere, new Color(0.15f, g, 0.18f));
            }

            // One blue-green pond off to one side, a flat unlit cylinder for a subtle water shimmer.
            Vector3 pondPos = ScatterXZ(rng, radius * 0.65f, radius * 0.75f, 0.02f);
            AddUnlitVisual(parent, "Pond", pondPos,
                new Vector3(radius * 0.3f, 0.02f, radius * 0.3f), PrimitiveType.Cylinder,
                new Color(0.2f, 0.5f, 0.55f));
        }

        /// <summary>
        /// Lava props: a few large dark volcanoes far out with glowing craters, emissive lava cracks
        /// streaking the ground, scattered glowing embers, and scorched dark boulders. Glows use the
        /// unlit helper so they read hot regardless of light. Deterministic via a fixed seed; all
        /// props are collider-free.
        /// </summary>
        private static void BuildLavaProps(Transform parent, float radius)
        {
            var rng = new System.Random(40000002);

            // ~4 volcanoes far out: a wide dark cone-ish cylinder base with a glowing crater on top.
            for (int i = 0; i < 4; i++)
            {
                Vector3 basePos = ScatterXZ(rng, radius * 1.2f, radius * 2.5f, 0f);
                var volcano = new GameObject("Volcano").transform;
                volcano.SetParent(parent, false);
                volcano.localPosition = basePos;

                float bw = radius * 0.5f;          // base half-width
                float bh = radius * 0.4f;          // base height (cylinder scale.y is full height)
                AddVisualTinted(volcano, "Cone", new Vector3(0f, bh, 0f),
                    new Vector3(bw, bh, bw), PrimitiveType.Cylinder, new Color(0.10f, 0.09f, 0.10f));
                // Glowing crater sits at the top of the base (cylinder top ≈ y = 2 * scale.y).
                AddUnlitVisual(volcano, "Crater Glow", new Vector3(0f, bh * 2f, 0f),
                    new Vector3(bw * 0.5f, 0.1f, bw * 0.5f), PrimitiveType.Cylinder,
                    new Color(1.0f, 0.45f, 0.12f));
            }

            // ~8 lava cracks: thin flat emissive cubes with a random yaw, hugging the ground.
            for (int i = 0; i < 8; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.3f, radius * 0.95f, 0.02f);
                var crack = AddUnlitVisual(parent, "Lava Crack", pos,
                    new Vector3(0.6f, 0.02f, 0.15f), PrimitiveType.Cube, new Color(1.0f, 0.35f, 0.10f));
                crack.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            }

            // ~6 glowing embers scattered around.
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.5f, radius * 1.0f, 0.1f);
                AddUnlitVisual(parent, "Ember", pos,
                    Vector3.one * 0.2f, PrimitiveType.Sphere, new Color(1.0f, 0.55f, 0.15f));
            }

            // ~5 scorched dark boulders.
            for (int i = 0; i < 5; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.7f, radius * 1.3f, 0.25f);
                AddVisualTinted(parent, "Scorched Boulder", pos,
                    Vector3.one * 0.5f, PrimitiveType.Sphere, new Color(0.12f, 0.10f, 0.10f));
            }
        }

        /// <summary>
        /// Blackveil industrial props: smokestacks with glowing tops ringing the play area, long
        /// horizontal pipe runs, crate clusters inside the boundary, and a few hot vent glows on the
        /// ground. Deterministic via a fixed seed; all props are collider-free.
        /// </summary>
        private static void BuildIndustrialProps(Transform parent, float radius)
        {
            var rng = new System.Random(40000003);
            Color hull = new Color(0.28f, 0.28f, 0.31f);
            Color pipe = new Color(0.38f, 0.36f, 0.34f);
            Color crate = new Color(0.34f, 0.30f, 0.24f);
            Color emberGlow = new Color(1.0f, 0.50f, 0.15f);

            // ~5 smokestacks: tall dark cylinders with an unlit glowing rim on top.
            for (int i = 0; i < 5; i++)
            {
                float h = radius * 0.7f;
                Vector3 pos = ScatterXZ(rng, radius * 1.0f, radius * 2.2f, h);
                AddVisualTinted(parent, "Smokestack", pos, new Vector3(0.9f, h, 0.9f), PrimitiveType.Cylinder, hull);
                AddUnlitVisual(parent, "StackGlow", pos + new Vector3(0f, h, 0f),
                    new Vector3(1.0f, 0.08f, 1.0f), PrimitiveType.Cylinder, emberGlow);
            }

            // ~4 long pipe runs hugging the ground at random yaws.
            for (int i = 0; i < 4; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.8f, radius * 1.6f, 0.3f);
                var run = AddVisualTinted(parent, "PipeRun", pos,
                    new Vector3(0.3f, radius * 0.8f, 0.3f), PrimitiveType.Cylinder, pipe);
                run.transform.localRotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
            }

            // ~6 crate clusters inside the play area.
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.5f, radius * 0.95f, 0.35f);
                AddVisualTinted(parent, "Crate", pos, Vector3.one * 0.7f, PrimitiveType.Cube, crate);
                AddVisualTinted(parent, "CrateSmall", pos + new Vector3(0.6f, -0.15f, 0.2f),
                    Vector3.one * 0.4f, PrimitiveType.Cube, hull);
            }

            // ~3 hot vent glows on the ground.
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.4f, radius * 0.9f, 0.02f);
                AddUnlitVisual(parent, "VentGlow", pos,
                    new Vector3(0.6f, 0.02f, 0.6f), PrimitiveType.Cylinder, emberGlow);
            }
        }

        /// <summary>
        /// Frosthold glacial props: tilted ice spires ringing the play area, low snow drifts sunk
        /// into the floor, a frozen pond sheen, and scattered ice boulders. Deterministic via a
        /// fixed seed; all props are collider-free.
        /// </summary>
        private static void BuildFrostProps(Transform parent, float radius)
        {
            var rng = new System.Random(40000005);
            Color ice = new Color(0.80f, 0.90f, 1.0f);
            Color snow = new Color(0.94f, 0.96f, 1.0f);

            // ~6 ice spires: tall pale cylinders with a random tilt so they read as shards.
            for (int i = 0; i < 6; i++)
            {
                float h = radius * 0.5f;
                Vector3 pos = ScatterXZ(rng, radius * 0.9f, radius * 2.2f, h * 0.8f);
                var spire = AddVisualTinted(parent, "IceSpire", pos,
                    new Vector3(0.5f, h, 0.5f), PrimitiveType.Cylinder, ice);
                float tiltX = ((float)rng.NextDouble() * 2f - 1f) * 14f;
                float tiltZ = ((float)rng.NextDouble() * 2f - 1f) * 14f;
                spire.transform.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
            }

            // ~5 snow drifts: wide flattened spheres sunk so only the crowns show.
            for (int i = 0; i < 5; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.8f, radius * 2.4f, -radius * 0.08f);
                AddVisualTinted(parent, "SnowDrift", pos,
                    new Vector3(radius * 0.6f, radius * 0.18f, radius * 0.6f), PrimitiveType.Sphere, snow);
            }

            // One frozen pond: a flat unlit pale disc for a subtle sheen.
            Vector3 pondPos = ScatterXZ(rng, radius * 0.6f, radius * 0.75f, 0.02f);
            AddUnlitVisual(parent, "FrozenPond", pondPos,
                new Vector3(radius * 0.35f, 0.02f, radius * 0.35f), PrimitiveType.Cylinder,
                new Color(0.75f, 0.85f, 0.95f));

            // ~6 ice boulders inside the play area.
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 0.5f, radius * 1.0f, 0.2f);
                AddVisualTinted(parent, "IceBoulder", pos, Vector3.one * 0.45f, PrimitiveType.Sphere, ice);
            }
        }

        /// <summary>
        /// Water props: a large flat blue water sheet ringing the island (just below the floor so the
        /// floor reads as land), low sand/green island domes beyond the boundary, and dark rocks
        /// poking out of the water. The water sheet is unlit for a flat sheen. Deterministic via a
        /// fixed seed; all props are collider-free.
        /// </summary>
        private static void BuildWaterProps(Transform parent, float radius)
        {
            var rng = new System.Random(40000004);

            // One large water surface ring surrounding the island, dipped just under the floor.
            AddUnlitVisual(parent, "Water Surface", new Vector3(0f, -0.08f, 0f),
                new Vector3(radius * 3.5f, 0.02f, radius * 3.5f), PrimitiveType.Cylinder,
                new Color(0.18f, 0.42f, 0.72f));

            // ~5 low island domes beyond the boundary (alternating sand / green).
            for (int i = 0; i < 5; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 1.3f, radius * 2.8f, -radius * 0.05f);
                Color c = (i % 2 == 0)
                    ? new Color(0.80f, 0.70f, 0.45f)   // sand island
                    : new Color(0.20f, 0.45f, 0.25f);  // green island
                AddVisualTinted(parent, "Island", pos,
                    new Vector3(radius * 0.5f, radius * 0.2f, radius * 0.5f), PrimitiveType.Sphere, c);
            }

            // ~6 dark rocks poking out of the water.
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = ScatterXZ(rng, radius * 1.1f, radius * 2.5f, 0.05f);
                AddVisualTinted(parent, "Rock", pos,
                    Vector3.one * 0.3f, PrimitiveType.Sphere, new Color(0.20f, 0.22f, 0.26f));
            }
        }

        /// <summary>
        /// Returns (creating + caching on first use) the procedural skybox material for one zone theme
        /// at <c>Galaxy1_Sky_{themeName}.mat</c>. Models <see cref="EnsureBlackSkybox"/>: a
        /// Skybox/Procedural material with the sun disk off, tinted to <paramref name="skyColor"/> with
        /// a darker ground. Idempotent — re-applies the colours on an existing asset so re-themes take.
        /// </summary>
        private static Material EnsureThemedSkybox(string themeName, Color skyColor)
        {
            string path = $"{MaterialFolder}/Galaxy1_Sky_{themeName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = existing == null;
            var mat = existing != null ? existing : new Material(Shader.Find("Skybox/Procedural"));

            mat.SetFloat("_SunDisk", 0f);                 // 0 = None: no sun on the skybox itself
            mat.SetColor("_SkyTint", skyColor);
            mat.SetColor("_GroundColor", skyColor * 0.4f); // darker ground than the sky
            mat.SetFloat("_AtmosphereThickness", 0.4f);
            mat.SetFloat("_Exposure", 1.0f);

            if (isNew)
            {
                EnsureFolder(MaterialFolder);
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }

        /// <summary>Builds the lush Jungle on-foot zone (Galaxy 1).</summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Jungle Zone", priority = 61)]
        public static void BuildJungleZone()
        {
            BuildThemedZoneScene(new GalaxyZoneTheme
            {
                displayName = "Jungle",
                scenePath = Galaxy1JungleScenePath,
                floorColor = new Color(0.18f, 0.35f, 0.18f),
                ambientColor = new Color(0.18f, 0.26f, 0.20f),
                lightColor = new Color(0.85f, 0.95f, 0.70f),
                lightEuler = new Vector3(45f, -25f, 0f),
                lightIntensity = 1.1f,
                skyColor = new Color(0.35f, 0.55f, 0.60f),
                propBuilder = BuildJungleProps,
            });
        }

        /// <summary>Builds the molten Lava on-foot zone (Galaxy 1).</summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Lava Zone", priority = 62)]
        public static void BuildLavaZone()
        {
            BuildThemedZoneScene(new GalaxyZoneTheme
            {
                displayName = "Lava",
                scenePath = Galaxy1LavaScenePath,
                floorColor = new Color(0.12f, 0.08f, 0.07f),
                ambientColor = new Color(0.25f, 0.12f, 0.08f),
                lightColor = new Color(1.0f, 0.55f, 0.30f),
                lightEuler = new Vector3(30f, -40f, 0f),
                lightIntensity = 1.2f,
                skyColor = new Color(0.30f, 0.10f, 0.08f),
                propBuilder = BuildLavaProps,
            });
        }

        /// <summary>
        /// Builds the Blackveil industrial-moon on-foot zone (Galaxy 1, EP07's dead shipbreaker
        /// moon). Keeps the legacy Desert scene PATH so the flow/build lists stay untouched — only
        /// the theme changed when the zone was remapped to the canon world.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Blackveil (Industrial) Zone", priority = 63)]
        public static void BuildDesertZone()
        {
            BuildThemedZoneScene(new GalaxyZoneTheme
            {
                displayName = "Blackveil",
                scenePath = Galaxy1DesertScenePath,
                floorColor = new Color(0.30f, 0.30f, 0.33f),
                ambientColor = new Color(0.18f, 0.18f, 0.20f),
                lightColor = new Color(1.0f, 0.70f, 0.40f),
                lightEuler = new Vector3(35f, -20f, 0f),
                lightIntensity = 0.9f,
                skyColor = new Color(0.20f, 0.18f, 0.18f),
                propBuilder = BuildIndustrialProps,
            });
        }

        /// <summary>Builds the Frosthold glacial on-foot zone (Galaxy 1, EP06's plague colony world).</summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Frost Zone", priority = 65)]
        public static void BuildFrostZone()
        {
            BuildThemedZoneScene(new GalaxyZoneTheme
            {
                displayName = "Frost",
                scenePath = Galaxy1FrostScenePath,
                floorColor = new Color(0.85f, 0.88f, 0.92f),
                ambientColor = new Color(0.30f, 0.35f, 0.45f),
                lightColor = new Color(0.80f, 0.90f, 1.0f),
                lightEuler = new Vector3(40f, -30f, 0f),
                lightIntensity = 1.1f,
                skyColor = new Color(0.55f, 0.70f, 0.85f),
                propBuilder = BuildFrostProps,
            });
        }

        /// <summary>
        /// Builds the Velorum frozen water-moon on-foot zone (Galaxy 1, EP02). The legacy Water
        /// scene path, retuned colder/silver to read as the frozen moon rather than a tropical sea.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 1/Build Velorum (Water) Zone", priority = 64)]
        public static void BuildWaterZone()
        {
            BuildThemedZoneScene(new GalaxyZoneTheme
            {
                displayName = "Velorum",
                scenePath = Galaxy1WaterScenePath,
                floorColor = new Color(0.55f, 0.62f, 0.68f),
                ambientColor = new Color(0.22f, 0.28f, 0.36f),
                lightColor = new Color(0.82f, 0.90f, 1.0f),
                lightEuler = new Vector3(50f, -30f, 0f),
                lightIntensity = 1.0f,
                skyColor = new Color(0.50f, 0.62f, 0.75f),
                propBuilder = BuildWaterProps,
            });
        }

        /// <summary>
        /// Fills one <c>landables</c> array entry on a <see cref="LandingApproach"/>'s serialized list:
        /// the planet's transform as the target, its approach radius, and the destination scene name.
        /// Guards <paramref name="planet"/> for null (BuildThemedPlanet can return null if the art
        /// registry yields null) — a null planet leaves an empty (inert) entry rather than throwing.
        /// </summary>
        private static void SetLandable(SerializedProperty landables, int index, GameObject planet,
            float approachRadius, string destinationScene, string requiredCompletedScene = "")
        {
            var entry = landables.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("target").objectReferenceValue = planet != null ? planet.transform : null;
            entry.FindPropertyRelative("approachRadius").floatValue = approachRadius;
            entry.FindPropertyRelative("destinationScene").stringValue = destinationScene;
            entry.FindPropertyRelative("requiredCompletedScene").stringValue = requiredCompletedScene;
        }

        /// <summary>
        /// Encloses the open Galaxy 1 cockpit in a solid cabin so the player only sees space through a
        /// front windshield, seats Kessler in the back-right as a co-pilot, and adds an auto-playing
        /// briefing about the destination (water/EP01) planet. Everything is parented under
        /// <paramref name="cockpit"/> so it follows <see cref="CockpitRecenter"/>. Cabin parts are
        /// collider-free (<see cref="AddVisualTinted"/> strips colliders) — the rig is seated, so they
        /// are pure scenery and never intercept gun bolts.
        ///
        /// Cockpit-local frame: +Z forward, floor y=0. The forward gun/sight box
        /// (x∈[-0.55,0.55], y∈[0.6,1.4], z>0.85) is kept clear: the windshield opening spans it, the
        /// front pillars sit outboard of ±1.25, the header sits above y2.2, and the sill below y0.6.
        /// </summary>
        private static void BuildGalaxy1Cabin(Transform cockpit)
        {
            var hull  = new Color(0.16f, 0.17f, 0.20f);   // dark interior panelling
            var floorC = new Color(0.13f, 0.14f, 0.17f);  // slightly darker floor pan

            // Solid shell: floor pan, ceiling, back wall, two side walls. Cabin widened (walls pushed
            // out to x±1.9) so less hull fills peripheral vision and the view feels open; floor/ceiling/
            // back grow to match (x scale 3.2 → 3.8).
            AddVisualTinted(cockpit, "CabinFloor",   new Vector3(0f, -0.05f, -0.4f), new Vector3(3.8f, 0.1f, 3.4f), PrimitiveType.Cube, floorC);
            AddVisualTinted(cockpit, "CabinCeiling", new Vector3(0f, 2.45f, -0.4f),  new Vector3(3.8f, 0.1f, 3.4f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinBack",    new Vector3(0f, 1.2f, -2.05f),  new Vector3(3.8f, 2.6f, 0.1f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinWallL",   new Vector3(-1.9f, 1.2f, -0.4f), new Vector3(0.1f, 2.6f, 3.4f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinWallR",   new Vector3(1.9f, 1.2f, -0.4f),  new Vector3(0.1f, 2.6f, 3.4f), PrimitiveType.Cube, hull);

            // Sleek wraparound glass-bubble canopy (slim dark frame + cyan neon edge-light), sized to
            // the wide Galaxy 1 cabin. Keeps the forward sight box clear; glass is collider-free.
            BuildGlassCanopyFrame(cockpit, hull, 1.9f);

            // The enclosed interior is now shaded from the external sun, so add a warm cabin light.
            BuildAccentPointLight("CabinLight", new Vector3(0f, 2.2f, -0.3f),
                new Color(1f, 0.88f, 0.7f), intensity: 2.0f, range: 6f);

            // Kessler in the back-right, facing forward toward the seat/windshield (so he reads as a
            // co-pilot) — out of the forward view, framed by the cabin when the player turns back-right.
            var kessler = InstantiateNpc(ArtPrefabBuilder.KesslerPrefabPath, Vector3.zero, "Kessler");
            if (kessler != null)
            {
                kessler.transform.SetParent(cockpit, false);
                kessler.transform.localPosition = new Vector3(0.85f, 0.95f, -1.3f);
                kessler.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.85f, 0f, 1.3f));

                var npc = kessler.AddComponent<StoryNpc>();
                var npcSo = new SerializedObject(npc);
                npcSo.FindProperty("displayName").stringValue = "Kessler";
                npcSo.FindProperty("remote").boolValue = false;
                npcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Kessler yells when hostiles close within warning range.
            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Destination-planet briefing (pre-mission): compact comms subtitle near the top of the windshield.
            // BuildDialoguePlayer builds a room-scale panel; re-anchor its children to the GO origin and
            // shrink them so it reads as a small cockpit screen. NOW plays via BriefingSelector based on mission state.
            var preMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy1Briefing", Vector3.zero, "space_briefing");
            var briefingGo = preMissionDialogue.gameObject;
            briefingGo.transform.SetParent(cockpit, false);
            briefingGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            briefingGo.transform.localRotation = Quaternion.identity;

            var textT = briefingGo.transform.Find("Text");
            if (textT != null)
            {
                textT.localPosition = new Vector3(0f, 0f, 0.02f);
                textT.localScale = Vector3.one * 0.0045f;
            }
            var panelT = briefingGo.transform.Find("Panel");
            if (panelT != null)
            {
                panelT.localPosition = Vector3.zero;
                var bg = panelT.Find("Background");
                if (bg != null) bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var dpSo = new SerializedObject(preMissionDialogue);
            dpSo.FindProperty("playOnStart").boolValue = false; // Now controlled by BriefingSelector
            dpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing (Velorum Station comms): same positioning as pre-mission.
            var postMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy1Velorum", Vector3.zero, "space_velorum");
            var postMissionGo = postMissionDialogue.gameObject;
            postMissionGo.transform.SetParent(cockpit, false);
            postMissionGo.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMissionGo.transform.localRotation = Quaternion.identity;

            var postTextT = postMissionGo.transform.Find("Text");
            if (postTextT != null)
            {
                postTextT.localPosition = new Vector3(0f, 0f, 0.02f);
                postTextT.localScale = Vector3.one * 0.0045f;
            }
            var postPanelT = postMissionGo.transform.Find("Panel");
            if (postPanelT != null)
            {
                postPanelT.localPosition = Vector3.zero;
                var postBg = postPanelT.Find("Background");
                if (postBg != null) postBg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var postDpSo = new SerializedObject(postMissionDialogue);
            postDpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            postDpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 2 (EP02 post): same positioning as the other briefings, built from Ep02Lines with ep02 clip prefix.
            var postMission2Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep02Post", Vector3.zero,
                Ep02Lines.Get("space_ep02_post"), null, "space_ep02_post", "ep02");
            var postMission2Go = postMission2Dialogue.gameObject;
            postMission2Go.transform.SetParent(cockpit, false);
            postMission2Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission2Go.transform.localRotation = Quaternion.identity;

            var post2TextT = postMission2Go.transform.Find("Text");
            if (post2TextT != null)
            {
                post2TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post2TextT.localScale = Vector3.one * 0.0045f;
            }
            var post2PanelT = postMission2Go.transform.Find("Panel");
            if (post2PanelT != null)
            {
                post2PanelT.localPosition = Vector3.zero;
                var post2Bg = post2PanelT.Find("Background");
                if (post2Bg != null) post2Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post2DpSo = new SerializedObject(postMission2Dialogue);
            post2DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post2DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 3 (EP03 post): same positioning, built from Ep03Lines with ep03 clip prefix.
            var postMission3Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep03Post", Vector3.zero,
                Ep03Lines.Get("space_ep03_post"), null, "space_ep03_post", "ep03");
            var postMission3Go = postMission3Dialogue.gameObject;
            postMission3Go.transform.SetParent(cockpit, false);
            postMission3Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission3Go.transform.localRotation = Quaternion.identity;

            var post3TextT = postMission3Go.transform.Find("Text");
            if (post3TextT != null)
            {
                post3TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post3TextT.localScale = Vector3.one * 0.0045f;
            }
            var post3PanelT = postMission3Go.transform.Find("Panel");
            if (post3PanelT != null)
            {
                post3PanelT.localPosition = Vector3.zero;
                var post3Bg = post3PanelT.Find("Background");
                if (post3Bg != null) post3Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post3DpSo = new SerializedObject(postMission3Dialogue);
            post3DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post3DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 4 (EP04 approach): same positioning, built from Ep04Lines with ep04 clip prefix.
            var postMission4Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep04Approach", Vector3.zero,
                Ep04Lines.Get("space_ep04_approach"), null, "space_ep04_approach", "ep04");
            var postMission4Go = postMission4Dialogue.gameObject;
            postMission4Go.transform.SetParent(cockpit, false);
            postMission4Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission4Go.transform.localRotation = Quaternion.identity;

            var post4TextT = postMission4Go.transform.Find("Text");
            if (post4TextT != null)
            {
                post4TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post4TextT.localScale = Vector3.one * 0.0045f;
            }
            var post4PanelT = postMission4Go.transform.Find("Panel");
            if (post4PanelT != null)
            {
                post4PanelT.localPosition = Vector3.zero;
                var post4Bg = post4PanelT.Find("Background");
                if (post4Bg != null) post4Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post4DpSo = new SerializedObject(postMission4Dialogue);
            post4DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post4DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 5 (EP04 post): same positioning, built from Ep04Lines with ep04 clip prefix.
            var postMission5Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep04Post", Vector3.zero,
                Ep04Lines.Get("space_ep04_post"), null, "space_ep04_post", "ep04");
            var postMission5Go = postMission5Dialogue.gameObject;
            postMission5Go.transform.SetParent(cockpit, false);
            postMission5Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission5Go.transform.localRotation = Quaternion.identity;

            var post5TextT = postMission5Go.transform.Find("Text");
            if (post5TextT != null)
            {
                post5TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post5TextT.localScale = Vector3.one * 0.0045f;
            }
            var post5PanelT = postMission5Go.transform.Find("Panel");
            if (post5PanelT != null)
            {
                post5PanelT.localPosition = Vector3.zero;
                var post5Bg = post5PanelT.Find("Background");
                if (post5Bg != null) post5Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post5DpSo = new SerializedObject(postMission5Dialogue);
            post5DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post5DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 6 (EP05 approach): same positioning, built from Ep05Lines with ep05 clip prefix.
            // Keyed on the same milestone as postMission5 but checked first, so once EP05 ships the
            // approach briefing (set course for the Rust Collective) supersedes the EP04 epilogue.
            var postMission6Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep05Approach", Vector3.zero,
                Ep05Lines.Get("space_ep05_approach"), null, "space_ep05_approach", "ep05");
            var postMission6Go = postMission6Dialogue.gameObject;
            postMission6Go.transform.SetParent(cockpit, false);
            postMission6Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission6Go.transform.localRotation = Quaternion.identity;

            var post6TextT = postMission6Go.transform.Find("Text");
            if (post6TextT != null)
            {
                post6TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post6TextT.localScale = Vector3.one * 0.0045f;
            }
            var post6PanelT = postMission6Go.transform.Find("Panel");
            if (post6PanelT != null)
            {
                post6PanelT.localPosition = Vector3.zero;
                var post6Bg = post6PanelT.Find("Background");
                if (post6Bg != null) post6Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post6DpSo = new SerializedObject(postMission6Dialogue);
            post6DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post6DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 7 (EP05 post): same positioning, built from Ep05Lines with ep05 clip prefix.
            var postMission7Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep05Post", Vector3.zero,
                Ep05Lines.Get("space_ep05_post"), null, "space_ep05_post", "ep05");
            var postMission7Go = postMission7Dialogue.gameObject;
            postMission7Go.transform.SetParent(cockpit, false);
            postMission7Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission7Go.transform.localRotation = Quaternion.identity;

            var post7TextT = postMission7Go.transform.Find("Text");
            if (post7TextT != null)
            {
                post7TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post7TextT.localScale = Vector3.one * 0.0045f;
            }
            var post7PanelT = postMission7Go.transform.Find("Panel");
            if (post7PanelT != null)
            {
                post7PanelT.localPosition = Vector3.zero;
                var post7Bg = post7PanelT.Find("Background");
                if (post7Bg != null) post7Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post7DpSo = new SerializedObject(postMission7Dialogue);
            post7DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post7DpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing 8 (EP08 introspection): same positioning, built from Ep08Lines with ep08 clip prefix.
            var postMission8Dialogue = BuildDialoguePlayer("Dialogue_Galaxy1Ep08Introspection", Vector3.zero,
                Ep08Lines.Get("introspection_hold"), null, "introspection_hold", "ep08");
            var postMission8Go = postMission8Dialogue.gameObject;
            postMission8Go.transform.SetParent(cockpit, false);
            postMission8Go.transform.localPosition = new Vector3(0f, 1.62f, 1.18f);
            postMission8Go.transform.localRotation = Quaternion.identity;

            var post8TextT = postMission8Go.transform.Find("Text");
            if (post8TextT != null)
            {
                post8TextT.localPosition = new Vector3(0f, 0f, 0.02f);
                post8TextT.localScale = Vector3.one * 0.0045f;
            }
            var post8PanelT = postMission8Go.transform.Find("Panel");
            if (post8PanelT != null)
            {
                post8PanelT.localPosition = Vector3.zero;
                var post8Bg = post8PanelT.Find("Background");
                if (post8Bg != null) post8Bg.localScale = new Vector3(1.5f, 0.5f, 0.02f);
            }

            var post8DpSo = new SerializedObject(postMission8Dialogue);
            post8DpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            post8DpSo.ApplyModifiedPropertiesWithoutUndo();

            // BriefingSelector: chooses which briefing to play based on mission milestones.
            // Selection: EP08 cargo hold → postMission8; EP05 market tier → postMission7; EP04 archive ring → postMission6 (EP05 approach, supersedes postMission5); EP04 jungle → postMission4; EP03 hauler → postMission3; EP02 docking → postMission2; EP01 planet → postMission; else preMission.
            var selectorGo = new GameObject("BriefingSelector");
            selectorGo.transform.SetParent(cockpit, false);
            var selector = selectorGo.AddComponent<BriefingSelector>();
            var selectorSo = new SerializedObject(selector);
            SetObjectRef(selectorSo, "preMission", preMissionDialogue);
            SetObjectRef(selectorSo, "postMission", postMissionDialogue);
            selectorSo.FindProperty("planetScene").stringValue = Galaxy1Ep01PlanetSceneName;
            SetObjectRef(selectorSo, "postMission2", postMission2Dialogue);
            selectorSo.FindProperty("planetScene2").stringValue = Galaxy1Ep02DockingSceneName;
            SetObjectRef(selectorSo, "postMission3", postMission3Dialogue);
            selectorSo.FindProperty("planetScene3").stringValue = Galaxy1Ep03HaulerSceneName;
            SetObjectRef(selectorSo, "postMission4", postMission4Dialogue);
            selectorSo.FindProperty("planetScene4").stringValue = Galaxy1Ep04JungleMoonSceneName;
            SetObjectRef(selectorSo, "postMission5", postMission5Dialogue);
            selectorSo.FindProperty("planetScene5").stringValue = Galaxy1Ep04ArchiveRingSceneName;
            SetObjectRef(selectorSo, "postMission6", postMission6Dialogue);
            selectorSo.FindProperty("planetScene6").stringValue = Galaxy1Ep04ArchiveRingSceneName;
            SetObjectRef(selectorSo, "postMission7", postMission7Dialogue);
            selectorSo.FindProperty("planetScene7").stringValue = Galaxy1Ep05MarketTierSceneName;
            SetObjectRef(selectorSo, "postMission8", postMission8Dialogue);
            selectorSo.FindProperty("planetScene8").stringValue = "Galaxy1_EP08_CargoHold";
            selectorSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
