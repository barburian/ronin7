using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Menu entries for the art-generation pipeline. Per-subject regenerate items are stubs that
    /// Agents 1–6 will implement so the menu structure is visible from day one.
    /// </summary>
    public static class ArtGenerationMenu
    {
        private const string TestOutputPath = "Assets/Ronin7/Art/Generated/TestRoundTrip.png";

        /// <summary>
        /// One-click faceted/pixel-art restyle: regenerates everything in dependency order —
        /// meshes → detail textures → planet textures → art prefabs → EP01 cast → batch
        /// characters. Keyless and idempotent; every step overwrites its assets in place so
        /// prefab/mesh GUIDs (and therefore scene references) survive.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Restyle Faceted — Rebuild Everything", priority = 0)]
        public static void RestyleFacetedRebuildEverything()
        {
            Debug.Log("[ArtGen Restyle] 1/6 faceted meshes…");
            LowPolyMeshes.RebuildAll();
            Debug.Log("[ArtGen Restyle] 2/6 pixel detail textures…");
            BuildEp01ProceduralTextures();
            Debug.Log("[ArtGen Restyle] 3/6 planet textures…");
            PixelatePlanetTextures();
            Debug.Log("[ArtGen Restyle] 4/6 art prefabs…");
            ArtPrefabBuilder.BuildAll();
            Debug.Log("[ArtGen Restyle] 5/6 EP01 characters…");
            ArtPrefabBuilder.BuildEp01Characters();
            Debug.Log("[ArtGen Restyle] 6/6 batch characters from specs…");
            CharacterBatchBuilder.BuildAll();
            Debug.Log("[ArtGen Restyle] combat VFX prefabs…");
            VfxPrefabBuilder.BuildAll();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArtGen Restyle] Done. Re-run the Tools/Space Samurai Build ... Scene items " +
                      "so scenes pick up the restyled prefabs.");
        }

        [MenuItem("Tools/Space Samurai/Art/Set Gemini API Key")]
        public static void OpenApiKeyWindow() => GeminiApiKeyWindow.ShowWindow();

        /// <summary>
        /// Diagnostic: lists every model the current API key can hit, with their supported
        /// methods. Useful when GenerateImageAsync returns 404 because Google renamed a model
        /// or the user's key doesn't have access to the configured one.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/List Available Models")]
        public static async void ListAvailableModels()
        {
            string apiKey = EditorPrefs.GetString(GeminiClient.ApiKeyPref, "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[ArtGenerationMenu] No Gemini API key set — Tools/Space Samurai/Art/Set Gemini API Key first.");
                return;
            }
            string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={UnityWebRequest.EscapeURL(apiKey)}&pageSize=200";
            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ArtGenerationMenu] ListModels HTTP {req.responseCode} {req.error}: {req.downloadHandler?.text}");
                return;
            }
            // Filter to anything that supports generateContent (the endpoint GeminiClient uses) and
            // looks image-capable by name. The full JSON is logged too for cases where the heuristic misses.
            string json = req.downloadHandler.text;
            Debug.Log("[ArtGenerationMenu] === Models supporting generateContent (image-capable heuristic) ===");
            int idx = 0;
            while ((idx = json.IndexOf("\"name\":", idx)) >= 0)
            {
                int qStart = json.IndexOf('"', idx + 7) + 1;
                int qEnd = json.IndexOf('"', qStart);
                if (qStart <= 0 || qEnd <= 0) break;
                string name = json.Substring(qStart, qEnd - qStart);
                int nextName = json.IndexOf("\"name\":", qEnd);
                int blockEnd = nextName > 0 ? nextName : json.Length;
                string block = json.Substring(qEnd, blockEnd - qEnd);
                bool supportsGen = block.Contains("\"generateContent\"");
                bool looksImage = name.Contains("image") || name.Contains("imagen");
                if (supportsGen && looksImage)
                    Debug.Log($"  {name}");
                idx = qEnd;
            }
            Debug.Log("[ArtGenerationMenu] === Full ListModels response (paste this if no match above) ===\n" + json);
        }

        [MenuItem("Tools/Space Samurai/Art/Test Gemini Round-Trip")]
        public static async void TestGeminiRoundTrip()
        {
            try
            {
                byte[] png = await GeminiClient.GenerateImageAsync(
                    "a stylized cel-shaded samurai helmet on black background, painted anime aesthetic",
                    seed: 42, width: 512, height: 512);

                string absDir = Path.GetDirectoryName(Path.Combine(Directory.GetCurrentDirectory(), TestOutputPath));
                if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);
                File.WriteAllBytes(TestOutputPath, png);

                AssetDatabase.ImportAsset(TestOutputPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TestOutputPath);
                if (tex != null) Selection.activeObject = tex;
                Debug.Log($"[ArtGenerationMenu] Round-trip OK → {TestOutputPath} ({png.Length} bytes)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ArtGenerationMenu] Round-trip failed: {ex.Message}");
            }
        }

        [MenuItem("Tools/Space Samurai/Art/Clear Gemini Cache")]
        public static void ClearGeminiCache()
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), GeminiClient.CacheFolder);
            if (!Directory.Exists(abs))
            {
                Debug.Log("[ArtGenerationMenu] Cache folder did not exist; nothing to clear.");
                return;
            }

            int removed = 0;
            // Only nuke PNGs and their .tmp siblings — keep .meta files so Unity doesn't churn
            // import GUIDs for the folder itself.
            foreach (string file in Directory.GetFiles(abs))
            {
                if (file.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".png.tmp", System.StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".png.meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    removed++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[ArtGenerationMenu] Cleared {removed} file(s) from {GeminiClient.CacheFolder}.");
        }

        [MenuItem("Tools/Space Samurai/Art/Regenerate Planets")]
        public static async void RegeneratePlanets()
        {
            // Hand-curated biome set. Variety comes from the texture, not the prefab — the
            // PlanetPrefabPath constant in XRRigBuilder is a single path, so we ship one prefab
            // (Planet_VariantA.prefab) and swap per-instance materials. The "Variant" letter in
            // the constant is legacy from the original plan; treat it as the canonical planet
            // prefab name.
            var biomes = new[]
            {
                new BiomeSpec("Rocky",    "barren rocky desert world, ochre and rust palette with dark canyons",                  seed: 11),
                new BiomeSpec("Ice",      "frozen ice world, pale cyan and white palette with frosted highlands",                seed: 23),
                new BiomeSpec("Lush",     "verdant forested world, deep green continents on teal oceans",                        seed: 37),
                new BiomeSpec("GasGiant", "banded gas giant, warm cream and amber horizontal bands with subtle swirling storms", seed: 53),
            };

            const string PlanetMatFolder = "Assets/Ronin7/Art/Materials";
            const string TexFolder       = "Assets/Ronin7/Art/Generated/Planets";
            const string ToonShaderName  = "Ronin7/SamuraiToon";

            EnsureFolder(TexFolder);

            var shader = Shader.Find(ToonShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Agent1 Planets] Shader '{ToonShaderName}' not found; aborting.");
                return;
            }

            int ok = 0, fail = 0;
            foreach (var biome in biomes)
            {
                string prompt =
                    $"equirectangular planet texture, stylized cel-shaded, {biome.PromptFragment}, no clouds, painted-anime aesthetic";
                string texPath = $"{TexFolder}/{biome.Name}.png";
                string matPath = $"{PlanetMatFolder}/SamuraiToon_Planet_{biome.Name}.mat";

                Debug.Log($"[Agent1 Planets] Generating biome '{biome.Name}' (seed {biome.Seed}) -> {texPath}");
                try
                {
                    byte[] png = await GeminiClient.GenerateImageAsync(prompt, biome.Seed, 1024, 1024);
                    // Pixelate before writing: downscale to 128px + posterize so the planet matches
                    // the point-filtered pixel-art style of the rest of the art pass.
                    png = PixelatePng(png, 128, 5);

                    string absDir = Path.GetDirectoryName(Path.Combine(Directory.GetCurrentDirectory(), texPath));
                    if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);
                    File.WriteAllBytes(texPath, png);

                    AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                    ApplyPixelArtImportSettings(texPath, 128);
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        mat = new Material(shader) { name = $"SamuraiToon_Planet_{biome.Name}" };
                        AssetDatabase.CreateAsset(mat, matPath);
                    }
                    else if (mat.shader != shader)
                    {
                        mat.shader = shader;
                    }
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white); // let the map carry color
                    if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", 0.3f);
                    if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", 4f);
                    if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(0.45f, 0.65f, 0.85f, 0.6f));
                    EditorUtility.SetDirty(mat);

                    Debug.Log($"[Agent1 Planets] OK  {biome.Name}: {png.Length} bytes -> {matPath}");
                    ok++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Agent1 Planets] FAIL {biome.Name}: {ex.Message}");
                    fail++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Agent1 Planets] Done. ok={ok} fail={fail}. Output: {TexFolder}/*.png and {PlanetMatFolder}/SamuraiToon_Planet_*.mat");
        }

        /// <summary>
        /// Keyless restyle path for planets: pixelates the planet PNGs already on disk (downscale
        /// to 128px + 5-level posterize) and flips their import to point filtering — no Gemini
        /// call. Idempotent: re-running on an already-pixelated map is a no-op.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Pixelate Existing Planet Textures", priority = 2)]
        public static void PixelatePlanetTextures()
        {
            const string TexFolder = "Assets/Ronin7/Art/Generated/Planets";
            string absDir = Path.Combine(Directory.GetCurrentDirectory(), TexFolder);
            if (!Directory.Exists(absDir))
            {
                Debug.LogWarning($"[ArtGen Planets] No planet textures at {TexFolder} — generate them " +
                                 "first (Regenerate Planets) or skip; planets keep flat tinted color.");
                return;
            }

            int done = 0;
            foreach (string abs in Directory.GetFiles(absDir, "*.png"))
            {
                string assetPath = $"{TexFolder}/{Path.GetFileName(abs)}";
                File.WriteAllBytes(abs, PixelatePng(File.ReadAllBytes(abs), 128, 5));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ApplyPixelArtImportSettings(assetPath, 128);
                done++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtGen Planets] Pixelated {done} planet texture(s) (128px, 5-level posterize, point-filtered).");
        }

        /// <summary>Box-downscales a PNG to <paramref name="outSize"/>² and posterizes each channel
        /// to <paramref name="levels"/> steps. LoadImage gives a readable texture regardless of the
        /// asset's importer settings, so this works on any PNG bytes.</summary>
        private static byte[] PixelatePng(byte[] png, int outSize, int levels)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!src.LoadImage(png)) { UnityEngine.Object.DestroyImmediate(src); return png; }
            try
            {
                int sw = src.width, sh = src.height;
                var sp = src.GetPixels32();
                var dp = new Color32[outSize * outSize];

                for (int y = 0; y < outSize; y++)
                for (int x = 0; x < outSize; x++)
                {
                    // Box average over the source block this output texel covers.
                    int x0 = x * sw / outSize, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / outSize);
                    int y0 = y * sh / outSize, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / outSize);
                    int r = 0, g = 0, b = 0, count = 0;
                    for (int sy = y0; sy < y1; sy++)
                    for (int sx = x0; sx < x1; sx++)
                    {
                        var c = sp[sy * sw + sx];
                        r += c.r; g += c.g; b += c.b; count++;
                    }
                    dp[y * outSize + x] = new Color32(
                        Posterize(r / count, levels), Posterize(g / count, levels), Posterize(b / count, levels), 255);
                }

                var dst = new Texture2D(outSize, outSize, TextureFormat.RGBA32, false, false);
                dst.SetPixels32(dp);
                dst.Apply(false, false);
                byte[] result = dst.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(dst);
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(src); }
        }

        private static byte Posterize(int value, int levels)
        {
            float v = value / 255f;
            float q = Mathf.Round(v * (levels - 1)) / (levels - 1);
            return (byte)Mathf.RoundToInt(q * 255f);
        }

        private readonly struct BiomeSpec
        {
            public readonly string Name;
            public readonly string PromptFragment;
            public readonly int Seed;
            public BiomeSpec(string name, string promptFragment, int seed)
            {
                Name = name; PromptFragment = promptFragment; Seed = seed;
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        /// <summary>
        /// Paints subtle tileable detail maps onto the EP01 material families (reuses the planet
        /// generation pattern: PNG → import sRGB+mips → assign _BaseMap). Detail maps are near-WHITE
        /// with faint dark structure: SamuraiToon multiplies _BaseMap * _BaseColor * vertexColor, so
        /// a bright map modulates any tint (steel/cloth/cyan) without recoloring it. The three EP01
        /// humans share the EnemyFoot base material, so they share one armor/cloth detail map.
        /// Keyless-safe: with no API key set, this skips entirely and prefabs keep their flat tinted
        /// look (default _BaseMap is "white").
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Regenerate EP01 Textures")]
        public static async void RegenerateEp01Textures()
        {
            const string MatFolder      = "Assets/Ronin7/Art/Materials";
            const string VariantsFolder = MatFolder + "/Variants";
            const string TexFolder      = "Assets/Ronin7/Art/Generated/EP01";

            string apiKey = EditorPrefs.GetString(GeminiClient.ApiKeyPref, "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogWarning("[ArtGen EP01] No Gemini API key set — skipping texture generation. " +
                    "EP01 prefabs render with flat tinted color (no detail _BaseMap), which is fine. " +
                    "Set a key via Tools/Space Samurai/Art/Set Gemini API Key, then re-run to add detail.");
                return;
            }

            EnsureFolder(TexFolder);

            var families = new[]
            {
                new TexFamily("ArmorDetail", "SamuraiToon_EnemyFoot",
                    "seamless tileable subtle surface detail, near-white field with faint dark panel seams, fine brushed-metal grain and a few light scratches, cel-shaded, low contrast, mostly white", 71),
                new TexFamily("BladeDetail", "SamuraiToon_Sword",
                    "seamless tileable subtle steel blade detail, near-white field with a faint wavy hamon temper line and fine vertical brushed-metal grain, low contrast, mostly white", 83),
                new TexFamily("GloveDetail", "SamuraiToon_Hand",
                    "seamless tileable subtle woven cloth detail, near-white field with faint fabric weave, low contrast, mostly white", 97),
            };

            int ok = 0, fail = 0;
            foreach (var fam in families)
            {
                string texPath = $"{TexFolder}/{fam.Name}.png";
                try
                {
                    byte[] png = await GeminiClient.GenerateImageAsync(fam.Prompt, fam.Seed, 1024, 1024);
                    // Match the procedural pixel-art recipe so the keyed path doesn't undo the style.
                    png = PixelatePng(png, 64, 5);
                    File.WriteAllBytes(texPath, png);

                    AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                    ApplyPixelArtImportSettings(texPath, 64);
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                    int applied = AssignBaseMapToFamily(MatFolder, VariantsFolder, fam.BaseMaterial, tex);
                    Debug.Log($"[ArtGen EP01] OK {fam.Name}: {png.Length} bytes → applied to {applied} material(s).");
                    ok++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ArtGen EP01] FAIL {fam.Name}: {ex.Message}");
                    fail++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtGen EP01] Done. ok={ok} fail={fail}. Detail _BaseMap applied to EP01 material " +
                      "families + their tinted variants. Prefabs reference these shared materials, so no " +
                      "prefab rebuild is needed — reopen the EP01 scene to see the change.");
        }

        /// <summary>
        /// Assigns a detail map to a base SamuraiToon material and every tinted variant derived from
        /// it (variant assets are named "{base}_rXgYbZ"). Returns the number of materials touched.
        /// </summary>
        private static int AssignBaseMapToFamily(string matFolder, string variantsFolder, string baseName, Texture2D tex)
        {
            int n = 0;
            void Apply(Material m)
            {
                if (m == null || !m.HasProperty("_BaseMap")) return;
                m.SetTexture("_BaseMap", tex);
                EditorUtility.SetDirty(m);
                n++;
            }

            Apply(AssetDatabase.LoadAssetAtPath<Material>($"{matFolder}/{baseName}.mat"));
            if (AssetDatabase.IsValidFolder(variantsFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { variantsFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path).StartsWith(baseName + "_"))
                        Apply(AssetDatabase.LoadAssetAtPath<Material>(path));
                }
            }
            return n;
        }

        /// <summary>
        /// Sibling of <see cref="AssignBaseMapToFamily"/> for emission: assigns an emission MASK to a
        /// base SamuraiToon material and every tinted variant derived from it, sets the HDR
        /// <paramref name="hdrEmission"/> accent colour, and enables the _EMISSION keyword (the shader
        /// multiplies map × colour, so a black-field mask only glows along its bright lines). Returns
        /// the number of materials touched. Mirrors the variant loop so the 262 batch NPCs that share
        /// the EnemyFoot family pick up the glow too.
        /// </summary>
        private static int AssignEmissionToFamily(string matFolder, string variantsFolder, string baseName,
            Texture2D tex, Color hdrEmission)
        {
            int n = 0;
            void Apply(Material m)
            {
                if (m == null) return;
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", hdrEmission);
                m.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(m);
                n++;
            }

            Apply(AssetDatabase.LoadAssetAtPath<Material>($"{matFolder}/{baseName}.mat"));
            if (AssetDatabase.IsValidFolder(variantsFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { variantsFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path).StartsWith(baseName + "_"))
                        Apply(AssetDatabase.LoadAssetAtPath<Material>(path));
                }
            }
            return n;
        }

        private readonly struct TexFamily
        {
            public readonly string Name;
            public readonly string BaseMaterial;
            public readonly string Prompt;
            public readonly int Seed;
            public TexFamily(string name, string baseMaterial, string prompt, int seed)
            {
                Name = name; BaseMaterial = baseMaterial; Prompt = prompt; Seed = seed;
            }
        }

        // ---------------- procedural pixel-art detail textures (no API) ----------------

        /// <summary>
        /// Builds the pixel-art surface-detail maps procedurally in-code — no Gemini key/quota
        /// needed. Each map is a 64×64, point-filtered, near-WHITE tileable grayscale detail drawn
        /// at texel scale and quantized to a 4-step palette (SamuraiToon multiplies
        /// _BaseMap * _BaseColor * vertexColor, so a bright map adds crisp pixel structure to any
        /// tint without recoloring it). Reuses <see cref="AssignBaseMapToFamily"/> to push each map
        /// onto its base material + tinted variants. Re-run any time; deterministic.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Art/Build EP01 Procedural Textures", priority = 1)]
        public static void BuildEp01ProceduralTextures()
        {
            const string MatFolder      = "Assets/Ronin7/Art/Materials";
            const string VariantsFolder = MatFolder + "/Variants";
            const string TexFolder      = "Assets/Ronin7/Art/Generated/EP01";
            const int Size = 64;

            EnsureFolder(TexFolder);

            // Make sure the ally/civilian NPC body material exists before we assign detail/emission to
            // its family — batch NPCs + Kessler/Ronin9 tint from it (see ArtPrefabBuilder).
            ArtPrefabBuilder.EnsureNpcMaterial();

            int applied = 0;
            // ArmorDetail covers both the Dominion EnemyFoot family and the ally/civilian Npc family.
            applied += WriteAndAssignDetail(TexFolder, "ArmorDetail", GenerateDetailTexture(Size, DetailKind.Armor), MatFolder, VariantsFolder, "SamuraiToon_EnemyFoot", "SamuraiToon_Npc");
            applied += WriteAndAssignDetail(TexFolder, "BladeDetail", GenerateDetailTexture(Size, DetailKind.Blade), MatFolder, VariantsFolder, "SamuraiToon_Sword");
            applied += WriteAndAssignDetail(TexFolder, "GloveDetail", GenerateDetailTexture(Size, DetailKind.Glove), MatFolder, VariantsFolder, "SamuraiToon_Hand");
            applied += WriteAndAssignDetail(TexFolder, "HullDetail",  GenerateDetailTexture(Size, DetailKind.Hull),  MatFolder, VariantsFolder,
                "SamuraiToon_EnemyShip", "SamuraiToon_ShipHull", "SamuraiToon_Cockpit");
            applied += WriteAndAssignDetail(TexFolder, "RockDetail",  GenerateDetailTexture(Size, DetailKind.Rock),  MatFolder, VariantsFolder, "SamuraiToon_Asteroid");

            // Emissive seam/visor mask: one black-field-with-bright-lines map, assigned per family at
            // its faction HDR colour so panel seams + a visor band glow under bloom — magenta for the
            // Dominion foot troopers, amber for the interceptor hull, and cyan for the ally/civilian
            // NPC body family (so the 262 batch NPCs + named allies don't read as Dominion). Imported
            // LINEAR (it's an emission mask, not an albedo detail map).
            string emiPath = $"{TexFolder}/EnemyEmissionMask.png";
            var emiSrc = GenerateDetailTexture(Size, DetailKind.EmissionMask);
            File.WriteAllBytes(emiPath, emiSrc.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(emiSrc);
            AssetDatabase.ImportAsset(emiPath, ImportAssetOptions.ForceUpdate);
            ApplyPixelArtImportSettings(emiPath, Size, sRGB: false);
            var emiTex = AssetDatabase.LoadAssetAtPath<Texture2D>(emiPath);
            applied += AssignEmissionToFamily(MatFolder, VariantsFolder, "SamuraiToon_EnemyFoot", emiTex, NeonPalette.Magenta);
            applied += AssignEmissionToFamily(MatFolder, VariantsFolder, "SamuraiToon_EnemyShip", emiTex, NeonPalette.Amber);
            // Ally/civilian NPC family: same seam/visor mask, but cyan (player/ally faction) so the
            // 262 batch NPCs + named allies (Kessler/Ronin9) no longer share the Dominion magenta.
            applied += AssignEmissionToFamily(MatFolder, VariantsFolder, "SamuraiToon_Npc", emiTex, NeonPalette.CyanDim);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtGen EP01] Pixel detail textures built (64px, point-filtered, no API) and " +
                      $"applied to {applied} material(s). Rebuild prefabs/scenes to see them everywhere.");
        }

        private enum DetailKind { Armor, Blade, Glove, Hull, Rock, EmissionMask }

        // 4-step grayscale palette every detail map snaps to. Step 0 stays white (so seams/highlights
        // read), but the lower steps are pushed darker than the original near-white set for a grittier,
        // higher-contrast techno-noir surface — the dark grooves now actually read as grooves while the
        // bright base still modulates the material tint rather than recoloring it.
        private static readonly float[] DetailPalette = { 1f, 0.85f, 0.66f, 0.45f };
        private static float Shade(int level) => DetailPalette[Mathf.Clamp(level, 0, DetailPalette.Length - 1)];

        /// <summary>Renders one tileable grayscale pixel-art detail map. Patterns are drawn per
        /// texel with modulo/integer-cycle math so they wrap seamlessly, and every texel lands
        /// exactly on the 4-step palette — no gradients, crisp under point filtering.</summary>
        private static Texture2D GenerateDetailTexture(int size, DetailKind kind)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var px = new Color32[size * size];
            const float TAU = Mathf.PI * 2f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v;
                switch (kind)
                {
                    case DetailKind.Armor:
                    {
                        const int cell = 16; // 4 panels across at 64px
                        int rx = x % cell, ry = y % cell;
                        int panel = (x / cell + y / cell) & 1;          // checkered panel shading
                        v = Shade(panel);
                        bool rivet = (rx == 2 || rx == cell - 3) && (ry == 2 || ry == cell - 3);
                        if (rivet) v = Shade(2);                        // corner rivets
                        if (rx == 0 || ry == 0) v = Shade(3);           // 1-texel panel seams
                        else if (!rivet && Hash01(x * 131 + y * 977) < 0.10f) v = Shade(panel + 1); // grittier scratch/grain
                        break;
                    }
                    case DetailKind.Blade:
                    {
                        v = x % 4 == 0 ? Shade(1) : Shade(0);           // vertical brushed lines
                        int hamonY = Mathf.RoundToInt(size * 0.5f + Mathf.Sin(x * (TAU * 3f / size)) * 6f);
                        if (y == hamonY) v = Shade(2);                  // texel-stepped hamon temper line
                        break;
                    }
                    case DetailKind.Glove:
                    {
                        v = Shade(((x / 2 + y / 2) & 1) == 0 ? 0 : 1);  // 2×2 weave checker
                        if (y % 8 == 0) v = Shade(2);                   // stitch rows
                        break;
                    }
                    case DetailKind.Hull:
                    {
                        const int cell = 16;
                        int rx = x % cell, ry = y % cell;
                        int panel = (x / cell + y / cell) & 1;
                        v = Shade(panel);
                        bool rivet = (rx == 2 || rx == cell - 3) && (ry == 2 || ry == cell - 3);
                        if (rivet) v = Shade(2);
                        // Vent block (2×4 dark slats) on alternate panels only.
                        if (panel == 1 && rx >= 6 && rx <= 9 && ry >= 7 && ry <= 8) v = Shade(3);
                        if (rx == 0 || ry == 0) v = Shade(3);           // panel seams
                        break;
                    }
                    case DetailKind.EmissionMask:
                    {
                        // Pure 0/1 glow mask (bypasses the near-white palette): black field with bright
                        // thin lines that the shader multiplies by the HDR _EmissionColor and Bloom turns
                        // into neon. Lines along the 16px panel-seam grid + a 2-texel horizontal "visor"
                        // band — both wrap seamlessly (modulo grid / full-width rows).
                        const int cell = 16;
                        int rx = x % cell, ry = y % cell;
                        bool seam  = rx == 0 || ry == 0;     // glowing panel seams
                        bool visor = y == 20 || y == 21;     // visor / eye strip band
                        v = (seam || visor) ? 1f : 0f;
                        break;
                    }
                    default: // Rock
                    {
                        // Low-frequency blotches + fine speckle, both hash-thresholded.
                        int blotch = Hash01((x / 8) * 31 + (y / 8) * 57) < 0.3f ? 1 : 0;
                        float h = Hash01(x * 131 + y * 977);
                        v = h < 0.08f ? Shade(3) : h < 0.22f ? Shade(blotch + 1) : Shade(blotch);
                        break;
                    }
                }

                byte b = (byte)Mathf.RoundToInt(v * 255f);
                px[y * size + x] = new Color32(b, b, b, 255);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // Cheap deterministic integer hash → [0,1]. Standard Wang-style bit mix.
        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
            return m / (float)0x7fffffff;
        }

        /// <summary>Encodes a generated detail map to a PNG asset (sRGB, point-filtered, mips ON —
        /// point + mips keeps texels crisp up close while minification self-antialiases instead of
        /// shimmering in the headset) and assigns it to the given material families. The in-memory
        /// source texture is destroyed after encoding; the imported PNG asset is what materials
        /// reference.</summary>
        private static int WriteAndAssignDetail(string texFolder, string name, Texture2D tex,
            string matFolder, string variantsFolder, params string[] baseMaterials)
        {
            string texPath = $"{texFolder}/{name}.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            ApplyPixelArtImportSettings(texPath, 64);

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            int applied = 0;
            foreach (string baseMaterial in baseMaterials)
                applied += AssignBaseMapToFamily(matFolder, variantsFolder, baseMaterial, imported);
            return applied;
        }

        /// <summary>Point filtering + mips: the pixel-art import recipe shared by detail maps and
        /// planet textures. <paramref name="sRGB"/> defaults true (albedo/detail maps); pass false for
        /// emission masks, which the shader samples as linear data.</summary>
        private static void ApplyPixelArtImportSettings(string texPath, int maxSize, bool sRGB = true)
        {
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = maxSize;
            importer.anisoLevel = 0;
            importer.SaveAndReimport();
        }

        [MenuItem("Tools/Space Samurai/Art/Regenerate Ship Enemies")]
        public static void RegenerateShipEnemies()
        {
            // Rebuild the low-poly enemy ship from its deterministic builder — preserves the gameplay
            // contract (Health, EnemyShip refs, muzzle, body renderer, hitbox sphere collider).
            bool ok = ArtPrefabBuilder.BuildEnemyShipPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(ok
                ? "[ArtGen] Rebuilt low-poly EnemyShip.prefab. Re-run a Build Phase X Scene to apply."
                : "[ArtGen] EnemyShip rebuild skipped (material missing — see earlier error).");
        }

        // Canned on-foot enemies to (re)generate from a description via the character generator.
        // Fixed seeds keep the cached spec stable across runs (no re-bill). Names are overridden so
        // each AI variant lands at a predictable Generated/<Name>.prefab path.
        private static readonly (string name, string description, int seed)[] FootEnemyJobs =
        {
            ("EnemyFootAI",       "a kabuto samurai warrior in dark indigo armor with brass trim", 101),
            ("DominionTrooperAI", "a steel-armored dominion soldier with a crimson chest stripe",  102),
        };

        [MenuItem("Tools/Space Samurai/Art/Regenerate On-foot Enemies")]
        public static async void RegenerateFootEnemies()
        {
            string apiKey = EditorPrefs.GetString(GeminiClient.ApiKeyPref, "");
            if (string.IsNullOrEmpty(apiKey))
            {
                // Keyless-safe: rebuild the on-foot enemies with the existing hand-coded builders,
                // which produce them unchanged. (BuildAll covers EnemyFoot; BuildEp01Characters
                // covers the DominionTrooper.)
                Debug.LogWarning("[ArtGen] No Gemini API key — falling back to the hand-coded on-foot " +
                    "enemy builders (rebuilds them unchanged). Set a key via " +
                    "Tools/Space Samurai/Art/Set Gemini API Key to generate AI variants instead.");
                ArtPrefabBuilder.BuildAll();
                ArtPrefabBuilder.BuildEp01Characters();
                return;
            }

            int ok = 0;
            foreach (var (name, description, seed) in FootEnemyJobs)
            {
                var prefab = await ArtPrefabBuilder.GenerateCharacterAsync(
                    description, ArtPrefabBuilder.CharacterRole.Combat, seed, overrideName: name);
                if (prefab != null) ok++;
            }
            Debug.Log($"[ArtGen] Regenerated {ok}/{FootEnemyJobs.Length} on-foot enemy AI variants into " +
                      "Prefabs/Art/Generated/ (combat-ready: Health + hurtbox + katana rig). The " +
                      "canonical EnemyFoot/DominionTrooper prefabs are left untouched — point a scene " +
                      "or the spawner at a variant to adopt it.");
        }

        [MenuItem("Tools/Space Samurai/Art/Regenerate Hands")]
        public static void RegenerateHands()
        {
            // Rebuild both low-poly VR hands; the builder preserves the "Visual" wrapper + pose the
            // XR rig expects.
            int ok = (ArtPrefabBuilder.BuildHandPrefab(isLeft: true) ? 1 : 0)
                   + (ArtPrefabBuilder.BuildHandPrefab(isLeft: false) ? 1 : 0);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArtGen] Rebuilt {ok}/2 low-poly hand prefabs (Hand_L / Hand_R).");
        }

        [MenuItem("Tools/Space Samurai/Art/Regenerate Cockpit")]
        public static void RegenerateCockpit()
        {
            // Rebuild the low-poly cockpit; the builder keeps the seated head/hand pose targets and
            // the must-stay-clear forward sightline intact.
            bool ok = ArtPrefabBuilder.BuildCockpitPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(ok
                ? "[ArtGen] Rebuilt low-poly Cockpit.prefab. Re-run a Build Phase X Scene to apply."
                : "[ArtGen] Cockpit rebuild skipped (material missing — see earlier error).");
        }

        [MenuItem("Tools/Space Samurai/Art/Regenerate Sword")]
        public static void RegenerateSword()
        {
            // Rebuild the low-poly katana; the builder preserves the gameplay-critical Blade child
            // (trigger BoxCollider + BladeDamager at the canonical hitbox transform).
            bool ok = ArtPrefabBuilder.BuildSwordPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(ok
                ? "[ArtGen] Rebuilt low-poly Sword_Katana.prefab. Re-run a Build Phase X Scene to apply."
                : "[ArtGen] Sword rebuild skipped (material missing — see earlier error).");
        }

        [MenuItem("Tools/Space Samurai/Art/Regenerate All")]
        public static void RegenerateAll()
        {
            // Every subject is now wired: Planets (textures), On-foot Enemies (character generator),
            // and the deterministic low-poly props (ship enemy, hands, cockpit, sword).
            RegeneratePlanets();
            RegenerateFootEnemies();
            RegenerateShipEnemies();
            RegenerateHands();
            RegenerateCockpit();
            RegenerateSword();
            Debug.Log("[ArtGen] Regenerate All ran every wired subject. " +
                      "Re-run each Build Phase X Scene to bake the rebuilt prefabs in.");
        }
    }

    /// <summary>
    /// Tiny EditorWindow with a password-style field for entering the Gemini API key.
    /// </summary>
    public class GeminiApiKeyWindow : EditorWindow
    {
        private string _apiKey = "";

        public static void ShowWindow()
        {
            var win = GetWindow<GeminiApiKeyWindow>(true, "Gemini API Key", true);
            win.minSize = new Vector2(420, 100);
            win._apiKey = EditorPrefs.GetString(GeminiClient.ApiKeyPref, "");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Gemini API Key (stored in EditorPrefs only):");
            _apiKey = EditorGUILayout.PasswordField(_apiKey);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    EditorPrefs.SetString(GeminiClient.ApiKeyPref, _apiKey ?? "");
                    Debug.Log("[ArtGenerationMenu] Gemini API key saved to EditorPrefs.");
                    Close();
                }
                if (GUILayout.Button("Clear"))
                {
                    EditorPrefs.SetString(GeminiClient.ApiKeyPref, "");
                    _apiKey = "";
                    Debug.Log("[ArtGenerationMenu] Gemini API key cleared.");
                }
            }
        }
    }
}
