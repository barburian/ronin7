using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Builds greybox silhouettes for the nine Phase-1 placeholder characters (Echo-adjacent cast that
    /// don't have real art yet) directly into their existing <c>Named/&lt;Name&gt;.prefab</c> stubs.
    ///
    /// Decision: chapter builders resolve named characters by a hardcoded direct path constant to
    /// <c>Assets/Ronin7/Art/Generated/Characters3D/Named/&lt;Name&gt;.prefab</c> (see Chapter1Builder's
    /// Ch1KesslerPrefab/Ch1KhallPrefab/Ch1EchoBladePrefab + InstantiateNpc) — NOT via
    /// <see cref="Ronin7.Editor.Art.ArtPrefabRegistry"/>, which is only used for generic/unnamed art
    /// (cockpit, sword, enemy body, planet, etc.). A stub prefab already exists at that exact path for
    /// all nine names (a shared placeholder mesh), so this writes greybox content directly into those
    /// files rather than a separate Placeholders/ folder that nothing would resolve to.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string PlaceholderCharacterFolder = "Assets/Ronin7/Art/Generated/Characters3D/Named";
        private const string PlaceholderMaterialFolder = "Assets/Ronin7/Art/Materials/Placeholders";

        private enum PlaceholderArchetype { Humanoid, Hooded, Massive, Ethereal, Throned }

        private readonly struct PlaceholderCharacterSpec
        {
            public readonly string Name;
            public readonly PlaceholderArchetype Archetype;
            public readonly Color Primary;
            public readonly Color Secondary;
            public PlaceholderCharacterSpec(string name, PlaceholderArchetype archetype, Color primary, Color secondary)
            {
                Name = name; Archetype = archetype; Primary = primary; Secondary = secondary;
            }
        }

        [MenuItem("Tools/Space Samurai/Chapters/Build Placeholder Characters", priority = 199)]
        public static void BuildPlaceholderCharacters()
        {
            EnsureFolder(PlaceholderCharacterFolder);

            var specs = new[]
            {
                new PlaceholderCharacterSpec("Velorum-Broker", PlaceholderArchetype.Humanoid,
                    new Color(0.42f, 0.22f, 0.52f), new Color(0.62f, 0.48f, 0.70f)), // merchant purple
                new PlaceholderCharacterSpec("Vera-Dusk", PlaceholderArchetype.Humanoid,
                    new Color(0.36f, 0.36f, 0.38f), new Color(0.56f, 0.56f, 0.58f)), // ash grey
                new PlaceholderCharacterSpec("Vess", PlaceholderArchetype.Humanoid,
                    new Color(0.55f, 0.10f, 0.12f), new Color(0.28f, 0.08f, 0.09f)), // raider crimson
                new PlaceholderCharacterSpec("The-Mourners", PlaceholderArchetype.Hooded,
                    new Color(0.80f, 0.77f, 0.70f), new Color(0.68f, 0.65f, 0.58f)), // bone white
                new PlaceholderCharacterSpec("The-Warden", PlaceholderArchetype.Massive,
                    new Color(0.22f, 0.32f, 0.25f), new Color(0.12f, 0.18f, 0.14f)), // grave-iron green
                new PlaceholderCharacterSpec("Vane_Wraith-6", PlaceholderArchetype.Massive,
                    new Color(0.07f, 0.08f, 0.09f), new Color(0.14f, 0.52f, 0.52f)), // black/teal
                new PlaceholderCharacterSpec("The-Dreaming-Archive", PlaceholderArchetype.Ethereal,
                    new Color(0.85f, 0.75f, 0.45f), new Color(0.95f, 0.90f, 0.70f)), // pale gold
                new PlaceholderCharacterSpec("The-Previous-Owner", PlaceholderArchetype.Ethereal,
                    new Color(0.50f, 0.68f, 0.88f), new Color(0.75f, 0.85f, 0.95f)), // ghost blue
                new PlaceholderCharacterSpec("The-Hollow-Kings", PlaceholderArchetype.Throned,
                    new Color(0.05f, 0.05f, 0.07f), new Color(0.90f, 0.35f, 0.12f)), // obsidian/ember
            };

            int built = 0;
            foreach (var spec in specs)
            {
                if (BuildPlaceholderCharacter(spec)) built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Space Samurai] Built {built}/{specs.Length} placeholder characters into " +
                      $"{PlaceholderCharacterFolder}/ (overwrote the existing empty Named/<Name>.prefab stubs).");
        }

        private static bool BuildPlaceholderCharacter(PlaceholderCharacterSpec spec)
        {
            var root = new GameObject(spec.Name);
            try
            {
                switch (spec.Archetype)
                {
                    case PlaceholderArchetype.Humanoid:
                        BuildHumanoidArchetype(root.transform, spec.Primary, spec.Secondary);
                        break;
                    case PlaceholderArchetype.Hooded:
                        BuildHoodedArchetype(root.transform, spec.Primary, spec.Secondary);
                        break;
                    case PlaceholderArchetype.Massive:
                        BuildMassiveArchetype(root.transform, spec.Primary, spec.Secondary);
                        break;
                    case PlaceholderArchetype.Ethereal:
                        BuildEtherealArchetype(root.transform, spec.Primary, spec.Secondary);
                        break;
                    case PlaceholderArchetype.Throned:
                        BuildThronedArchetype(root.transform, spec.Primary, spec.Secondary);
                        break;
                }
                BuildPlaceholderLabel(root.transform, spec.Name);

                PrefabUtility.SaveAsPrefabAsset(root, $"{PlaceholderCharacterFolder}/{spec.Name}.prefab");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- Archetype silhouettes (~1.8m humanoid scale unless noted; floor-standing pivot at feet). ----

        private static void BuildHumanoidArchetype(Transform root, Color primary, Color secondary)
        {
            AddArchetypePart(root, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 0.75f, 0.45f), primary);
            AddArchetypePart(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(0.32f, 0.32f, 0.32f), secondary);
            AddArchetypePart(root, "ShoulderL", PrimitiveType.Sphere, new Vector3(-0.34f, 1.42f, 0f), new Vector3(0.22f, 0.16f, 0.22f), secondary);
            AddArchetypePart(root, "ShoulderR", PrimitiveType.Sphere, new Vector3(0.34f, 1.42f, 0f), new Vector3(0.22f, 0.16f, 0.22f), secondary);
        }

        private static void BuildHoodedArchetype(Transform root, Color primary, Color secondary)
        {
            AddArchetypePart(root, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.80f, 0f), new Vector3(0.46f, 0.72f, 0.46f), primary);
            // No native Cone primitive; a Cylinder stands in for the hood's cowl (same "Cone"-via-Cylinder
            // approximation Galaxy1Builder.BuildVolcanoes uses for its volcano cones).
            AddArchetypePart(root, "Hood", PrimitiveType.Cylinder, new Vector3(0f, 1.55f, 0f), new Vector3(0.34f, 0.35f, 0.34f), secondary);
        }

        private static void BuildMassiveArchetype(Transform root, Color primary, Color secondary)
        {
            AddArchetypePart(root, "Legs", PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.55f, 0.6f), primary);
            AddArchetypePart(root, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(0.9f, 1.0f, 0.55f), primary);
            AddArchetypePart(root, "ShoulderL", PrimitiveType.Cube, new Vector3(-0.62f, 1.9f, 0f), new Vector3(0.4f, 0.35f, 0.4f), secondary);
            AddArchetypePart(root, "ShoulderR", PrimitiveType.Cube, new Vector3(0.62f, 1.9f, 0f), new Vector3(0.4f, 0.35f, 0.4f), secondary);
            AddArchetypePart(root, "Head", PrimitiveType.Cube, new Vector3(0f, 2.35f, 0f), new Vector3(0.4f, 0.4f, 0.4f), secondary);
        }

        private static void BuildEtherealArchetype(Transform root, Color primary, Color secondary)
        {
            AddArchetypeGlassPart(root, "OrbBase", new Vector3(0f, 0.5f, 0f), new Vector3(0.55f, 0.55f, 0.55f), primary, 0.35f);
            AddArchetypeGlassPart(root, "OrbMid", new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.42f, 0.42f), primary, 0.35f);
            AddArchetypeGlassPart(root, "OrbHead", new Vector3(0f, 1.65f, 0f), new Vector3(0.28f, 0.28f, 0.28f), secondary, 0.35f);
        }

        private static void BuildThronedArchetype(Transform root, Color primary, Color secondary)
        {
            AddArchetypePart(root, "ThroneSeat", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.25f, 0.9f), primary);
            AddArchetypePart(root, "ThroneBack", PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.38f), new Vector3(1.0f, 1.4f, 0.15f), primary);
            AddArchetypePart(root, "ThroneArmL", PrimitiveType.Cube, new Vector3(-0.48f, 0.65f, 0.15f), new Vector3(0.12f, 0.3f, 0.5f), primary);
            AddArchetypePart(root, "ThroneArmR", PrimitiveType.Cube, new Vector3(0.48f, 0.65f, 0.15f), new Vector3(0.12f, 0.3f, 0.5f), primary);
            AddArchetypePart(root, "FigureTorso", PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0.1f), new Vector3(0.4f, 0.45f, 0.4f), secondary);
            AddArchetypePart(root, "FigureHead", PrimitiveType.Sphere, new Vector3(0f, 1.32f, 0.1f), new Vector3(0.28f, 0.28f, 0.28f), secondary);
        }

        private static void BuildPlaceholderLabel(Transform root, string name)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.01f;
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = $"[PLACEHOLDER] {name}";
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 42;
            tm.color = new Color(1f, 0.85f, 0.3f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void AddArchetypePart(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreatePlaceholderMaterial(color, null);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static void AddArchetypeGlassPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color, float alpha)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreatePlaceholderMaterial(color, alpha);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        /// <summary>Prefabs cannot hold loose in-memory materials — SaveAsPrefabAsset serializes them as
        /// missing ({fileID: 0} pink), see ArtPrefabBuilder.TintInstance. So every placeholder material is
        /// persisted as an asset (deduped by color, variant-file naming convention) and reloaded before use.</summary>
        private static Material GetOrCreatePlaceholderMaterial(Color color, float? glassAlpha)
        {
            int r = Mathf.RoundToInt(color.r * 255f);
            int g = Mathf.RoundToInt(color.g * 255f);
            int b = Mathf.RoundToInt(color.b * 255f);
            string fileName = glassAlpha.HasValue
                ? $"Placeholder_Glass_r{r}g{g}b{b}a{Mathf.RoundToInt(glassAlpha.Value * 100f)}"
                : $"Placeholder_Unlit_r{r}g{g}b{b}";
            string path = $"{PlaceholderMaterialFolder}/{fileName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EnsureFolder(PlaceholderMaterialFolder);
            var mat = glassAlpha.HasValue ? MakeGlassMaterial(color, glassAlpha.Value) : MakeUnlitMaterial(color);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
