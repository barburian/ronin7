using System;
using System.Text;
using System.Threading.Tasks;
using Ronin7.Combat;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Description → low-poly character pipeline. Asks Gemini (via GeminiClient.GenerateTextAsync) to
    /// turn a text description into a JSON list of colored blocks, then bakes those blocks into a
    /// prefab using the same primitive-composition helpers the hand-coded builders use
    /// (MakeRoundedTinted / MakeChildTinted / TintInstance / AddOutline). Default block style is the
    /// rounded cube (Roblox-ish soft look); the description can request cylinder/sphere/hard cubes
    /// per part. Two roles: Decorative (visual only) and Combat (adds the Enemy.cs contract — a
    /// capsule body on the root for Renderer-resolve, a CapsuleCollider hurtbox, Health, and the
    /// shared AttachHeldKatanaRig so the character can fight).
    ///
    /// Partial of ArtPrefabBuilder so it can reuse that file's private helpers and constants directly.
    /// </summary>
    public static partial class ArtPrefabBuilder
    {
        private const string GeneratedFolder = PrefabFolder + "/Generated";

        public enum CharacterRole { Decorative, Combat }

        // JsonUtility-friendly spec: only public fields, float[] for vectors/colors (JsonUtility
        // supports float[] and arrays of [Serializable] classes). Missing fields stay null/default
        // and are filled in by the ToV3/ToColor fallbacks below.
        [Serializable]
        public class CharPartSpec
        {
            public string name;
            public string shape;  // "rounded" (default) | "cylinder" | "sphere" | "hardcube" | "capsule"
            public float[] pos;   // local position [x,y,z]
            public float[] scale; // local scale [x,y,z]
            public float[] rot;   // optional local euler [x,y,z]
            public float[] color; // linear RGB [r,g,b], 0..1
        }

        [Serializable]
        public class CharacterSpec
        {
            public string name;
            public CharPartSpec[] parts;
        }

        // Quest budget: each part is at least one draw (the ink outline adds a second submesh), so
        // cap how many blocks a single character can spend. Excess parts are dropped with a warning.
        private const int MaxParts = 40;

        /// <summary>
        /// Bakes a parsed spec into a prefab under Prefabs/Art/Generated/ and returns the saved
        /// prefab asset (null on failure). The VISUAL is built identically for both roles — the spec's
        /// blocks at their authored full-size coordinates — so a character's combat and decorative
        /// forms read as the same figure. Combat then layers gameplay on top without distorting the
        /// body: Health, a CapsuleCollider hurtbox sized to the standing figure, and the held-katana
        /// rig placed at the figure's right hand. (Earlier the Combat path squashed the whole figure
        /// into a (0.5,0.9,0.5)@y0.9 enemy-capsule frame, which is why it looked nothing like the
        /// decorative form.)
        /// </summary>
        public static GameObject BuildCharacterFromSpec(CharacterSpec spec, CharacterRole role)
        {
            if (spec == null || spec.parts == null || spec.parts.Length == 0)
            {
                Debug.LogError("[CharacterGen] spec has no parts; nothing to build.");
                return null;
            }

            // Faction split: Dominion / Obsidian-Synod characters keep the magenta EnemyFoot family;
            // every other character (allies, civilians, independent species) uses the cyan-accent NPC
            // body family so they no longer glow the enemy magenta.
            var mat = IsHostileCharacter(spec.name) ? LoadMaterial(EnemyFootMatPath) : EnsureNpcMaterial();
            if (mat == null) return null; // LoadMaterial already logged

            string safeName = Sanitize(spec.name);
            var root = new GameObject(safeName);
            try
            {
                int count = Mathf.Min(spec.parts.Length, MaxParts);
                if (spec.parts.Length > MaxParts)
                    Debug.LogWarning($"[CharacterGen] {spec.parts.Length} parts exceeds the Quest-friendly " +
                                     $"cap of {MaxParts}; using the first {MaxParts}.");

                for (int i = 0; i < count; i++)
                    SpawnPart(root.transform, spec.parts[i], mat, i);

                if (role == CharacterRole.Combat)
                {
                    // Combat contract, layered on the same full-size figure (no root squash/offset):
                    //   - Health + a CapsuleCollider hurtbox wrapping the standing body (world units,
                    //     since the root stays at identity scale).
                    //   - Just a katana gripped in the figure's existing right hand (includeArm:false,
                    //     so no redundant extra arm), shrunk to ~0.6m and tipped upright so it reads as
                    //     a held sword rather than a blade jutting forward.
                    // The rig keeps the ArmR/Sword/Blade/BladeTip names Enemy.cs drives, so these
                    // prefabs are still wirable as enemies (BuildEnemy would resolve its bodyRenderer
                    // from a child renderer rather than the root).
                    root.AddComponent<Health>();
                    var hurtbox = root.AddComponent<CapsuleCollider>();
                    hurtbox.center = new Vector3(0f, 0.9f, 0f);
                    hurtbox.radius = 0.35f;
                    hurtbox.height = 1.8f;

                    AttachHeldKatanaRig(root.transform, mat,
                        limbColor: new Color(0.12f, 0.1f, 0.18f), roundedLimbs: true, limbThickness: 0.1f,
                        armOrigin: new Vector3(0.24f, 0.82f, 0.12f), rigScale: 0.6f,
                        includeArm: false, armEuler: new Vector3(-80f, 0f, 0f));
                }

                AddOutline(root);

                // AssetDatabase-aware (not just filesystem) so SaveAsPrefabAsset can write into this
                // brand-new subfolder on a fresh checkout.
                EnsureAssetFolder(GeneratedFolder);
                string path = $"{GeneratedFolder}/{safeName}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[CharacterGen] Built '{safeName}' ({count} parts, role={role}) → {path}");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>
        /// End-to-end: build the prompt, call Gemini for JSON, parse it, bake the prefab. Surfaces
        /// (and swallows) parse/empty-spec failures with a truncated dump so a malformed model
        /// response is debuggable rather than throwing through the editor UI.
        /// </summary>
        public static async Task<GameObject> GenerateCharacterAsync(
            string description, CharacterRole role, int seed, string overrideName = null)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                Debug.LogError("[CharacterGen] description is empty.");
                return null;
            }

            string prompt = BuildCharacterPrompt(description, role);
            string json = await GeminiClient.GenerateTextAsync(prompt, seed);

            CharacterSpec spec = null;
            try { spec = JsonUtility.FromJson<CharacterSpec>(json); }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterGen] Could not parse spec JSON: {ex.Message}\n{Trunc(json)}");
                return null;
            }

            if (spec == null || spec.parts == null || spec.parts.Length == 0)
            {
                Debug.LogError($"[CharacterGen] Spec JSON had no parts:\n{Trunc(json)}");
                return null;
            }

            // A caller-supplied name (the "regenerate existing" path) wins so the output filename is
            // predictable; otherwise fall back to the model's name, then the description.
            if (!string.IsNullOrEmpty(overrideName)) spec.name = overrideName;
            else if (string.IsNullOrEmpty(spec.name)) spec.name = description;

            var prefab = BuildCharacterFromSpec(spec, role);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return prefab;
        }

        // ---------------- spec → geometry ----------------

        // Specs author parts that merely touch (or leave a hairline gap), so with a per-part ink
        // outline the figure reads as a pile of separate blocks. Inflating every part a little about
        // its own centre overlaps neighbours and closes the seams so it reads as one connected body.
        // Tune here: 1.0 = exactly as authored, higher = chunkier/more fused.
        private const float PartSeamPad = 1.12f;

        private static void SpawnPart(Transform parent, CharPartSpec p, Material mat, int index)
        {
            if (p == null) return;
            Vector3 pos = ToV3(p.pos, Vector3.zero);
            Vector3 scale = ToV3(p.scale, Vector3.one) * PartSeamPad;
            Color color = ToColor(p.color);
            Quaternion? rot = (p.rot != null && p.rot.Length >= 3)
                ? Quaternion.Euler(p.rot[0], p.rot[1], p.rot[2])
                : (Quaternion?)null;
            string name = string.IsNullOrEmpty(p.name) ? $"Part_{index}" : p.name;
            string shape = (p.shape ?? "rounded").Trim().ToLowerInvariant();

            switch (shape)
            {
                case "cylinder":
                    MakeChildTinted(parent, name, PrimitiveType.Cylinder, mat, color, pos, scale, rot);
                    break;
                case "sphere":
                    MakeChildTinted(parent, name, PrimitiveType.Sphere, mat, color, pos, scale, rot);
                    break;
                case "capsule":
                    MakeChildTinted(parent, name, PrimitiveType.Capsule, mat, color, pos, scale, rot);
                    break;
                case "hardcube":
                case "box":
                    MakeChildTinted(parent, name, PrimitiveType.Cube, mat, color, pos, scale, rot);
                    break;
                default: // "rounded" / "cube" / unknown → soft rounded block (Roblox default)
                    MakeRoundedTinted(parent, name, mat, color, pos, scale, rot);
                    break;
            }
        }

        private static Vector3 ToV3(float[] a, Vector3 fallback)
            => (a != null && a.Length >= 3) ? new Vector3(a[0], a[1], a[2]) : fallback;

        private static Color ToColor(float[] a)
            => (a != null && a.Length >= 3)
                ? new Color(Mathf.Clamp01(a[0]), Mathf.Clamp01(a[1]), Mathf.Clamp01(a[2]))
                : new Color(0.6f, 0.6f, 0.6f);

        /// <summary>
        /// True for the handful of batch/named characters that are canonically Dominion / Obsidian
        /// Synod and therefore keep the magenta enemy glow; everyone else routes to the cyan NPC body
        /// material. The character specs carry no faction field, and the Decorative/Combat/Filler
        /// folders are ROLE not faction (e.g. Maelgorn and ObsidianRank sit in Decorative beside ally
        /// Kessler), so this is the minimal explicit hostile set. Deliberately conservative:
        /// independent species (NullTouched, Aureling, …) are NOT Dominion, so their Combat variants
        /// read neutral-cyan rather than magenta.
        /// </summary>
        internal static bool IsHostileCharacter(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = Sanitize(name);
            return n == "Maelgorn" || n.StartsWith("ObsidianRank");
        }

        // Prefab/asset-safe name: letters and digits only, others collapsed to '_'.
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "GeneratedCharacter";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            string r = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(r) ? "GeneratedCharacter" : r;
        }

        private static string Trunc(string s)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= 600 ? s : s.Substring(0, 600) + "…");

        // ---------------- prompt construction ----------------

        // The JSON schema + rules both roles share. Kept terse — the worked example carries the rest.
        private const string SchemaBlock =
            "Output ONLY a JSON object, no prose, with this exact shape:\n" +
            "{\"name\":\"PascalCaseName\",\"parts\":[{" +
            "\"name\":\"Part\",\"shape\":\"rounded|cylinder|sphere|hardcube\"," +
            "\"pos\":[x,y,z],\"scale\":[x,y,z],\"rot\":[x,y,z],\"color\":[r,g,b]}]}\n" +
            "Rules: +Y is up, +Z is forward. Colors are linear RGB in 0..1. \"rot\" is optional " +
            "(euler degrees). Prefer \"rounded\" blocks for a soft low-poly (Roblox/Minecraft) look; " +
            "use cylinder for limbs/poles and sphere for heads when it reads better.";

        /// <summary>Builds the few-shot prompt. Decorative uses a feet-at-0, ~1.8-tall humanoid
        /// frame; Combat uses the armor-on-a-torso frame the enemy rig expects (head ~y0.78,
        /// shoulders ~y0.35, skirt ~y-0.4) and tells the model NOT to add arms/hands/weapons because
        /// the code attaches the held-katana rig.</summary>
        private static string BuildCharacterPrompt(string description, CharacterRole role)
        {
            if (role == CharacterRole.Combat)
            {
                return
                    "You are a low-poly character modeler for a VR game. Build a blocky humanoid enemy " +
                    "from this description as armor/body pieces placed on an (invisible) capsule torso.\n\n" +
                    SchemaBlock + "\n\n" +
                    "FRAME (important): the torso capsule is centered near the origin; head near y=0.78, " +
                    "shoulders near y=0.35 at |x|≈0.55, chest near (0,0.15,0.32), hanging skirt near " +
                    "y=-0.4. Typical part scales are 0.2..1.1. Do NOT add arms, hands, or any weapon — " +
                    "those are added separately. Use 8..16 parts. Give the character a clear silhouette " +
                    "(helmet, shoulder plates, chest plate, skirt) and a coherent palette.\n\n" +
                    "Worked example for \"armored kabuto samurai trooper\":\n" +
                    "{\"name\":\"KabutoTrooper\",\"parts\":[" +
                    "{\"name\":\"Helmet\",\"shape\":\"sphere\",\"pos\":[0,0.78,0],\"scale\":[1.05,1.0,1.05],\"color\":[0.2,0.22,0.26]}," +
                    "{\"name\":\"HelmetBrim\",\"shape\":\"cylinder\",\"pos\":[0,0.62,0.05],\"scale\":[1.35,0.05,1.35],\"color\":[0.78,0.55,0.18]}," +
                    "{\"name\":\"SodeL\",\"shape\":\"rounded\",\"pos\":[-0.55,0.35,0],\"scale\":[0.45,0.4,0.65],\"rot\":[0,0,12],\"color\":[0.2,0.22,0.26]}," +
                    "{\"name\":\"SodeR\",\"shape\":\"rounded\",\"pos\":[0.55,0.35,0],\"scale\":[0.45,0.4,0.65],\"rot\":[0,0,-12],\"color\":[0.2,0.22,0.26]}," +
                    "{\"name\":\"DoChest\",\"shape\":\"rounded\",\"pos\":[0,0.15,0.32],\"scale\":[0.7,0.7,0.25],\"color\":[0.16,0.18,0.22]}," +
                    "{\"name\":\"ChestAccent\",\"shape\":\"rounded\",\"pos\":[0,0.15,0.38],\"scale\":[0.1,0.5,0.05],\"color\":[0.55,0.1,0.1]}," +
                    "{\"name\":\"KusazuriFront\",\"shape\":\"rounded\",\"pos\":[0,-0.4,0.35],\"scale\":[0.6,0.5,0.15],\"color\":[0.16,0.18,0.22]}," +
                    "{\"name\":\"KusazuriBack\",\"shape\":\"rounded\",\"pos\":[0,-0.4,-0.35],\"scale\":[0.6,0.5,0.15],\"color\":[0.16,0.18,0.22]}]}\n\n" +
                    "Now build: \"" + description + "\"";
            }

            return
                "You are a low-poly character modeler for a VR game. Build a blocky character from this " +
                "description as a small set of colored blocks (Roblox/Minecraft style).\n\n" +
                SchemaBlock + "\n\n" +
                "FRAME (important): the character stands with feet at y=0 and is about 1.8 tall, facing " +
                "+Z. Build legs, torso, arms, and head explicitly. Use 8..18 parts. Keep a clear, " +
                "readable silhouette and a coherent palette.\n\n" +
                "Worked example for \"a chubby green goblin merchant with a brown apron\":\n" +
                "{\"name\":\"GoblinMerchant\",\"parts\":[" +
                "{\"name\":\"LegL\",\"shape\":\"rounded\",\"pos\":[-0.18,0.35,0],\"scale\":[0.22,0.7,0.22],\"color\":[0.18,0.45,0.2]}," +
                "{\"name\":\"LegR\",\"shape\":\"rounded\",\"pos\":[0.18,0.35,0],\"scale\":[0.22,0.7,0.22],\"color\":[0.18,0.45,0.2]}," +
                "{\"name\":\"Torso\",\"shape\":\"rounded\",\"pos\":[0,0.95,0],\"scale\":[0.6,0.7,0.4],\"color\":[0.22,0.55,0.25]}," +
                "{\"name\":\"Apron\",\"shape\":\"rounded\",\"pos\":[0,0.9,0.21],\"scale\":[0.5,0.6,0.06],\"color\":[0.45,0.3,0.15]}," +
                "{\"name\":\"ArmL\",\"shape\":\"rounded\",\"pos\":[-0.4,1.0,0],\"scale\":[0.18,0.6,0.18],\"rot\":[0,0,10],\"color\":[0.2,0.5,0.22]}," +
                "{\"name\":\"ArmR\",\"shape\":\"rounded\",\"pos\":[0.4,1.0,0],\"scale\":[0.18,0.6,0.18],\"rot\":[0,0,-10],\"color\":[0.2,0.5,0.22]}," +
                "{\"name\":\"Head\",\"shape\":\"sphere\",\"pos\":[0,1.5,0],\"scale\":[0.5,0.5,0.5],\"color\":[0.3,0.6,0.3]}," +
                "{\"name\":\"EarL\",\"shape\":\"rounded\",\"pos\":[-0.28,1.55,0],\"scale\":[0.18,0.1,0.08],\"rot\":[0,0,30],\"color\":[0.3,0.6,0.3]}," +
                "{\"name\":\"EarR\",\"shape\":\"rounded\",\"pos\":[0.28,1.55,0],\"scale\":[0.18,0.1,0.08],\"rot\":[0,0,-30],\"color\":[0.3,0.6,0.3]}," +
                "{\"name\":\"NoseTip\",\"shape\":\"sphere\",\"pos\":[0,1.46,0.24],\"scale\":[0.12,0.1,0.14],\"color\":[0.32,0.62,0.32]}]}\n\n" +
                "Now build: \"" + description + "\"";
        }
    }
}
