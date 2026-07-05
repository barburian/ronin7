using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Applies the <see cref="XRRigBuilder.PickRoomDetailArchetypes"/> room-detail variety additively to
    /// every already-shipped scene whose rooms were built by <see cref="XRRigBuilder.BuildRoomDetails"/>
    /// before the archetypes existed. Those scenes were built with only the identical 3-prop base template
    /// (crate cluster + console + ceiling pipe), and the project's chapter Build menu items destructively
    /// NewScene-rebuild — never to be re-run against a shipped scene — so this utility opens each scene
    /// directly and adds the new content in place instead (mirrors <see cref="ChapterVoiceWirer"/> /
    /// <see cref="XRRigBuilder.WireEnemyVarietyIntoScenes"/>'s "open -> modify -> save" idiom).
    ///
    /// Room parameters (center/half-extents/accent) below are copied verbatim from the
    /// <c>BuildRoomDetails(...)</c> call sites in each ChapterNBuilder / HubBuilder, so a future rebuild of
    /// any of these scenes reproduces this exactly. Each room is located in the live scene by its
    /// "{room}_Console" child (built unconditionally by BuildRoomDetails for every room), so this utility
    /// doesn't need to know the scene's exact GameObject hierarchy.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private struct RoomDetailSpec
        {
            public string Name;
            public Vector3 Center;
            public Vector2 HalfExtents;
            public Color Accent;

            public RoomDetailSpec(string name, Vector3 center, Vector2 half, Color accent)
            {
                Name = name;
                Center = center;
                HalfExtents = half;
                Accent = accent;
            }
        }

        // Scene file name -> every BuildRoomDetails room it contains. Only scenes that actually used the
        // template are listed (found by grepping for "_Crate0"/"_Console"/"_Pipe" across the shipped
        // scenes) — chapters 5, 7-13, 16 dress their rooms with bespoke props instead and are unaffected.
        private static readonly (string SceneFile, RoomDetailSpec[] Rooms)[] RoomDetailScenes =
        {
            ("Galaxy1_Ch1_Hub.unity", new[]
            {
                // Chapter1Builder (ship interior).
                new RoomDetailSpec("Medbay", new Vector3(0f, 0f, 0f), new Vector2(4f, 4f), new Color(0.5f, 0.55f, 0.6f)),
                new RoomDetailSpec("Hold", new Vector3(2f, 0f, 10f), new Vector2(3.5f, 5.5f), new Color(0.3f, 0.32f, 0.36f)),
                new RoomDetailSpec("Command", new Vector3(0f, 0f, 30f), new Vector2(8f, 3.5f), new Color(0.33f, 0.4f, 0.5f)),
                // HubBuilder.BuildDarkRoomShell gate rooms (local center is always Vector3.zero — the room
                // root itself is positioned at the world center).
                new RoomDetailSpec("CrewCommons", Vector3.zero, new Vector2(4f, 4f), new Color(0.2f, 0.21f, 0.24f)),
                new RoomDetailSpec("IronDojoBay", Vector3.zero, new Vector2(4f, 4f), new Color(0.2f, 0.21f, 0.24f)),
                new RoomDetailSpec("ArchiveHolds", Vector3.zero, new Vector2(4f, 4f), new Color(0.2f, 0.21f, 0.24f)),
                new RoomDetailSpec("WarRoomTable", Vector3.zero, new Vector2(4f, 4f), new Color(0.2f, 0.21f, 0.24f)),
                new RoomDetailSpec("SurgeryReactor", Vector3.zero, new Vector2(4f, 4f), new Color(0.2f, 0.21f, 0.24f)),
            }),
            ("Ch02_Auction.unity", new[]
            {
                new RoomDetailSpec("Alley", new Vector3(0f, 0f, 0f), new Vector2(4f, 4f), new Color(0.35f, 0.32f, 0.3f)),
                new RoomDetailSpec("Market", new Vector3(0f, 0f, 12f), new Vector2(7f, 8f), new Color(0.5f, 0.2f, 0.35f)),
                new RoomDetailSpec("BrokerFront", new Vector3(0f, 0f, 25f), new Vector2(5f, 5f), new Color(0.25f, 0.45f, 0.3f)),
                new RoomDetailSpec("Auction", new Vector3(0f, 0f, 39f), new Vector2(9f, 9f), new Color(0.5f, 0.45f, 0.15f)),
                new RoomDetailSpec("Cells", new Vector3(0f, 0f, 55f), new Vector2(6f, 7f), new Color(0.2f, 0.3f, 0.32f)),
                new RoomDetailSpec("Vault", new Vector3(0f, 0f, 68f), new Vector2(5f, 6f), new Color(0.25f, 0.5f, 0.55f)),
                new RoomDetailSpec("Dock", new Vector3(0f, 0f, 83f), new Vector2(6f, 9f), new Color(0.4f, 0.36f, 0.3f)),
            }),
            ("Ch03_SwordRemembers.unity", new[]
            {
                new RoomDetailSpec("Hold", new Vector3(0f, 0f, 3f), new Vector2(5f, 7f), new Color(0.38f, 0.3f, 0.24f)),
            }),
            ("Ch04_OverseersHunt.unity", new[]
            {
                new RoomDetailSpec("Throat", new Vector3(0f, 0f, 6f), new Vector2(7f, 10f), new Color(0.4f, 0.35f, 0.5f)),
                new RoomDetailSpec("Sink", new Vector3(0f, 0f, 29f), new Vector2(9f, 13f), new Color(0.5f, 0.2f, 0.4f)),
                new RoomDetailSpec("Mast", new Vector3(0f, 0f, 51f), new Vector2(5f, 9f), new Color(0.3f, 0.4f, 0.6f)),
                new RoomDetailSpec("KerraxHold", new Vector3(0f, 0f, 122f), new Vector2(7f, 10f), new Color(0.2f, 0.35f, 0.55f)),
            }),
            ("Ch06_IronDojo.unity", new[]
            {
                new RoomDetailSpec("MorriganSpine", new Vector3(0f, 0f, 92f), new Vector2(6f, 8f), new Color(0.35f, 0.4f, 0.55f)),
                // Ch6BuildTowerArena local centers, relative to the shared "upper" (UpperCitadel) parent.
                new RoomDetailSpec("Cradle", new Vector3(-46f, 0f, 111f), new Vector2(8f, 8f), new Color(0.45f, 0.35f, 0.4f)),
                new RoomDetailSpec("Vesting", new Vector3(46f, 0f, 111f), new Vector2(8f, 8f), new Color(0.3f, 0.35f, 0.45f)),
                new RoomDetailSpec("Proving", new Vector3(0f, 0f, 150f), new Vector2(8f, 8f), new Color(0.4f, 0.3f, 0.25f)),
            }),
        };

        [MenuItem("Tools/Space Samurai/Chapters/Wire Room Detail Variety Into Existing Scenes")]
        public static void WireRoomDetailVarietyIntoScenes()
        {
            int totalRooms = 0, totalAdded = 0;
            foreach (var (sceneFile, rooms) in RoomDetailScenes)
            {
                totalAdded += WireRoomDetailVarietyIntoScene(sceneFile, rooms);
                totalRooms += rooms.Length;
            }
            Debug.Log($"[ChapterRoomDetailsVarietyWirer] Done: {totalAdded} new props across {totalRooms} rooms in {RoomDetailScenes.Length} scenes.");
        }

        /// <summary>Opens one scene, adds each room's hash-selected extra props (skipping any already
        /// present), saves it. Returns the number of new props added.</summary>
        private static int WireRoomDetailVarietyIntoScene(string sceneFileName, RoomDetailSpec[] rooms)
        {
            string scenePath = $"{SceneFolder}/{sceneFileName}";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int added = 0;
            foreach (var room in rooms)
            {
                var consoleT = FindTransformIncludingInactive(room.Name + "_Console");
                if (consoleT == null)
                {
                    Debug.LogWarning($"[ChapterRoomDetailsVarietyWirer] {sceneFileName}: '{room.Name}_Console' not found — skipped.");
                    continue;
                }

                foreach (int archetype in PickRoomDetailArchetypes(room.Name))
                {
                    string marker = room.Name + RoomDetailArchetypeMarkerSuffix(archetype);
                    if (consoleT.parent.Find(marker) != null) continue; // idempotent — already wired

                    BuildRoomDetailArchetype(archetype, consoleT.parent, room.Name, room.Center, room.HalfExtents, room.Accent);
                    added += RoomDetailArchetypeRendererCount(archetype);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ChapterRoomDetailsVarietyWirer] {sceneFileName}: {added} new props across {rooms.Length} rooms.");
            return added;
        }

        /// <summary>Finds a Transform by exact name anywhere in the currently loaded scene(s), including
        /// inactive GameObjects (the Hub's gameplay content starts inactive — see HubBuilder — so plain
        /// GameObject.Find would miss it).</summary>
        private static Transform FindTransformIncludingInactive(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) return t;
            return null;
        }
    }
}
