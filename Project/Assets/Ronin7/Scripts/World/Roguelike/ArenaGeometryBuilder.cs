using System.Collections.Generic;
using Ronin7.Core;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Ronin7.World
{
    /// <summary>
    /// Runtime room-shell builder for a roguelike arena node: floor, ceiling, four walls, a handful
    /// of static accent lights, and a deterministic prop scatter. Built once — call from
    /// <see cref="RunArenaController.Start"/>, never per-frame. Every renderer is assigned the shipped
    /// URP material <see cref="ArenaRoomLibrary.surfaceMaterial"/> (Unity's default primitive material
    /// is unavailable in player builds and renders magenta) and is tinted through <see cref="RendererTint"/>
    /// (MaterialPropertyBlock) — note the SRP Batcher does NOT batch renderers carrying a
    /// MaterialPropertyBlock, so this trades SRP batching away in exchange for avoiding a
    /// per-renderer <c>.material</c> clone leak, which is the cost that actually matters for a
    /// handful of shared primitives; colliders are the primitive BoxColliders
    /// <see cref="GameObject.CreatePrimitive"/> already attaches — never a MeshCollider, and any
    /// prop prefab carrying one is stripped on instantiation (see <see cref="ScatterProps"/>).
    ///
    /// A static helper taking explicit parameters (no scene/asset lookups) so its deterministic
    /// layout (<see cref="PropPositions"/>) is unit-testable in EditMode without a loaded scene.
    /// </summary>
    public static class ArenaGeometryBuilder
    {
        /// <summary>Default room half-extent (metres) — a 24m x 24m arena a human stands in.</summary>
        public const float DefaultHalfExtent = 12f;
        public const float CeilingHeight = 4.5f;

        /// <summary>P2-09: XRI interaction-layer bit shared by the floor's TeleportationArea and the
        /// rig's teleport ray, so the UI ray (which masks it out) can never teleport the player.</summary>
        public const int TeleportLayerMask = 1 << 31;

        private const float WallThickness = 0.4f;
        private const float FloorThickness = 0.2f;
        // A5.7: Mobile_RPAsset caps additionalLightsPerObjectLimit at 4 per-pixel; 4 overlapping
        // range-9 point lights saturated it with zero headroom for boon VFX/weapon glow. Dropped to 2.
        private const int AccentLightCount = 2;
        private const float AccentLightHeight = 3f;
        private const float AccentLightRange = 9f;
        private const float AccentLightIntensity = 1.4f;
        private const int PropCount = 6;
        private const float PropWallMargin = 1.5f;

        private static readonly Color DefaultFloorColor = new Color(0.16f, 0.18f, 0.22f);
        private static readonly Color DefaultCeilColor = new Color(0.10f, 0.11f, 0.14f);
        private static readonly Color DefaultAccentColor = new Color(0.4f, 0.7f, 1f);

        /// <summary>Builds the full room shell under <paramref name="parent"/> and returns its root.
        /// <paramref name="biome"/> may be null (greybox fallback colours, no props).</summary>
        public static GameObject Build(Transform parent, float halfExtent, ArenaRoomLibrary.Biome biome, Material surfaceMaterial, ref RunRng rng)
        {
            halfExtent = Mathf.Max(1f, halfExtent);
            if (surfaceMaterial == null)
                Debug.LogError("[ArenaGeometryBuilder] ArenaRoomLibrary.surfaceMaterial is not assigned; room will render magenta on device.");
            Color floorColor = biome != null ? biome.floorColor : DefaultFloorColor;
            Color ceilColor = biome != null ? biome.ceilColor : DefaultCeilColor;
            Color accentColor = biome != null ? biome.accentColor : DefaultAccentColor;

            var root = new GameObject("ArenaGeometry");
            root.transform.SetParent(parent, false);

            BuildFloorAndCeiling(root.transform, halfExtent, floorColor, ceilColor, surfaceMaterial);
            BuildWalls(root.transform, halfExtent, floorColor, surfaceMaterial);
            BuildAccentLights(root.transform, halfExtent, accentColor);
            ScatterProps(root.transform, halfExtent, biome, ref rng);

            return root;
        }

        /// <summary>P5-05: what a baked room shell still needs from the runtime: the seeded prop scatter and
        /// ONE realtime point light for gameplay signals (Overdrive, boss telegraph), gated by
        /// <see cref="LightBudget"/>. No greybox geometry and no accent lights.</summary>
        public static GameObject BuildShellSupport(Transform parent, float halfExtent, ArenaRoomLibrary.Biome biome, ref RunRng rng)
        {
            var root = new GameObject("ArenaGeometry");
            root.transform.SetParent(parent, false);
            ScatterProps(root.transform, Mathf.Max(1f, halfExtent), biome, ref rng);

            var go = new GameObject("SignalLight");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, AccentLightHeight, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = biome != null ? biome.accentColor : DefaultAccentColor;
            light.range = AccentLightRange;
            light.intensity = AccentLightIntensity;
            light.shadows = LightShadows.None;
            light.enabled = LightBudget.ShouldAnimate(GraphicsRuntime.Quality, isGameplaySignal: true);
            return root;
        }

        private static void BuildFloorAndCeiling(Transform parent, float halfExtent, Color floorColor, Color ceilColor, Material material)
        {
            float size = halfExtent * 2f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -FloorThickness * 0.5f, 0f);
            floor.transform.localScale = new Vector3(size, FloorThickness, size);
            ApplyMaterial(floor, material);
            RendererTint.Apply(floor.GetComponent<Renderer>(), floorColor);
            // BoxCollider from CreatePrimitive stays — that's the walkable floor, and (P2-09) the
            // teleport target: only the rig's teleport ray (TeleportLayerMask) can select it.
            floor.AddComponent<TeleportationArea>().interactionLayers = TeleportLayerMask;

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(parent, false);
            ceiling.transform.localPosition = new Vector3(0f, CeilingHeight + FloorThickness * 0.5f, 0f);
            ceiling.transform.localScale = new Vector3(size, FloorThickness, size);
            ApplyMaterial(ceiling, material);
            RendererTint.Apply(ceiling.GetComponent<Renderer>(), ceilColor);
            // A5.2: Object.Destroy is illegal outside play mode (logs an error and does nothing,
            // which fails the EditMode gate on an unexpected LogError) and would otherwise ship an
            // invisible 24x24m ceiling collider that teleport arcs/raycasts hit. Disable instead —
            // nothing above the ceiling to block, and this is legal in both edit and play mode.
            var ceilingCollider = ceiling.GetComponent<Collider>();
            if (ceilingCollider != null) ceilingCollider.enabled = false;
        }

        private static void BuildWalls(Transform parent, float halfExtent, Color wallColor, Material material)
        {
            float length = halfExtent * 2f;
            float wallY = CeilingHeight * 0.5f;

            BuildWall(parent, "Wall_North", new Vector3(0f, wallY, halfExtent), new Vector3(length, CeilingHeight, WallThickness), wallColor, material);
            BuildWall(parent, "Wall_South", new Vector3(0f, wallY, -halfExtent), new Vector3(length, CeilingHeight, WallThickness), wallColor, material);
            BuildWall(parent, "Wall_East", new Vector3(halfExtent, wallY, 0f), new Vector3(WallThickness, CeilingHeight, length), wallColor, material);
            BuildWall(parent, "Wall_West", new Vector3(-halfExtent, wallY, 0f), new Vector3(WallThickness, CeilingHeight, length), wallColor, material);
        }

        private static void BuildWall(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = localScale;
            ApplyMaterial(wall, material);
            RendererTint.Apply(wall.GetComponent<Renderer>(), color);
            // BoxCollider from CreatePrimitive stays — player can't walk through.
        }

        // Assigned before RendererTint so the MPB tint lands on the URP shader's _BaseColor.
        private static void ApplyMaterial(GameObject go, Material material)
        {
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildAccentLights(Transform parent, float halfExtent, Color color)
        {
            float radius = halfExtent * 0.8f;
            for (int i = 0; i < AccentLightCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / AccentLightCount;
                var localPos = new Vector3(Mathf.Cos(angle) * radius, AccentLightHeight, Mathf.Sin(angle) * radius);

                var go = new GameObject($"AccentLight_{i}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = AccentLightRange;
                light.intensity = AccentLightIntensity;
                light.shadows = LightShadows.None; // static + no per-light shadow cost on Quest
            }
        }

        private static void ScatterProps(Transform parent, float halfExtent, ArenaRoomLibrary.Biome biome, ref RunRng rng)
        {
            GameObject[] propPrefabs = biome?.propPrefabs;
            if (propPrefabs == null || propPrefabs.Length == 0) return; // greybox fallback: no props

            List<Vector3> positions = PropPositions(halfExtent, PropCount, ref rng);
            for (int i = 0; i < positions.Count; i++)
            {
                GameObject prefab = propPrefabs[rng.NextInt(propPrefabs.Length)];
                if (prefab == null) continue;
                GameObject instance = Object.Instantiate(prefab);
                instance.transform.SetParent(parent, false);
                instance.transform.localPosition = positions[i];
                StripMeshColliders(instance);
            }
        }

        /// <summary>A5.7: CLAUDE.md forbids MeshColliders in shipped scenes, but a designer can drop
        /// a Tripo art prefab (which may carry one on import) into <see cref="ArenaRoomLibrary.Biome.propPrefabs"/>
        /// where no scene audit ever sees it. Strip and log loudly rather than silently shipping one.</summary>
        private static void StripMeshColliders(GameObject instance)
        {
            MeshCollider[] meshColliders = instance.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                Debug.LogError($"[ArenaGeometryBuilder] Prop '{instance.name}' carries a MeshCollider — stripping it (CLAUDE.md forbids MeshColliders).", instance);
                // Object.Destroy is a NO-OP outside play mode, so an edit-time caller (the arena
                // scene builder, or an EditMode test) would log the error and still ship the
                // collider. Same trap A5.2 hit on the ceiling collider — branch on the mode.
                if (Application.isPlaying) Object.Destroy(meshColliders[i]);
                else Object.DestroyImmediate(meshColliders[i]);
            }
        }

        // ---- P2-04 exit gates ----
        public const float GateWidth = 1.6f;
        public const float GateHeight = 2.6f;
        /// <summary>How far into the room (from the north wall's inner face) the pick box reaches.</summary>
        public const float GateDepth = 1.6f;
        private const float GateSpacing = 3f;

        /// <summary>Gate centre X (arena-local) for gate <paramref name="i"/> of <paramref name="count"/>,
        /// spread along the north wall and kept clear of the side walls.</summary>
        public static float GateX(int i, int count, float halfExtent)
        {
            float spacing = count > 1 ? Mathf.Min(GateSpacing, (2f * halfExtent - 2f - GateWidth) / (count - 1)) : 0f;
            return (i - (count - 1) * 0.5f) * spacing;
        }

        /// <summary>Which gate's pick box an arena-local head position (XZ) is inside, or -1. A box spans
        /// the arch's width and <see cref="GateDepth"/> in front of the north wall. Pure: polled each
        /// frame by RunArenaController, so it works for smooth walking and teleporting alike (a
        /// physics trigger would miss a teleport and needs a rigidbody on the rig).</summary>
        public static int GateIndexAt(Vector2 headXZ, int count, float halfExtent)
        {
            float innerWall = halfExtent - WallThickness * 0.5f;
            if (headXZ.y < innerWall - GateDepth) return -1;
            for (int i = 0; i < count; i++)
                if (Mathf.Abs(headXZ.x - GateX(i, count, halfExtent)) <= GateWidth * 0.5f) return i;
            return -1;
        }

        /// <summary>Raises one arch per choice on the north wall: two posts, a lintel, a glowing kind-coloured
        /// doorway and a big TextMeshPro label ("ELITE" + one-line hint). Called once, after the reward
        /// is resolved. Returns the root (destroy it to drop the gates).</summary>
        public static GameObject BuildExitGates(Transform parent, IReadOnlyList<RoomKind> choices, float halfExtent, Material surfaceMaterial)
        {
            var root = new GameObject("ExitGates");
            root.transform.SetParent(parent, false);
            int n = choices.Count;
            float z = halfExtent - WallThickness * 0.5f - 0.15f;
            for (int i = 0; i < n; i++)
            {
                RoomKind kind = choices[i];
                Color c = RoomKindText.Color(kind);
                var gate = new GameObject("Gate_" + i + "_" + kind);
                gate.transform.SetParent(root.transform, false);
                gate.transform.localPosition = new Vector3(GateX(i, n, halfExtent), 0f, z);

                float half = GateWidth * 0.5f;
                GatePart(gate.transform, "PostL", new Vector3(-half - 0.1f, GateHeight * 0.5f, 0f), new Vector3(0.2f, GateHeight, 0.3f), c * 0.6f, surfaceMaterial, true);
                GatePart(gate.transform, "PostR", new Vector3(half + 0.1f, GateHeight * 0.5f, 0f), new Vector3(0.2f, GateHeight, 0.3f), c * 0.6f, surfaceMaterial, true);
                GatePart(gate.transform, "Lintel", new Vector3(0f, GateHeight + 0.1f, 0f), new Vector3(GateWidth + 0.4f, 0.2f, 0.3f), c * 0.6f, surfaceMaterial, false);
                // Doorway: unlit and brighter than 1 so bloom gives it a glow; reads as "a way through".
                GatePart(gate.transform, "Doorway", new Vector3(0f, GateHeight * 0.5f, 0.1f), new Vector3(GateWidth, GateHeight, 0.02f), c * 1.6f, VrUiMaterials.SharedUnlit, false);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(gate.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, GateHeight + 0.75f, -0.05f);
                var tmp = labelGo.AddComponent<TextMeshPro>();
                tmp.rectTransform.sizeDelta = new Vector2(GateSpacing - 0.2f, 1.1f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.fontStyle = FontStyles.Bold;
                tmp.fontSize = 5f; // ponytail: first-guess size (~0.35 m caps), tune in-headset
                tmp.color = c;
                tmp.text = RoomKindText.Name(kind) + "\n<size=45%>" + RoomKindText.Hint(kind) + "</size>";
            }
            return root;
        }

        private static void GatePart(Transform parent, string name, Vector3 pos, Vector3 scale, Color color, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            ApplyMaterial(go, material);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            RendererTint.Apply(r, color);
            // Same edit/play-safe disable as the ceiling (A5.2): only the posts block.
            if (!solid) go.GetComponent<Collider>().enabled = false;
        }

        /// <summary>Deterministic even-ring prop layout with light per-point jitter — always inside
        /// the arena bounds by construction, so no rejection sampling is needed for cosmetic props.
        /// Pure Vector3 math; unit-testable without creating a single GameObject.</summary>
        public static List<Vector3> PropPositions(float halfExtent, int count, ref RunRng rng)
        {
            var result = new List<Vector3>(Mathf.Max(0, count));
            if (count <= 0) return result;

            float maxRadius = Mathf.Max(0.5f, halfExtent - PropWallMargin);
            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count + rng.NextFloat() * 0.3f;
                float radius = maxRadius * (0.55f + rng.NextFloat() * 0.4f);
                result.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return result;
        }
    }
}
