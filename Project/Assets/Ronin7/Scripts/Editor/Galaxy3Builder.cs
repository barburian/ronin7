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
    /// Galaxy 3 solar-system builder (EP17 sessions). Builds the industrial/rust-palette hub with
    /// 8 canon worlds: Rustfang's Cradle (landable), Meridian-5, The Rust Meridian, Kethis Prime
    /// (with asteroid belt), Narcosis, Arkship Meridian, Thermopause, and Meridian-7.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths for Galaxy 3 and its connected episode scenes (EP17).
        private const string Galaxy3ScenePath = SceneFolder + "/Galaxy3.unity";
        private const string Galaxy3Ep17SalvageYardScenePath = SceneFolder + "/Galaxy3_EP17_SalvageYard.unity";
        private const string Galaxy3Ep17ArenaScenePath = SceneFolder + "/Galaxy3_EP17_Arena.unity";
        private const string Galaxy3Ep17LowerPitScenePath = SceneFolder + "/Galaxy3_EP17_LowerPit.unity";
        private const string Galaxy3Ep17VaultScenePath = SceneFolder + "/Galaxy3_EP17_Vault.unity";
        private const string Galaxy3Ep17EscapeScenePath = SceneFolder + "/Galaxy3_EP17_Escape.unity";
        private const string Galaxy3Ep17SurfaceDuelScenePath = SceneFolder + "/Galaxy3_EP17_SurfaceDuel.unity";
        private const string Galaxy3Ep18StationScenePath = SceneFolder + "/Galaxy3_EP18_Station.unity";
        private const string Galaxy3Ep18VaultOuterScenePath = SceneFolder + "/Galaxy3_EP18_VaultOuter.unity";
        private const string Galaxy3Ep18ExtractionScenePath = SceneFolder + "/Galaxy3_EP18_ExtractionChamber.unity";
        private const string Galaxy3Ep18CommandCoreScenePath = SceneFolder + "/Galaxy3_EP18_CommandCore.unity";
        private const string Galaxy3Ep18DockScenePath = SceneFolder + "/Galaxy3_EP18_Dock.unity";
        private const string Galaxy3Ep18HyperspaceScenePath = SceneFolder + "/Galaxy3_EP18_Hyperspace.unity";
        private const string Galaxy3Ep19CargoHoldScenePath = SceneFolder + "/Galaxy3_EP19_CargoHold.unity";
        private const string Galaxy3Ep19AsteroidPursuitScenePath = SceneFolder + "/Galaxy3_EP19_AsteroidPursuit.unity";
        private const string Galaxy3Ep19MineShaftScenePath = SceneFolder + "/Galaxy3_EP19_MineShaft.unity";
        private const string Galaxy3Ep19VaultBelowScenePath = SceneFolder + "/Galaxy3_EP19_VaultBelow.unity";
        private const string Galaxy3Ep19AshAndVoidScenePath = SceneFolder + "/Galaxy3_EP19_AshAndVoid.unity";
        private const string Galaxy3Ep19DarkCorridorsScenePath = SceneFolder + "/Galaxy3_EP19_DarkCorridors.unity";
        private const string Galaxy3Ep19CorsairScenePath = SceneFolder + "/Galaxy3_EP19_Corsair.unity";
        private const string Galaxy3Ep20DockingBayScenePath = SceneFolder + "/Galaxy3_EP20_DockingBay.unity";
        private const string Galaxy3Ep20ChemicalSectorScenePath = SceneFolder + "/Galaxy3_EP20_ChemicalSector.unity";
        private const string Galaxy3Ep20VarekShardVaultScenePath = SceneFolder + "/Galaxy3_EP20_VarekShardVault.unity";
        private const string Galaxy3Ep20CentralVaultScenePath = SceneFolder + "/Galaxy3_EP20_CentralVault.unity";
        private const string Galaxy3Ep20DominionInterdictionScenePath = SceneFolder + "/Galaxy3_EP20_DominionInterdiction.unity";
        private const string Galaxy3Ep20RecordsRoomScenePath = SceneFolder + "/Galaxy3_EP20_RecordsRoom.unity";
        private const string Galaxy3Ep20HyperspaceScenePath = SceneFolder + "/Galaxy3_EP20_Hyperspace.unity";
        private const string Galaxy3Ep21NarcosisDescentScenePath = SceneFolder + "/Galaxy3_EP21_NarcosisDescent.unity";
        private const string Galaxy3Ep21ForgettingDescentScenePath = SceneFolder + "/Galaxy3_EP21_ForgettingDescent.unity";
        private const string Galaxy3Ep21GardenOfEchoesScenePath = SceneFolder + "/Galaxy3_EP21_GardenOfEchoes.unity";
        private const string Galaxy3Ep21TestimonyScenePath = SceneFolder + "/Galaxy3_EP21_Testimony.unity";
        private const string Galaxy3Ep21ExpulsionScenePath = SceneFolder + "/Galaxy3_EP21_Expulsion.unity";
        private const string Galaxy3Ep21ShadowInCodeScenePath = SceneFolder + "/Galaxy3_EP21_ShadowInCode.unity";
        private const string Galaxy3Ep21AwakeningScenePath = SceneFolder + "/Galaxy3_EP21_Awakening.unity";
        private const string Galaxy3Ep22CargoApproachScenePath = SceneFolder + "/Galaxy3_EP22_CargoApproach.unity";
        private const string Galaxy3Ep22VaultOfGhostsScenePath = SceneFolder + "/Galaxy3_EP22_VaultOfGhosts.unity";
        private const string Galaxy3Ep22ChildrenBelowScenePath = SceneFolder + "/Galaxy3_EP22_ChildrenBelow.unity";
        private const string Galaxy3Ep22WhatRonin1LeftScenePath = SceneFolder + "/Galaxy3_EP22_WhatRonin1Left.unity";
        private const string Galaxy3Ep22YoungerChainScenePath = SceneFolder + "/Galaxy3_EP22_YoungerChain.unity";
        private const string Galaxy3Ep22BroadcastScenePath = SceneFolder + "/Galaxy3_EP22_Broadcast.unity";
        private const string Galaxy3Ep22StellarDiveScenePath = SceneFolder + "/Galaxy3_EP22_StellarDive.unity";
        private const string Galaxy3Ep23SignalApproachScenePath = SceneFolder + "/Galaxy3_EP23_SignalApproach.unity";
        private const string Galaxy3Ep23IceBelowScenePath = SceneFolder + "/Galaxy3_EP23_IceBelow.unity";
        private const string Galaxy3Ep23CryptBelowScenePath = SceneFolder + "/Galaxy3_EP23_CryptBelow.unity";
        private const string Galaxy3Ep23TheCommanderScenePath = SceneFolder + "/Galaxy3_EP23_TheCommander.unity";
        private const string Galaxy3Ep23DefectiveGenerationScenePath = SceneFolder + "/Galaxy3_EP23_DefectiveGeneration.unity";
        private const string Galaxy3Ep23TheWakeScenePath = SceneFolder + "/Galaxy3_EP23_TheWake.unity";
        private const string Galaxy3Ep23GhostFleetScenePath = SceneFolder + "/Galaxy3_EP23_GhostFleet.unity";
        private const string Galaxy3Ep24HowManyScenePath = SceneFolder + "/Galaxy3_EP24_HowMany.unity";
        private const string Galaxy3Ep24RetirementColonyScenePath = SceneFolder + "/Galaxy3_EP24_RetirementColony.unity";
        private const string Galaxy3Ep24ThirtyYearsScenePath = SceneFolder + "/Galaxy3_EP24_ThirtyYears.unity";
        private const string Galaxy3Ep24KhallsMercyScenePath = SceneFolder + "/Galaxy3_EP24_KhallsMercy.unity";
        private const string Galaxy3Ep24ShatteredProtocolScenePath = SceneFolder + "/Galaxy3_EP24_ShatteredProtocol.unity";
        private const string Galaxy3Ep24LightSeparateScenePath = SceneFolder + "/Galaxy3_EP24_LightSeparate.unity";

        private static readonly string Galaxy3SceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3ScenePath);
        private static readonly string Galaxy3Ep17SalvageYardSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep17SalvageYardScenePath);
        private static readonly string Galaxy3Ep17ArenaSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep17ArenaScenePath);
        private static readonly string Galaxy3Ep17LowerPitSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep17LowerPitScenePath);
        private static readonly string Galaxy3Ep17VaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep17VaultScenePath);
        private static readonly string Galaxy3Ep17EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep17EscapeScenePath);
        private static readonly string Galaxy3Ep17SurfaceDuelSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep17SurfaceDuelScenePath);
        private static readonly string Galaxy3Ep18StationSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep18StationScenePath);
        private static readonly string Galaxy3Ep18VaultOuterSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep18VaultOuterScenePath);
        private static readonly string Galaxy3Ep18ExtractionSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep18ExtractionScenePath);
        private static readonly string Galaxy3Ep18CommandCoreSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep18CommandCoreScenePath);
        private static readonly string Galaxy3Ep18DockSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep18DockScenePath);
        private static readonly string Galaxy3Ep18HyperspaceSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep18HyperspaceScenePath);
        private static readonly string Galaxy3Ep19CargoHoldSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19CargoHoldScenePath);
        private static readonly string Galaxy3Ep19AsteroidPursuitSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19AsteroidPursuitScenePath);
        private static readonly string Galaxy3Ep19MineShaftSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19MineShaftScenePath);
        private static readonly string Galaxy3Ep19VaultBelowSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19VaultBelowScenePath);
        private static readonly string Galaxy3Ep19AshAndVoidSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19AshAndVoidScenePath);
        private static readonly string Galaxy3Ep19DarkCorridorsSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19DarkCorridorsScenePath);
        private static readonly string Galaxy3Ep19CorsairSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep19CorsairScenePath);
        private static readonly string Galaxy3Ep20DockingBaySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20DockingBayScenePath);
        private static readonly string Galaxy3Ep20ChemicalSectorSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20ChemicalSectorScenePath);
        private static readonly string Galaxy3Ep20VarekShardVaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20VarekShardVaultScenePath);
        private static readonly string Galaxy3Ep20CentralVaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20CentralVaultScenePath);
        private static readonly string Galaxy3Ep20DominionInterdictionSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20DominionInterdictionScenePath);
        private static readonly string Galaxy3Ep20RecordsRoomSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20RecordsRoomScenePath);
        private static readonly string Galaxy3Ep20HyperspaceSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep20HyperspaceScenePath);
        private static readonly string Galaxy3Ep21NarcosisDescentSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21NarcosisDescentScenePath);
        private static readonly string Galaxy3Ep21ForgettingDescentSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21ForgettingDescentScenePath);
        private static readonly string Galaxy3Ep21GardenOfEchoesSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21GardenOfEchoesScenePath);
        private static readonly string Galaxy3Ep21TestimonySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21TestimonyScenePath);
        private static readonly string Galaxy3Ep21ExpulsionSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21ExpulsionScenePath);
        private static readonly string Galaxy3Ep21ShadowInCodeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21ShadowInCodeScenePath);
        private static readonly string Galaxy3Ep21AwakeningSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep21AwakeningScenePath);
        private static readonly string Galaxy3Ep22CargoApproachSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22CargoApproachScenePath);
        private static readonly string Galaxy3Ep22VaultOfGhostsSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22VaultOfGhostsScenePath);
        private static readonly string Galaxy3Ep22ChildrenBelowSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22ChildrenBelowScenePath);
        private static readonly string Galaxy3Ep22WhatRonin1LeftSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22WhatRonin1LeftScenePath);
        private static readonly string Galaxy3Ep22YoungerChainSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22YoungerChainScenePath);
        private static readonly string Galaxy3Ep22BroadcastSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22BroadcastScenePath);
        private static readonly string Galaxy3Ep22StellarDiveSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep22StellarDiveScenePath);
        private static readonly string Galaxy3Ep23SignalApproachSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23SignalApproachScenePath);
        private static readonly string Galaxy3Ep23IceBelowSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23IceBelowScenePath);
        private static readonly string Galaxy3Ep23CryptBelowSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23CryptBelowScenePath);
        private static readonly string Galaxy3Ep23TheCommanderSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23TheCommanderScenePath);
        private static readonly string Galaxy3Ep23DefectiveGenerationSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23DefectiveGenerationScenePath);
        private static readonly string Galaxy3Ep23TheWakeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23TheWakeScenePath);
        private static readonly string Galaxy3Ep23GhostFleetSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep23GhostFleetScenePath);
        private static readonly string Galaxy3Ep24HowManySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep24HowManyScenePath);
        private static readonly string Galaxy3Ep24RetirementColonySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep24RetirementColonyScenePath);
        private static readonly string Galaxy3Ep24ThirtyYearsSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep24ThirtyYearsScenePath);
        private static readonly string Galaxy3Ep24KhallsMercySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep24KhallsMercyScenePath);
        private static readonly string Galaxy3Ep24ShatteredProtocolSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep24ShatteredProtocolScenePath);
        private static readonly string Galaxy3Ep24LightSeparateSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy3Ep24LightSeparateScenePath);

        /// <summary>
        /// Encloses the Galaxy 3 cockpit in a solid cabin with a front windshield and adds
        /// auto-playing briefings wired to a BriefingSelector. Simplified for single episode (EP17).
        /// </summary>
        private static void BuildGalaxy3Cabin(Transform cockpit)
        {
            var hull = new Color(0.16f, 0.17f, 0.20f);
            var floorC = new Color(0.13f, 0.14f, 0.17f);

            // Cabin shell: floor, ceiling, back wall, side walls.
            AddVisualTinted(cockpit, "CabinFloor", new Vector3(0f, -0.05f, -0.4f), new Vector3(3.2f, 0.1f, 3.4f), PrimitiveType.Cube, floorC);
            AddVisualTinted(cockpit, "CabinCeiling", new Vector3(0f, 2.45f, -0.4f), new Vector3(3.2f, 0.1f, 3.4f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinBack", new Vector3(0f, 1.2f, -2.05f), new Vector3(3.2f, 2.6f, 0.1f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinWallL", new Vector3(-1.55f, 1.2f, -0.4f), new Vector3(0.1f, 2.6f, 3.4f), PrimitiveType.Cube, hull);
            AddVisualTinted(cockpit, "CabinWallR", new Vector3(1.55f, 1.2f, -0.4f), new Vector3(0.1f, 2.6f, 3.4f), PrimitiveType.Cube, hull);

            // Sleek wraparound glass-bubble canopy (slim dark frame + cyan neon edge-light).
            BuildGlassCanopyFrame(cockpit, hull, 1.55f);

            // Cabin light.
            BuildAccentPointLight("CabinLight", new Vector3(0f, 2.2f, -0.3f),
                new Color(1f, 0.88f, 0.7f), intensity: 2.0f, range: 6f);

            // Enemy warning system.
            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Pre-mission briefing (EP17 cabin briefing from Ep17Lines).
            var preMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep17Briefing", Vector3.zero,
                Ep17Lines.Get("cabin_brief"), null, "cabin_brief", "ep17");
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
            dpSo.FindProperty("playOnStart").boolValue = false; // Controlled by BriefingSelector
            dpSo.ApplyModifiedPropertiesWithoutUndo();

            // Post-mission briefing (EP17 space outro from Ep17Lines).
            var postMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep17Post", Vector3.zero,
                Ep17Lines.Get("space_ep17_post"), null, "space_ep17_post", "ep17");
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

            // Post-mission briefing (EP18 space outro from Ep18Lines).
            var postMission2Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep18Post", Vector3.zero,
                Ep18Lines.Get("space_ep18_post"), null, "space_ep18_post", "ep18");
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

            // Post-mission briefing (EP19 space outro from Ep19Lines).
            var postMission3Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep19Post", Vector3.zero,
                Ep19Lines.Get("space_ep19_post"), null, "space_ep19_post", "ep19");
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

            // Post-mission briefing (EP20 space outro from Ep20Lines).
            var postMission4Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep20Post", Vector3.zero,
                Ep20Lines.Get("space_ep20_post"), null, "space_ep20_post", "ep20");
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

            // Post-mission briefing (EP21 hub outro from Ep21Lines).
            var postMission5Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep21Post", Vector3.zero,
                Ep21Lines.Get("space_ep21_post"), null, "space_ep21_post", "ep21");
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

            // Post-mission briefing (EP22 hub outro from Ep22Lines).
            var postMission6Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep22Post", Vector3.zero,
                Ep22Lines.Get("space_ep22_post"), null, "space_ep22_post", "ep22");
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

            // Post-mission briefing (EP23 hub outro from Ep23Lines).
            var postMission7Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep23Post", Vector3.zero,
                Ep23Lines.Get("space_ep23_post"), null, "space_ep23_post", "ep23");
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

            // Post-mission briefing (EP24 hub outro from Ep24Lines — Galaxy 3 finale).
            var postMission8Dialogue = BuildDialoguePlayer("Dialogue_Galaxy3Ep24Post", Vector3.zero,
                Ep24Lines.Get("space_ep24_post"), null, "space_ep24_post", "ep24");
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

            // Wire the BriefingSelector: preMission + postMission (EP17) + postMission2 (EP18) + postMission3 (EP19) + planet scenes.
            var selectorGo = new GameObject("BriefingSelector");
            selectorGo.transform.SetParent(cockpit, false);
            var selector = selectorGo.AddComponent<BriefingSelector>();
            var selSo = new SerializedObject(selector);
            SetObjectRef(selSo, "preMission", preMissionDialogue);
            SetObjectRef(selSo, "postMission", postMissionDialogue);
            selSo.FindProperty("planetScene").stringValue = Galaxy3Ep17SurfaceDuelSceneName;
            SetObjectRef(selSo, "postMission2", postMission2Dialogue);
            selSo.FindProperty("planetScene2").stringValue = Galaxy3Ep18StationSceneName;
            SetObjectRef(selSo, "postMission3", postMission3Dialogue);
            selSo.FindProperty("planetScene3").stringValue = Galaxy3Ep19CargoHoldSceneName;
            SetObjectRef(selSo, "postMission4", postMission4Dialogue);
            selSo.FindProperty("planetScene4").stringValue = Galaxy3Ep20DockingBaySceneName;
            SetObjectRef(selSo, "postMission5", postMission5Dialogue);
            selSo.FindProperty("planetScene5").stringValue = Galaxy3Ep21NarcosisDescentSceneName;
            SetObjectRef(selSo, "postMission6", postMission6Dialogue);
            selSo.FindProperty("planetScene6").stringValue = Galaxy3Ep22CargoApproachSceneName;
            SetObjectRef(selSo, "postMission7", postMission7Dialogue);
            selSo.FindProperty("planetScene7").stringValue = Galaxy3Ep23SignalApproachSceneName;
            SetObjectRef(selSo, "postMission8", postMission8Dialogue);
            selSo.FindProperty("planetScene8").stringValue = Galaxy3Ep24HowManySceneName;
            selSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds the Galaxy 3 SPACE flight scene: the industrial/rust-palette hub for EP17, orbiting
        /// 8 canon worlds with Rustfang's Cradle as the landable EP17 destination. The Corsair
        /// is dockable as a fallback.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 3/Build Galaxy 3 Space Scene", priority = 80)]
        public static void BuildGalaxy3Scene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Industrial/rust-tinted space with warm ember lighting.
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.025f, 0.02f);
            RenderSettings.skybox = EnsureBlackSkybox();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);

            var gameGo = new GameObject("Game");
            var gs = gameGo.AddComponent<GameState>();
            var gsSo = new SerializedObject(gs);
            var sm = gsSo.FindProperty("startMode");
            if (sm != null) sm.enumValueIndex = (int)GameMode.SpaceFlight;
            gsSo.ApplyModifiedPropertiesWithoutUndo();

            // Seated flight rig, no locomotion.
            var rig = BuildRig(refs, addLocomotion: false);

            var vrRig = rig.GetComponent<VRRig>();
            var cam = vrRig != null && vrRig.Head != null ? vrRig.Head.GetComponent<Camera>() : null;
            if (cam != null) cam.farClipPlane = 6000f;

            if (rig.GetComponent<Health>() == null) rig.AddComponent<Health>();
            rig.AddComponent<PlayerShipDamageRelay>();

            var hull = new GameObject("Ship Hull (Damage Volume)");
            hull.transform.SetParent(rig.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hullCol = hull.AddComponent<SphereCollider>();
            hullCol.radius = 1.2f;
            hullCol.isTrigger = false;

            var cockpit = new GameObject("Cockpit").transform;
            BuildCockpit(cockpit);

            var recenter = cockpit.gameObject.AddComponent<CockpitRecenter>();
            var rcSo = new SerializedObject(recenter);
            SetObjectRef(rcSo, "recenterAction", FindRef(refs, "Right Hand", "Recenter Cockpit"));
            SetObjectRef(rcSo, "head", vrRig != null ? vrRig.Head : null);
            SetObjectRef(rcSo, "cockpit", cockpit);
            rcSo.ApplyModifiedPropertiesWithoutUndo();

            // Ship hull selector (same as Galaxy1/2).
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

            // Build the Galaxy 3 cabin with EP17 briefings.
            BuildGalaxy3Cabin(cockpit);

            // Universe: ember sun + 8 canon worlds.
            var universe = new GameObject("Universe").transform;
            Vector3 sunCenter = new Vector3(0f, 0f, 1500f);
            var sunVisual = BuildEmberSun(universe, light, sunCenter, 400f);

            var aimer = lightGo.AddComponent<SunLightAimer>();
            var aimerSo = new SerializedObject(aimer);
            SetObjectRef(aimerSo, "sunLight", light);
            SetObjectRef(aimerSo, "sunVisual", sunVisual.transform);
            aimerSo.ApplyModifiedPropertiesWithoutUndo();

            // World 1: Rustfang's Cradle — the landable anchor planet for EP17, rust-brown rocky at 850u orbit.
            Vector3 rustfangPos = OrbitPos(sunCenter, 850f, 0f, 0f);
            var rustfang = BuildCanonPlanet(universe, "Rustfang's Cradle",
                rustfangPos, 120f, new Color(0.45f, 0.30f, 0.22f), "Rocky");
            AddSurfacePatches(rustfang, 6, new Color(0.30f, 0.20f, 0.15f), 60000001);
            AddCityLights(rustfang, 10, new Color(1f, 0.55f, 0.2f), 60000002);
            AddPlanetOrbit(rustfang, sunVisual.transform, 850f, 0.22f, 0f, 0f);

            // World 2: Thalassa-5 — icy-blue planet at 1150u orbit (EP18 landing).
            Vector3 meridian5Pos = OrbitPos(sunCenter, 1150f, 40f, 0f);
            var meridian5 = BuildCanonPlanet(universe, "Thalassa-5",
                meridian5Pos, 120f, new Color(0.55f, 0.75f, 0.85f), "Ice");
            AddPolarCaps(meridian5, Color.white);
            AddCloudShell(meridian5, new Color(0.8f, 0.9f, 1f), 0.25f);
            AddPlanetOrbit(meridian5, sunVisual.transform, 1150f, 0.15f, 40f, 0f);

            // World 3: The Rust Meridian — orange-rust rocky at 1400u orbit.
            Vector3 rustMeridianPos = OrbitPos(sunCenter, 1400f, 120f, 0f);
            var rustMeridian = BuildCanonPlanet(universe, "The Rust Meridian",
                rustMeridianPos, 120f, new Color(0.5f, 0.32f, 0.20f), "Rocky");
            AddSurfacePatches(rustMeridian, 5, new Color(0.35f, 0.22f, 0.15f), 60000003);
            AddCityLights(rustMeridian, 8, new Color(1f, 0.5f, 0.15f), 60000004);
            AddPlanetOrbit(rustMeridian, sunVisual.transform, 1400f, 0.12f, 120f, 0f);

            // World 4: Kethis Prime — gray-rock planet at 1650u orbit with asteroid belt.
            Vector3 kethisPos = OrbitPos(sunCenter, 1650f, 200f, 0f);
            var kethis = BuildCanonPlanet(universe, "Kethis Prime",
                kethisPos, 100f, new Color(0.5f, 0.45f, 0.4f), "Rocky");
            AddSurfacePatches(kethis, 4, new Color(0.35f, 0.30f, 0.28f), 60000005);
            AddPlanetOrbit(kethis, sunVisual.transform, 1650f, 0.10f, 200f, 0f);
            // Kethis Asteroid Belt.
            BuildAsteroidRing(universe, kethisPos, 180f, 40f, 30f, 50, scaleMul: 6f);

            // World 5: Narcosis — purple-magenta gas giant at 1850u orbit.
            Vector3 narcosisPos = OrbitPos(sunCenter, 1850f, 260f, 0f);
            var narcosis = BuildCanonPlanet(universe, "Narcosis",
                narcosisPos, 130f, new Color(0.6f, 0.2f, 0.55f), "Gas");
            AddCloudShell(narcosis, new Color(0.8f, 0.4f, 0.9f), 0.4f);
            AddPlanetOrbit(narcosis, sunVisual.transform, 1850f, 0.08f, 260f, 0f);

            // World 6: Arkship Meridian — derelict generation ship (gray hull) at 2100u orbit.
            Vector3 arkshipPos = OrbitPos(sunCenter, 2100f, 330f, 0f);
            var arkship = BuildCanonPlanet(universe, "Arkship Meridian",
                arkshipPos, 90f, new Color(0.4f, 0.42f, 0.45f), "Rocky");
            AddCityLights(arkship, 6, new Color(0.6f, 0.8f, 1f), 60000006);
            AddPlanetOrbit(arkship, sunVisual.transform, 2100f, 0.06f, 330f, 0f);

            // World 7: Thermopause — red-giant lit rocky at 2350u orbit.
            Vector3 thermopausePos = OrbitPos(sunCenter, 2350f, 380f, 0f);
            var thermopause = BuildCanonPlanet(universe, "Thermopause",
                thermopausePos, 100f, new Color(0.7f, 0.35f, 0.25f), "Rocky");
            AddPolarCaps(thermopause, new Color(0.8f, 0.85f, 0.95f));
            AddPlanetOrbit(thermopause, sunVisual.transform, 2350f, 0.05f, 380f, 0f);

            // World 8: Meridian-7 — clone-colony dense-city rocky at 2600u orbit.
            Vector3 meridian7Pos = OrbitPos(sunCenter, 2600f, 440f, 0f);
            var meridian7 = BuildCanonPlanet(universe, "Meridian-7",
                meridian7Pos, 120f, new Color(0.45f, 0.5f, 0.55f), "Rocky");
            AddCityLights(meridian7, 20, new Color(0.7f, 0.9f, 1f), 60000007);
            AddPlanetOrbit(meridian7, sunVisual.transform, 2600f, 0.04f, 440f, 0f);

            // Starfield dome.
            BuildStarfield(null, 5000f, 2200);

            // The Corsair: parked at (250, 20, 350), dockable as fallback.
            var corsair = BuildCorsairExterior(universe, new Vector3(250f, 20f, 350f), 6f);

            // Galaxy 4 Wormhole — unlocked by finishing EP24 (The Fracture Protocol). Flying into it teleports
            // to the Galaxy 4 hub. Parked opposite the Corsair so the two docks never read as one.
            var galaxy4Wormhole = BuildGalaxy4Wormhole(universe, new Vector3(-350f, 30f, 250f));

            // Asteroid hazard.
            var hazardGo = new GameObject("Asteroid Hazard");
            var hazard = hazardGo.AddComponent<AsteroidHazard>();
            hazard.Configure(rig.GetComponent<Health>(), hullCol.radius);

            // Flight controller.
            var flightGo = new GameObject("Flight Controller");
            var shipCtrl = flightGo.AddComponent<ShipController>();
            var shipSo = new SerializedObject(shipCtrl);
            SetObjectRef(shipSo, "universe", universe);
            SetObjectRef(shipSo, "throttleAxis", FindRef(refs, "Left Hand", "Move"));
            SetObjectRef(shipSo, "steerAxis", FindRef(refs, "Right Hand", "Turn"));
            shipSo.FindProperty("maxSpeed").floatValue = 90f;
            shipSo.FindProperty("acceleration").floatValue = 20f;
            shipSo.ApplyModifiedPropertiesWithoutUndo();

            // Projectile pool.
            var poolGo = new GameObject("Projectile Pool");
            var pool = poolGo.AddComponent<ProjectilePool>();

            // Player guns.
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
            SetObjectRef(gunsSo, "ownerRoot", rig);
            SetObjectRefList(gunsSo, "muzzles", new List<Object> { muzzleL, muzzleR });
            gunsSo.ApplyModifiedPropertiesWithoutUndo();

            BuildCockpitCrosshair(cockpit, guns);

            // Windshield waypoint arrow.
            var arrowGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowGo.name = "Planet Waypoint Arrow";
            Object.DestroyImmediate(arrowGo.GetComponent<Collider>());
            arrowGo.transform.SetParent(cockpit, false);
            arrowGo.transform.localPosition = new Vector3(0f, 1.35f, 1.2f);
            arrowGo.transform.localScale = new Vector3(0.06f, 0.06f, 0.25f);
            TintShared(arrowGo.GetComponent<Renderer>(), new Color(0.3f, 0.8f, 1f));
            var marker = arrowGo.AddComponent<Ronin7.Ship.PlanetTargetMarker>();
            var mSo = new SerializedObject(marker);
            SetObjectRef(mSo, "arrow", arrowGo.transform);
            SetObjectRef(mSo, "target", rustfang != null ? rustfang.transform : null);
            SetObjectRef(mSo, "universe", universe);
            SetObjectRef(mSo, "arrowRenderer", arrowGo.GetComponent<Renderer>());
            mSo.FindProperty("fadeStartRange").floatValue = 3000f;
            mSo.FindProperty("arriveRange").floatValue = 150f;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            var arrowCtrl = arrowGo.AddComponent<Ronin7.Ship.ObjectiveArrowController>();
            var acSo = new SerializedObject(arrowCtrl);
            SetObjectRef(acSo, "marker", marker);
            SetObjectRef(acSo, "defaultObjective", rustfang != null ? rustfang.transform : null);
            acSo.ApplyModifiedPropertiesWithoutUndo();

            // Campaign Objective Selector: 1 entry for Galaxy 3 (only EP17 for now).
            var selectorGo = new GameObject("Campaign Objective Selector");
            selectorGo.transform.SetParent(arrowGo.transform, false);
            var selector = selectorGo.AddComponent<CampaignObjectiveSelector>();
            var selSo = new SerializedObject(selector);
            var entriesProp = selSo.FindProperty("entries");
            entriesProp.arraySize = 7;
            // Entry 0: Rustfang's Cradle (always available at start)
            var entry0 = entriesProp.GetArrayElementAtIndex(0);
            entry0.FindPropertyRelative("requiredCompletedScene").stringValue = "";
            entry0.FindPropertyRelative("objective").objectReferenceValue = rustfang != null ? rustfang.transform : null;
            // Entry 1: Thalassa-5 (EP18, available after EP17 Surface Duel)
            var entry1 = entriesProp.GetArrayElementAtIndex(1);
            entry1.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy3Ep17SurfaceDuelSceneName;
            entry1.FindPropertyRelative("objective").objectReferenceValue = meridian5 != null ? meridian5.transform : null;
            // Entry 2: The Rust Meridian (EP19, available after EP18 Hyperspace finale)
            var entry2 = entriesProp.GetArrayElementAtIndex(2);
            entry2.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy3Ep18HyperspaceSceneName;
            entry2.FindPropertyRelative("objective").objectReferenceValue = rustMeridian != null ? rustMeridian.transform : null;
            // Entry 3: Kethis Prime (EP20, available after EP19 Corsair finale)
            var entry3 = entriesProp.GetArrayElementAtIndex(3);
            entry3.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy3Ep19CorsairSceneName;
            entry3.FindPropertyRelative("objective").objectReferenceValue = kethis != null ? kethis.transform : null;
            // Entry 4: Narcosis (EP21, available after EP20 Hyperspace finale)
            var entry4 = entriesProp.GetArrayElementAtIndex(4);
            entry4.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy3Ep20HyperspaceSceneName;
            entry4.FindPropertyRelative("objective").objectReferenceValue = narcosis != null ? narcosis.transform : null;
            // Entry 5: Arkship Meridian (EP22, available after EP21 Awakening finale)
            var entry5 = entriesProp.GetArrayElementAtIndex(5);
            entry5.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy3Ep21AwakeningSceneName;
            entry5.FindPropertyRelative("objective").objectReferenceValue = arkship != null ? arkship.transform : null;
            // Entry 6: Thermopause (EP23, available after EP22 Stellar Dive finale)
            var entry6 = entriesProp.GetArrayElementAtIndex(6);
            entry6.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy3Ep22StellarDiveSceneName;
            entry6.FindPropertyRelative("objective").objectReferenceValue = thermopause != null ? thermopause.transform : null;
            SetObjectRef(selSo, "arrow", arrowCtrl);
            selSo.ApplyModifiedPropertiesWithoutUndo();

            // Space Encounter Manager (same settings as Galaxy2).
            var encounterGo = new GameObject("Space Encounters");
            var encounter = encounterGo.AddComponent<SpaceEncounterManager>();
            var encSo = new SerializedObject(encounter);
            SetObjectRef(encSo, "player", shipCtrl);
            SetObjectRef(encSo, "universe", universe);
            SetObjectRef(encSo, "pool", pool);
            SetObjectRef(encSo, "definition", enemyShipDef);
            encSo.FindProperty("requireFirstPlanetDeparture").boolValue = true;
            encSo.FindProperty("waveCount").intValue = 1;
            encSo.FindProperty("enemiesPerWave").intValue = 2;
            encSo.FindProperty("enemiesAddedPerWave").intValue = 0;
            encSo.FindProperty("enemiesAddedPerGalaxy").intValue = 2;
            encSo.FindProperty("interWaveDelay").floatValue = 6f;
            encSo.FindProperty("initialDelay").floatValue = 5f;
            encSo.FindProperty("travelBetweenWaves").floatValue = 400f;
            encSo.FindProperty("spawnDistance").floatValue = 260f;
            encSo.ApplyModifiedPropertiesWithoutUndo();

            // NO GuardEncounter for Galaxy 3 (kept simple for EP17).

            // Landing approach: 2 landable targets (Rustfang primary, Corsair fallback).
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            lso.FindProperty("firstPlanetScene").stringValue = Galaxy3Ep17SalvageYardSceneName;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 10;
                // Rustfang's Cradle: primary EP17 landing (no requirement).
                SetLandable(landables, 0, rustfang, 90f, Galaxy3Ep17SalvageYardSceneName);
                // The Corsair: fallback docking (reuse Galaxy1 interior).
                SetLandable(landables, 1, corsair, 220f, Galaxy1CorsairSceneName);
                // Thalassa-5: EP18 landing, gated on EP17 Surface Duel completion.
                SetLandable(landables, 2, meridian5, 90f, Galaxy3Ep18StationSceneName, Galaxy3Ep17SurfaceDuelSceneName);
                // The Rust Meridian: EP19 landing, gated on EP18 Hyperspace finale completion.
                SetLandable(landables, 3, rustMeridian, 90f, Galaxy3Ep19CargoHoldSceneName, Galaxy3Ep18HyperspaceSceneName);
                // Kethis Prime: EP20 landing (Varek Bazaar asteroid marketplace), gated on EP19 Corsair finale completion.
                SetLandable(landables, 4, kethis, 90f, Galaxy3Ep20DockingBaySceneName, Galaxy3Ep19CorsairSceneName);
                // Narcosis: EP21 landing (Crimson Lotus pollen city-station), gated on EP20 Hyperspace finale completion.
                SetLandable(landables, 5, narcosis, 90f, Galaxy3Ep21NarcosisDescentSceneName, Galaxy3Ep20HyperspaceSceneName);
                // Arkship Meridian: EP22 landing (derelict generation ship in the Scythe Nebula), gated on EP21 Awakening finale completion.
                SetLandable(landables, 6, arkship, 90f, Galaxy3Ep22CargoApproachSceneName, Galaxy3Ep21AwakeningSceneName);
                // Thermopause: EP23 landing (derelict cryo research outpost over the ice world), gated on EP22 Stellar Dive finale completion.
                SetLandable(landables, 7, thermopause, 90f, Galaxy3Ep23SignalApproachSceneName, Galaxy3Ep22StellarDiveSceneName);
                // Meridian-7: EP24 landing (failing hive-consensus clone colony), gated on EP23 Ghost Fleet finale completion. GALAXY 3 FINALE.
                SetLandable(landables, 8, meridian7, 90f, Galaxy3Ep24HowManySceneName, Galaxy3Ep23GhostFleetSceneName);
                // Galaxy 4 Wormhole: teleports to the Galaxy 4 hub, gated on EP24 Light Separate finale (galaxy3 complete).
                SetLandable(landables, 9, galaxy4Wormhole, 120f, Galaxy4SceneName, Galaxy3Ep24LightSeparateSceneName);
            }
            lso.ApplyModifiedPropertiesWithoutUndo();

            // Ship Spawn Placer.
            var shipSpawnerGo = new GameObject("Ship Spawn Placer");
            var shipSpawner = shipSpawnerGo.AddComponent<Ronin7.Ship.ShipSpawnPlacer>();
            var spSo = new SerializedObject(shipSpawner);
            SetObjectRef(spSo, "ship", shipCtrl);
            SetObjectRef(spSo, "universe", universe);
            SetObjectRef(spSo, "landing", landing);
            spSo.ApplyModifiedPropertiesWithoutUndo();

            // Completed Planet Outlines.
            var outlineGo = new GameObject("Completed Planet Outlines");
            var outlines = outlineGo.AddComponent<Ronin7.Ship.CompletedPlanetOutlines>();
            var outSo = new SerializedObject(outlines);
            SetObjectRef(outSo, "landing", landing);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Ronin7/Art/Materials/SamuraiOutline.mat");
            if (outlineMat != null)
                SetObjectRef(outSo, "outlineMaterial", outlineMat);
            outSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Galaxy3ScenePath);
            EnsureScenesInBuild(Galaxy3ScenePath, Galaxy3Ep17SalvageYardScenePath, Galaxy3Ep17ArenaScenePath,
                Galaxy3Ep17LowerPitScenePath, Galaxy3Ep17VaultScenePath, Galaxy3Ep17EscapeScenePath,
                Galaxy3Ep17SurfaceDuelScenePath, Galaxy3Ep18StationScenePath, Galaxy3Ep18VaultOuterScenePath,
                Galaxy3Ep18ExtractionScenePath, Galaxy3Ep18CommandCoreScenePath, Galaxy3Ep18DockScenePath,
                Galaxy3Ep18HyperspaceScenePath, Galaxy3Ep19CargoHoldScenePath, Galaxy3Ep19AsteroidPursuitScenePath,
                Galaxy3Ep19MineShaftScenePath, Galaxy3Ep19VaultBelowScenePath, Galaxy3Ep19AshAndVoidScenePath,
                Galaxy3Ep19DarkCorridorsScenePath, Galaxy3Ep19CorsairScenePath,
                Galaxy3Ep20DockingBayScenePath, Galaxy3Ep20ChemicalSectorScenePath, Galaxy3Ep20VarekShardVaultScenePath,
                Galaxy3Ep20CentralVaultScenePath, Galaxy3Ep20DominionInterdictionScenePath, Galaxy3Ep20RecordsRoomScenePath,
                Galaxy3Ep20HyperspaceScenePath,
                Galaxy3Ep21NarcosisDescentScenePath, Galaxy3Ep21ForgettingDescentScenePath, Galaxy3Ep21GardenOfEchoesScenePath,
                Galaxy3Ep21TestimonyScenePath, Galaxy3Ep21ExpulsionScenePath, Galaxy3Ep21ShadowInCodeScenePath,
                Galaxy3Ep21AwakeningScenePath,
                Galaxy3Ep22CargoApproachScenePath, Galaxy3Ep22VaultOfGhostsScenePath, Galaxy3Ep22ChildrenBelowScenePath,
                Galaxy3Ep22WhatRonin1LeftScenePath, Galaxy3Ep22YoungerChainScenePath, Galaxy3Ep22BroadcastScenePath,
                Galaxy3Ep22StellarDiveScenePath,
                Galaxy3Ep23SignalApproachScenePath, Galaxy3Ep23IceBelowScenePath, Galaxy3Ep23CryptBelowScenePath,
                Galaxy3Ep23TheCommanderScenePath, Galaxy3Ep23DefectiveGenerationScenePath, Galaxy3Ep23TheWakeScenePath,
                Galaxy3Ep23GhostFleetScenePath,
                Galaxy3Ep24HowManyScenePath, Galaxy3Ep24RetirementColonyScenePath, Galaxy3Ep24ThirtyYearsScenePath,
                Galaxy3Ep24KhallsMercyScenePath, Galaxy3Ep24ShatteredProtocolScenePath, Galaxy3Ep24LightSeparateScenePath);

            Debug.Log($"[Space Samurai] Galaxy 3 SPACE scene built at {Galaxy3ScenePath}. " +
                      "Industrial/rust-palette ember sun with 8 canon worlds: Rustfang's Cradle (landable, EP17), " +
                      "Thalassa-5 (icy-blue ocean, landable EP18), The Rust Meridian (orange-rust), Kethis Prime (with asteroid belt), " +
                      "Narcosis (purple gas giant), Arkship Meridian (derelict), Thermopause (red-giant, landable EP23), Meridian-7 (dense colony). " +
                      "Single ambient wave (no GuardEncounter). Landing: Rustfang's Cradle → EP17 (no gate), Thalassa-5 → EP18 (gated on EP17), " +
                      "Thermopause → EP23 (gated on EP22 Stellar Dive finale), Corsair fallback. " +
                      "Briefings: pre-mission (cabin_brief) → post-mission EP17 (space_ep17_post) → post-mission EP18 (space_ep18_post).");
        }
    }
}
