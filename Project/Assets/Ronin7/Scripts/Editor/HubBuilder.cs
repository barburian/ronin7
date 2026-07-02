using System.Collections.Generic;
using Ronin7.Flow;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Builds the persistent-hub half of <c>Galaxy1_Ch1_Hub</c>: the mission-launch console and the
    /// dark room-shell placeholders for chapters that unlock the hub's other rooms. No MenuItem — this
    /// is called by <see cref="XRRigBuilder.BuildChapter1Hub"/> at the end of its build, since the hub
    /// and the Ch1 mission share the one scene (see <see cref="HubStateController"/>).
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string ChapterCampaignPath = DataFolder + "/ChapterCampaign.asset";

        /// <summary>Adds the HubRoot (console + briefing point + gated future-chapter room shells) and
        /// wires a <see cref="HubStateController"/> onto <paramref name="gameRoot"/>.
        /// <paramref name="missionRoot"/> is the Ch1 "Mission" GameObject (disabled in hub mode);
        /// <paramref name="hubUnlockedDoors"/> are the startLocked door controllers the mission's
        /// Trigger steps would have activated — hub mode activates them so the hub stays traversable.</summary>
        private static void BuildHubMode(GameObject gameRoot, GameObject missionRoot, GameObject[] hubUnlockedDoors)
        {
            var hubRoot = new GameObject("HubRoot");

            // ---- Mission console: reuses BuildTransitionBox's canvas/button, swapping StoryTransition
            // for a MissionLauncher wired to the chapter campaign asset. ----
            var consoleGo = BuildTransitionBox("MissionConsole", new Vector3(6f, 1.2f, 30f), "LAUNCH NEXT MISSION",
                out var launchBtn, out var unusedTransition);
            Object.DestroyImmediate(unusedTransition); // MissionLauncher drives this button, not StoryTransition
            consoleGo.transform.SetParent(hubRoot.transform, true);
            var director = EnsureChapterCampaignDirector();
            var launcher = consoleGo.AddComponent<MissionLauncher>();
            var launcherSo = new SerializedObject(launcher);
            SetObjectRef(launcherSo, "director", director);
            launcherSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(launchBtn.onClick,
                new UnityEngine.Events.UnityAction(launcher.LaunchNext));

            // Briefing point: empty anchor for a future per-chapter briefing DialoguePlayer.
            var briefingPointGo = new GameObject("BriefingPoint");
            briefingPointGo.transform.SetParent(hubRoot.transform, false);
            briefingPointGo.transform.position = new Vector3(3f, 1f, 40f);

            // ---- Dark room-shell placeholders, one per future chapter gate. Off the Ch1 room run
            // (Ch1 occupies x[-9,9]) so nothing collides with the built hub geometry. ----
            var gatesGo = new GameObject("Gates");
            gatesGo.transform.SetParent(hubRoot.transform, false);

            var crewCommons = BuildDarkRoomShell(gatesGo.transform, "CrewCommons", new Vector3(-20f, 0f, 4f));
            Ch2FillCrewCommons(crewCommons); // Ch2 increment: table, seats, warm light, idle Resh/Iris/Mira — still gated by ch2_complete below
            var ironDojoBay = BuildDarkRoomShell(gatesGo.transform, "IronDojoBay", new Vector3(-20f, 0f, 12f));
            var archiveHolds = BuildDarkRoomShell(gatesGo.transform, "ArchiveHolds", new Vector3(-20f, 0f, 20f));
            var warRoomTable = BuildDarkRoomShell(gatesGo.transform, "WarRoomTable", new Vector3(-20f, 0f, 28f));
            var surgeryReactor = BuildDarkRoomShell(gatesGo.transform, "SurgeryReactor", new Vector3(-20f, 0f, 36f));
            var fullyLit = BuildFullyLitRoot(gatesGo.transform, new Vector3(-20f, 0f, 44f));

            // ---- HubStateController: switches the scene between the Ch1 mission and the hub, and
            // gates each future-chapter room on its own completion flag. ----
            var controller = gameRoot.AddComponent<HubStateController>();
            var controllerSo = new SerializedObject(controller);
            SetObjectRef(controllerSo, "missionModeRoot", missionRoot);
            SetObjectRef(controllerSo, "hubModeRoot", hubRoot);
            SetObjectRefList(controllerSo, "hubModeUnlocked",
                new List<Object>(hubUnlockedDoors ?? new GameObject[0]));
            var gatesProp = controllerSo.FindProperty("roomGates");
            var gateSpecs = new (string flag, GameObject root)[]
            {
                ("ch2_complete", crewCommons),
                ("ch6_complete", ironDojoBay),
                ("ch7_complete", archiveHolds),
                ("ch9_complete", warRoomTable),
                ("ch13_complete", surgeryReactor),
                ("ch16_complete", fullyLit),
            };
            gatesProp.arraySize = gateSpecs.Length;
            for (int i = 0; i < gateSpecs.Length; i++)
            {
                var gate = gatesProp.GetArrayElementAtIndex(i);
                gate.FindPropertyRelative("flag").stringValue = gateSpecs[i].flag;
                var rootsProp = gate.FindPropertyRelative("roots");
                rootsProp.arraySize = 1;
                rootsProp.GetArrayElementAtIndex(0).objectReferenceValue = gateSpecs[i].root;
            }
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            hubRoot.SetActive(false); // HubStateController.Awake sets the correct mode at runtime
        }

        /// <summary>A simple enclosed 8x8 dark room shell (floor/ceiling/4 walls + minimal dressing),
        /// centred at <paramref name="center"/>. No door — future chapter passes replace this with the
        /// real room and connect it into the hub.</summary>
        private static GameObject BuildDarkRoomShell(Transform parent, string name, Vector3 center)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = center;

            var floorColor = new Color(0.08f, 0.08f, 0.09f);
            var ceilColor = new Color(0.05f, 0.05f, 0.06f);
            var accent = new Color(0.2f, 0.21f, 0.24f);

            BuildFloorCeiling(root.transform, name, Vector3.zero, new Vector3(8f, 0f, 8f), floorColor, ceilColor);
            BuildWall(root.transform, name + "_WallN", new Vector3(0f, RoomH / 2f, 4f), new Vector3(8f, RoomH, 0.2f));
            BuildWall(root.transform, name + "_WallS", new Vector3(0f, RoomH / 2f, -4f), new Vector3(8f, RoomH, 0.2f));
            BuildWall(root.transform, name + "_WallE", new Vector3(4f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 8f));
            BuildWall(root.transform, name + "_WallW", new Vector3(-4f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 8f));
            BuildRoomDetails(root.transform, name, Vector3.zero, new Vector2(4f, 4f), accent);

            return root;
        }

        /// <summary>Ch2 increment: fills the CrewCommons dark-shell gate room (built by
        /// <see cref="BuildDarkRoomShell"/> just above) with a lightweight commons — a table, seats, a
        /// warm accent light, and the three Ch02 allies idling together. Shell geometry and the
        /// ch2_complete room-gate wiring below are unchanged.</summary>
        private static void Ch2FillCrewCommons(GameObject root)
        {
            var wood = new Color(0.32f, 0.22f, 0.14f);
            BuildProp(root.transform, "Table", new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 1.0f), wood);
            Vector3[] seatOffsets = { new Vector3(-1.1f, 0f, 0f), new Vector3(1.1f, 0f, 0f), new Vector3(0f, 0f, -0.9f) };
            foreach (var off in seatOffsets)
                BuildProp(root.transform, "Seat", off + new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.44f, 0.4f), wood * 1.2f);

            var lightGo = new GameObject("CommonsLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.intensity = 2f;
            light.range = 8f;
            light.shadows = LightShadows.None;

            (string prefab, Vector3 pos, string name)[] cast =
            {
                (Ch2ReshPrefab, root.transform.position + new Vector3(-1.6f, 0f, 1.2f), "Resh"),
                (Ch2IrisPrefab, root.transform.position + new Vector3(1.6f, 0f, 1.2f), "Iris"),
                (Ch2MiraPrefab, root.transform.position + new Vector3(0f, 0f, 1.8f), "Mira"),
            };
            foreach (var (prefab, pos, name) in cast)
            {
                var npc = InstantiateNpc(prefab, pos, name);
                FitNamedCharacter(npc);
                if (npc == null) continue;
                // InstantiateNpc leaves instances at scene root; parent under the room so the
                // ch2_complete gate (and the hub/mission mode toggle) actually covers them.
                npc.transform.SetParent(root.transform, true);
                var storyNpc = npc.AddComponent<StoryNpc>();
                var so = new SerializedObject(storyNpc);
                so.FindProperty("displayName").stringValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>ch16's gate: not a room shell but an empty root holding a few bright lights (an
        /// unsubtle "fully lit" marker until the real Ch16 pass replaces it).</summary>
        private static GameObject BuildFullyLitRoot(Transform parent, Vector3 center)
        {
            var root = new GameObject("FullyLit");
            root.transform.SetParent(parent, false);
            root.transform.position = center;

            Vector3[] offsets = { new Vector3(-2f, 2.6f, -2f), new Vector3(2f, 2.6f, -2f), new Vector3(0f, 2.6f, 2f) };
            foreach (var offset in offsets)
            {
                var lightGo = new GameObject("BrightLight");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = offset;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = Color.white;
                l.intensity = 4f;
                l.range = 12f;
                l.shadows = LightShadows.None;
            }

            return root;
        }

        /// <summary>Creates or updates the Data/ChapterCampaign.asset CampaignDirector with the 13
        /// chapter missions in play order. Ch1 is absent by design — it is the hub scene itself, and its
        /// completion is flag-keyed (ch1_complete), not scene-keyed.</summary>
        private static CampaignDirector EnsureChapterCampaignDirector()
        {
            var missions = new[]
            {
                new CampaignDirector.Mission { id = "CH02", entryScene = "Ch02_Auction" },
                new CampaignDirector.Mission { id = "CH03", entryScene = "Ch03_SwordRemembers" },
                new CampaignDirector.Mission { id = "CH04", entryScene = "Ch04_OverseersHunt" },
                new CampaignDirector.Mission { id = "CH05", entryScene = "Ch05_DebtOfAshes" },
                new CampaignDirector.Mission { id = "CH06", entryScene = "Ch06_IronDojo" },
                new CampaignDirector.Mission { id = "CH07", entryScene = "Ch07_ForgottenNames" },
                new CampaignDirector.Mission { id = "CH08", entryScene = "Ch08_SilentGarden" },
                new CampaignDirector.Mission { id = "CH09", entryScene = "Ch09_PitAndTheDeep" },
                new CampaignDirector.Mission { id = "CH10", entryScene = "Ch10_LedgerOfRust" },
                new CampaignDirector.Mission { id = "CH11", entryScene = "Ch11_GhostsAndOrigins" },
                new CampaignDirector.Mission { id = "CH12", entryScene = "Ch12_TheFracture" },
                new CampaignDirector.Mission { id = "CH13", entryScene = "Ch13_SterileReckoning" },
                new CampaignDirector.Mission { id = "CH16", entryScene = "Ch16_ThroneOfAshes" },
            };

            var director = AssetDatabase.LoadAssetAtPath<CampaignDirector>(ChapterCampaignPath);
            if (director == null)
            {
                EnsureFolder(DataFolder);
                director = ScriptableObject.CreateInstance<CampaignDirector>();
                AssetDatabase.CreateAsset(director, ChapterCampaignPath);
            }

            var so = new SerializedObject(director);
            var missionsProp = so.FindProperty("missions");
            missionsProp.arraySize = missions.Length;
            for (int i = 0; i < missions.Length; i++)
            {
                var m = missionsProp.GetArrayElementAtIndex(i);
                m.FindPropertyRelative("id").stringValue = missions[i].id;
                m.FindPropertyRelative("entryScene").stringValue = missions[i].entryScene;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return director;
        }
    }
}
