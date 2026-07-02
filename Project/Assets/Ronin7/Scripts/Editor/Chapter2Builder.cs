using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Chapter 2 ("The Auction") scene builder. Builds a single self-contained Velorum undermarket
    /// run on a +Z line: Docking Alley (spawn) -> Market Row (Resh spared) -> Broker Front (the
    /// refusal) -> Auction Floor (syndicate brawl set-piece) -> Holding Cells (Iris freed) -> Records
    /// Vault (the failed-execution reveal) -> Escape/Dock (Mira found, chapter outro).
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> /
    /// <c>Chapter1Builder</c> so it can reuse their geometry/door/NPC/dialogue/mission-step helpers
    /// directly. All chapter-local helpers here are prefixed <c>Ch2</c> to avoid colliding with the
    /// other partial-class files.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch2ScenePath = SceneFolder + "/Ch02_Auction.unity";

        // Voice clips/SFX for this chapter live alongside Ch1's (Chapter1Builder wires its own copies
        // of these same folders rather than sharing the constants, so this chapter does too).
        private const string Ch2VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";
        private const string Ch2AudioFolder = "Assets/Ronin7/Art/Generated/Audio";

        // Named-cast prefabs (Tripo image->3D pipeline, feet placed at y=0, grounded via
        // FitNamedCharacter — same convention Chapter1Builder uses for Kessler/Khall/Echo).
        private const string Ch2ReshPrefab   = "Assets/Ronin7/Art/Generated/Characters3D/Named/Resh.prefab";
        private const string Ch2IrisPrefab   = "Assets/Ronin7/Art/Generated/Characters3D/Named/Iris.prefab";
        private const string Ch2MiraPrefab   = "Assets/Ronin7/Art/Generated/Characters3D/Named/Mira.prefab";
        private const string Ch2BrokerPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Velorum-Broker.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 02 — The Auction", priority = 202)]
        public static void BuildChapter2Auction()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets,
            // so references held across it go fake-null and serialize as {fileID: 0} on every enemy.
            var weapon = EnsureWeaponDefinition();
            var enemyDef = EnsureEnemyDefinition();
            var bodyguardDef = Ch2EnsureBodyguardDefinition();

            // ---- Lighting: grimy amber "surface bleed" key + low ambient, neon accents per room. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.9f, 0.75f, 0.55f);
            light.intensity = 0.5f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -40f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.10f, 0.09f);

            BuildAccentPointLight("AlleyLight", new Vector3(0f, 2.6f, 0f), new Color(0.6f, 0.75f, 0.9f), 1.6f, 12f);
            BuildAccentPointLight("MarketLight0", new Vector3(-3f, 2.8f, 10f), new Color(1f, 0.25f, 0.7f), 2f, 14f);
            BuildAccentPointLight("MarketLight1", new Vector3(3f, 2.8f, 16f), new Color(0.2f, 0.9f, 1f), 2f, 14f);
            BuildAccentPointLight("BrokerLight", new Vector3(0f, 2.6f, 25f), new Color(0.3f, 0.8f, 0.5f), 1.4f, 10f);
            BuildAccentPointLight("AuctionLight0", new Vector3(0f, 3.2f, 39f), Color.white, 3f, 22f);
            BuildAccentPointLight("AuctionLight1", new Vector3(-7f, 3f, 44f), new Color(0.3f, 0.6f, 1f), 1.8f, 14f); // overseer box
            BuildAccentPointLight("CellsLight", new Vector3(0f, 2.4f, 55f), new Color(0.25f, 0.55f, 0.5f), 1.2f, 12f);
            BuildAccentPointLight("VaultLight", new Vector3(0f, 2.8f, 68f), new Color(0.6f, 0.9f, 1f), 1.8f, 12f);
            BuildAccentPointLight("DockLight", new Vector3(0f, 2.6f, 83f), new Color(1f, 0.82f, 0.6f), 2f, 16f);

            // ---- Interior geometry. Linear +Z run: Alley -> Market -> BrokerFront -> Auction ->
            // Cells -> Vault -> Dock. Room widths vary; every doorway gap is centred on x=0. ----
            var interiorGo = new GameObject("Undermarket");
            var interior = interiorGo.transform;
            var floorColor = new Color(0.16f, 0.14f, 0.13f);
            var ceilColor = new Color(0.08f, 0.07f, 0.07f);

            // Docking Alley: x[-4,4], z[-4,4]. Spawn point. Solid front wall, doorway back (z=4).
            BuildFloorCeiling(interior, "Alley", new Vector3(0f, 0f, 0f), new Vector3(8f, 0f, 8f), floorColor, ceilColor);
            BuildWall(interior, "Alley_WallW", new Vector3(-4f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 8f));
            BuildWall(interior, "Alley_WallE", new Vector3(4f, RoomH / 2f, 0f), new Vector3(0.2f, RoomH, 8f));
            BuildWall(interior, "Alley_WallFront", new Vector3(0f, RoomH / 2f, -4f), new Vector3(8f, RoomH, 0.2f));
            BuildDoorwayWall(interior, "Alley_WallBack", new Vector3(0f, RoomH / 2f, 4f), 8f, true, 2.4f);
            BuildRoomDetails(interior, "Alley", new Vector3(0f, 0f, 0f), new Vector2(4f, 4f), new Color(0.35f, 0.32f, 0.3f));

            // Market Row: x[-7,7], z[4,20]. Stalls, decorative crowd, Resh.
            BuildFloorCeiling(interior, "Market", new Vector3(0f, 0f, 12f), new Vector3(14f, 0f, 16f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "Market_WallFront", new Vector3(0f, RoomH / 2f, 4f), 14f, true, 2.4f);
            BuildDoorwayWall(interior, "Market_WallBack", new Vector3(0f, RoomH / 2f, 20f), 14f, true, 2.4f);
            BuildWall(interior, "Market_WallW", new Vector3(-7f, RoomH / 2f, 12f), new Vector3(0.2f, RoomH, 16f));
            BuildWall(interior, "Market_WallE", new Vector3(7f, RoomH / 2f, 12f), new Vector3(0.2f, RoomH, 16f));
            BuildRoomDetails(interior, "Market", new Vector3(0f, 0f, 12f), new Vector2(7f, 8f), new Color(0.5f, 0.2f, 0.35f));
            Ch2BuildMarketStalls(interior);
            PlaceDecorativeCrowd(new[]
            {
                new Vector3(-4f, 0f, 7f), new Vector3(4f, 0f, 8f), new Vector3(-5f, 0f, 12f),
                new Vector3(5f, 0f, 13f), new Vector3(-3f, 0f, 18f), new Vector3(3f, 0f, 17f),
            });

            // Broker Front: x[-5,5], z[20,30]. The barred stall.
            BuildFloorCeiling(interior, "BrokerFront", new Vector3(0f, 0f, 25f), new Vector3(10f, 0f, 10f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "BrokerFront_WallFront", new Vector3(0f, RoomH / 2f, 20f), 10f, true, 2.4f);
            BuildDoorwayWall(interior, "BrokerFront_WallBack", new Vector3(0f, RoomH / 2f, 30f), 10f, true, 2.4f);
            BuildWall(interior, "BrokerFront_WallW", new Vector3(-5f, RoomH / 2f, 25f), new Vector3(0.2f, RoomH, 10f));
            BuildWall(interior, "BrokerFront_WallE", new Vector3(5f, RoomH / 2f, 25f), new Vector3(0.2f, RoomH, 10f));
            BuildRoomDetails(interior, "BrokerFront", new Vector3(0f, 0f, 25f), new Vector2(5f, 5f), new Color(0.25f, 0.45f, 0.3f));
            Ch2BuildBrokerStall(interior);

            // Auction Floor: x[-9,9], z[30,48]. The set-piece.
            BuildFloorCeiling(interior, "Auction", new Vector3(0f, 0f, 39f), new Vector3(18f, 0f, 18f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "Auction_WallFront", new Vector3(0f, RoomH / 2f, 30f), 18f, true, 2.4f);
            BuildDoorwayWall(interior, "Auction_WallBack", new Vector3(0f, RoomH / 2f, 48f), 18f, true, 2.4f);
            BuildWall(interior, "Auction_WallW", new Vector3(-9f, RoomH / 2f, 39f), new Vector3(0.2f, RoomH, 18f));
            BuildWall(interior, "Auction_WallE", new Vector3(9f, RoomH / 2f, 39f), new Vector3(0.2f, RoomH, 18f));
            BuildRoomDetails(interior, "Auction", new Vector3(0f, 0f, 39f), new Vector2(9f, 9f), new Color(0.5f, 0.45f, 0.15f));
            Ch2BuildAuctionSet(interior);

            // Holding Cells: x[-6,6], z[48,62]. Iris caged; the market's secret back-channel.
            BuildFloorCeiling(interior, "Cells", new Vector3(0f, 0f, 55f), new Vector3(12f, 0f, 14f), new Color(0.1f, 0.11f, 0.12f), ceilColor);
            BuildDoorwayWall(interior, "Cells_WallFront", new Vector3(0f, RoomH / 2f, 48f), 12f, true, 2.4f);
            BuildDoorwayWall(interior, "Cells_WallBack", new Vector3(0f, RoomH / 2f, 62f), 12f, true, 2.4f);
            BuildWall(interior, "Cells_WallW", new Vector3(-6f, RoomH / 2f, 55f), new Vector3(0.2f, RoomH, 14f));
            BuildWall(interior, "Cells_WallE", new Vector3(6f, RoomH / 2f, 55f), new Vector3(0.2f, RoomH, 14f));
            BuildRoomDetails(interior, "Cells", new Vector3(0f, 0f, 55f), new Vector2(6f, 7f), new Color(0.2f, 0.3f, 0.32f));
            Ch2BuildCells(interior);
            var doorSlideClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch2AudioFolder}/DoorSlide.wav");
            var auctionToCellsDoor = BuildSlidingDoor(interior, "AuctionToCellsDoor", new Vector3(0f, 0f, 48f), 2.4f, true, startLocked: true);
            WireDoorAudio(auctionToCellsDoor, doorSlideClip);

            // Records Vault: x[-5,5], z[62,74]. Cold, sealed, the archive of the dead.
            BuildFloorCeiling(interior, "Vault", new Vector3(0f, 0f, 68f), new Vector3(10f, 0f, 12f), new Color(0.1f, 0.1f, 0.12f), ceilColor);
            BuildDoorwayWall(interior, "Vault_WallFront", new Vector3(0f, RoomH / 2f, 62f), 10f, true, 2.4f);
            BuildDoorwayWall(interior, "Vault_WallBack", new Vector3(0f, RoomH / 2f, 74f), 10f, true, 2.4f);
            BuildWall(interior, "Vault_WallW", new Vector3(-5f, RoomH / 2f, 68f), new Vector3(0.2f, RoomH, 12f));
            BuildWall(interior, "Vault_WallE", new Vector3(5f, RoomH / 2f, 68f), new Vector3(0.2f, RoomH, 12f));
            BuildRoomDetails(interior, "Vault", new Vector3(0f, 0f, 68f), new Vector2(5f, 6f), new Color(0.25f, 0.5f, 0.55f));
            Ch2BuildVault(interior);
            var vaultToDockDoor = BuildSlidingDoor(interior, "VaultToDockDoor", new Vector3(0f, 0f, 74f), 2.4f, true, startLocked: true);
            WireDoorAudio(vaultToDockDoor, doorSlideClip);

            // Escape / Dock: x[-6,6], z[74,92]. Solid back wall (the ship, lifting off).
            BuildFloorCeiling(interior, "Dock", new Vector3(0f, 0f, 83f), new Vector3(12f, 0f, 18f), floorColor, ceilColor);
            BuildDoorwayWall(interior, "Dock_WallFront", new Vector3(0f, RoomH / 2f, 74f), 12f, true, 2.4f);
            BuildWall(interior, "Dock_WallBack", new Vector3(0f, RoomH / 2f, 92f), new Vector3(12f, RoomH, 0.2f));
            BuildWall(interior, "Dock_WallW", new Vector3(-6f, RoomH / 2f, 83f), new Vector3(0.2f, RoomH, 18f));
            BuildWall(interior, "Dock_WallE", new Vector3(6f, RoomH / 2f, 83f), new Vector3(0.2f, RoomH, 18f));
            BuildRoomDetails(interior, "Dock", new Vector3(0f, 0f, 83f), new Vector2(6f, 9f), new Color(0.4f, 0.36f, 0.3f));
            Ch2BuildDock(interior);

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig (head + hands, no body), locomotion, bounds, belt katana. ----
            var rig = BuildRig(refs, addLocomotion: true);
            var playerHealth = rig.GetComponent<Health>();
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 0f, 44f);
            bounds.radius = 55f;
            BuildSword(new Vector3(2.2f, 0.9f, -2.5f), Quaternion.Euler(0f, 90f, 0f), weapon, Ch1EchoBladePrefab);

            // ---- Resh: worked into Market Row as a fixer/runner (Beat 2 — sparing him). ----
            var reshGo = InstantiateNpc(Ch2ReshPrefab, new Vector3(2f, 0f, 15f), "Resh");
            FitNamedCharacter(reshGo);
            if (reshGo != null)
            {
                var reshNpc = reshGo.AddComponent<StoryNpc>();
                var reshSo = new SerializedObject(reshNpc);
                reshSo.FindProperty("displayName").stringValue = "Resh";
                reshSo.ApplyModifiedPropertiesWithoutUndo();
                var reshWander = reshGo.AddComponent<StoryNpcWander>();
                var reshWanderSo = new SerializedObject(reshWander);
                reshWanderSo.FindProperty("wanderRadius").floatValue = 0.8f;
                reshWanderSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- The broker: a placeholder body behind his own counter/glass, Broker Front. ----
            var brokerGo = InstantiateNpc(Ch2BrokerPrefab, new Vector3(-3.5f, 0f, 25.6f), "Velorum-Broker");
            FitNamedCharacter(brokerGo);
            if (brokerGo != null)
            {
                var brokerNpc = brokerGo.AddComponent<StoryNpc>();
                var brokerSo = new SerializedObject(brokerNpc);
                brokerSo.FindProperty("displayName").stringValue = "Broker";
                brokerSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Iris: caged in the Holding Cells until the Prompt step cuts her loose. ----
            var irisGo = InstantiateNpc(Ch2IrisPrefab, new Vector3(1f, 0f, 54f), "Iris");
            FitNamedCharacter(irisGo);
            if (irisGo != null)
            {
                var irisNpc = irisGo.AddComponent<StoryNpc>();
                var irisSo = new SerializedObject(irisNpc);
                irisSo.FindProperty("displayName").stringValue = "Iris";
                irisSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- Mira: revealed only once the escape-run Trigger fires (she's an unnoticed stowaway
            // until then). Carries a Health + ProtectNpcObjective so the escape leg can fail if she's
            // lost — see ProtectNpcObjective's own doc comment for the failure mechanism. ----
            var miraGo = InstantiateNpc(Ch2MiraPrefab, new Vector3(1.5f, 0f, 77f), "Mira");
            FitNamedCharacter(miraGo);
            Health miraHealth = null;
            if (miraGo != null)
            {
                miraHealth = miraGo.AddComponent<Health>();
                miraHealth.Configure(40f);
                var protect = miraGo.AddComponent<ProtectNpcObjective>();
                var protectSo = new SerializedObject(protect);
                SetObjectRef(protectSo, "protectedHealth", miraHealth);
                SetObjectRef(protectSo, "playerEntity", rig);
                protectSo.ApplyModifiedPropertiesWithoutUndo();
                var miraNpc = miraGo.AddComponent<StoryNpc>();
                var miraSo = new SerializedObject(miraNpc);
                miraSo.FindProperty("displayName").stringValue = "Mira";
                miraSo.ApplyModifiedPropertiesWithoutUndo();
                miraGo.SetActive(false);
            }

            // ---- Auction Floor combat: syndicate enforcers + a tougher Bodyguard mini-boss folded into
            // the same encounter (per the build brief — the script places the Bodyguard in the vault,
            // but this build condenses the chapter's combat into one set-piece on the Auction Floor). ----
            var auctionEnemyHealths = new List<Object>();
            var enforcerPositions = new Vector3[]
            {
                new Vector3(-3f, 0f, 34f), new Vector3(3f, 0f, 34f),
                new Vector3(-4f, 0f, 40f), new Vector3(4f, 0f, 40f),
            };
            foreach (var pos in enforcerPositions)
            {
                var enemy = BuildEnemy(pos, playerHealth, enemyDef);
                enemy.gameObject.SetActive(false);
                auctionEnemyHealths.Add(enemy.GetComponent<Health>());
            }
            var bodyguard = BuildEnemy(new Vector3(0f, 0f, 44f), playerHealth, bodyguardDef);
            bodyguard.gameObject.SetActive(false);
            auctionEnemyHealths.Add(bodyguard.GetComponent<Health>());

            // ---- Escape-run pursuers: inactive until the escape Trigger fires. Not a DefeatEnemies
            // gate — the leg is timed by ReachTrigger: Dock. One pursuer hunts Mira (both activate on
            // the same Trigger step), making the ProtectNpcObjective a live fail-state, not decoration. ----
            var pursuer1 = BuildEnemy(new Vector3(-2f, 0f, 78f), playerHealth, enemyDef);
            pursuer1.gameObject.SetActive(false);
            var pursuer2 = BuildEnemy(new Vector3(2f, 0f, 80f), miraHealth != null ? miraHealth : playerHealth, enemyDef);
            pursuer2.gameObject.SetActive(false);

            // ---- Reach points. ----
            var marketReachGo = new GameObject("MarketReachPoint");
            marketReachGo.transform.position = new Vector3(0f, 1f, 12f);
            var brokerReachGo = new GameObject("BrokerReachPoint");
            brokerReachGo.transform.position = new Vector3(0f, 1f, 25f);
            var auctionReachGo = new GameObject("AuctionReachPoint");
            auctionReachGo.transform.position = new Vector3(0f, 1f, 39f);
            var cellsReachGo = new GameObject("CellsReachPoint");
            cellsReachGo.transform.position = new Vector3(0f, 1f, 55f);
            var vaultReachGo = new GameObject("VaultReachPoint");
            vaultReachGo.transform.position = new Vector3(0f, 1f, 68f);
            var dockReachGo = new GameObject("DockReachPoint");
            dockReachGo.transform.position = new Vector3(0f, 1f, 85f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch2BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 1f), "ch2_beat0_briefing", talkRef);
            var dlgUndermarket = Ch2BuildDialogue("Dialogue_Beat1_Undermarket", new Vector3(0f, 1f, 8f), "ch2_beat1_undermarket", talkRef);
            var dlgReshConfront = Ch2BuildDialogue("Dialogue_Beat2_ReshConfront", new Vector3(2f, 1f, 14f), "ch2_beat2_resh_confront", talkRef);
            var dlgReshRecruit = Ch2BuildDialogue("Dialogue_Beat2_ReshRecruit", new Vector3(2f, 1f, 16f), "ch2_beat2_resh_recruit", talkRef);
            var dlgBroker = Ch2BuildDialogue("Dialogue_Beat1_Broker", new Vector3(-3f, 1f, 26f), "ch2_beat1_broker", talkRef);
            var dlgIris = Ch2BuildDialogue("Dialogue_Beat3_Iris", new Vector3(1f, 1f, 56f), "ch2_beat3_iris", talkRef);
            var dlgReveal = Ch2BuildDialogue("Dialogue_Beat4_Reveal", new Vector3(0f, 1f, 69f), "ch2_beat4_reveal", talkRef);
            var dlgReunion = Ch2BuildDialogue("Dialogue_Beat5_Reunion", new Vector3(0f, 1f, 86f), "ch2_beat5_reunion", talkRef);
            var dlgKept = Ch2BuildDialogue("Dialogue_Beat5_Kept", new Vector3(0f, 1f, 87f), "ch2_beat5_kept", talkRef);

            // ---- Free-Iris prompt (cuts the cage lock), advanced by input via PromptInputAdvancer. ----
            var cutIrisPromptGo = Ch2BuildPrompt("Cut Iris Free  (Y)", new Vector3(1f, 1.4f, 54f));

            // ---- Chapter-complete canvas (worldspace) + outro driver. ----
            var completeCanvasGo = Ch2BuildCompleteCanvas(new Vector3(0f, 1.4f, 85f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 85f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch2_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            // publishZoneCompleted defaults to true on ChapterOutro — this scene is scene-keyed
            // completion, same as every episode/chapter finale.
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 2 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var advancer = cutIrisPromptGo.GetComponent<PromptInputAdvancer>();
            var advancerSo = new SerializedObject(advancer);
            SetObjectRef(advancerSo, "mission", missionDirector);
            if (talkRef != null) SetObjectRef(advancerSo, "advanceAction", talkRef);
            advancerSo.ApplyModifiedPropertiesWithoutUndo();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 20;

            AuthorDialogueStep(steps, n++, "Beat0: Briefing (aboard the rig)", dlgBriefing);
            AuthorReachStep(steps, n++, "ReachTrigger: Market Row", marketReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: Undermarket (descent, naming Resh)", dlgUndermarket);
            AuthorDialogueStep(steps, n++, "Beat2: Resh (confrontation)", dlgReshConfront);
            AuthorDialogueStep(steps, n++, "Beat2: Resh (spared, recruited)", dlgReshRecruit);
            AuthorReachStep(steps, n++, "ReachTrigger: Broker Front", brokerReachGo.transform, 4.5f);
            AuthorDialogueStep(steps, n++, "Beat1: Broker (the refusal)", dlgBroker);
            AuthorReachStep(steps, n++, "ReachTrigger: Auction Floor", auctionReachGo.transform, 5f);
            AuthorDefeatStep(steps, n++, "DefeatEnemies: Syndicate Enforcers + Bodyguard", auctionEnemyHealths);
            AuthorTriggerStep(steps, n++, "Trigger: Unlock Cells Route (Resh's back-channel)", auctionToCellsDoor);
            AuthorReachStep(steps, n++, "ReachTrigger: Holding Cells", cellsReachGo.transform, 5f);
            AuthorPromptStep(steps, n++, "Prompt: Cut Iris Free", cutIrisPromptGo);
            AuthorDialogueStep(steps, n++, "Beat3: Iris Freed (Kessler reunion)", dlgIris);
            AuthorReachStep(steps, n++, "ReachTrigger: Records Vault", vaultReachGo.transform, 4.5f);
            AuthorDialogueStep(steps, n++, "Beat4: The Failed Execution (killswitch reveal)", dlgReveal);
            AuthorTriggerStep(steps, n++, "Trigger: Escape Run (unlock dock route, Mira revealed, pursuers)",
                vaultToDockDoor, pursuer1.gameObject, pursuer2.gameObject, miraGo);
            AuthorReachStep(steps, n++, "ReachTrigger: Dock", dockReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat5: Reunion (Kessler + Iris, Mira found)", dlgReunion);
            AuthorDialogueStep(steps, n++, "Beat5: Mira Kept", dlgKept);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch2ScenePath);
            EnsureScenesInBuild(Ch2ScenePath);

            Debug.Log($"[Space Samurai] Chapter 2 built at {Ch2ScenePath}. " +
                      "Seven rooms: Docking Alley -> Market Row (Resh spared) -> Broker Front (the refusal) -> " +
                      "Auction Floor (enforcers + Bodyguard) -> Holding Cells (Iris freed) -> Records Vault " +
                      "(killswitch fired-and-failed reveal) -> Escape/Dock (Mira found, outro). " +
                      "20 mission steps. Cells route locked until the Auction Floor fight is won; the dock " +
                      "route locked until the vault files are copied.");
        }

        // ---- Data assets. ----

        /// <summary>Ensures a tougher EnemyDefinition for the broker's Bodyguard mini-boss (higher
        /// health/damage, slower), mirroring the shared EnsureEnemyDefinition pattern.</summary>
        private static EnemyDefinition Ch2EnsureBodyguardDefinition()
        {
            const string path = DataFolder + "/Ch2Bodyguard.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (existing != null) return existing;

            EnsureFolder(DataFolder);
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 220f;
            def.damage = 22f;
            def.moveSpeed = 1.1f;
            def.attackCooldown = 1f;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        // ---- Dialogue: build via the shared helper, then wire ch2 voice clips ourselves. ----

        private static DialoguePlayer Ch2BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter2Lines.Get(setId);
            // clipSetId left null so the shared loader does not spam missing-clip warnings for the wrong folder.
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch2WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter2] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch2WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter2Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch2VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch2VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Small scene-dressing / prompt helpers specific to Chapter 2. ----

        /// <summary>A worldspace prompt carrying a PromptInputAdvancer. Created inactive.</summary>
        private static GameObject Ch2BuildPrompt(string text, Vector3 position)
        {
            var go = new GameObject("CutIrisPrompt");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.012f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.color = new Color(0.95f, 0.85f, 0.55f);
            go.AddComponent<PromptInputAdvancer>();
            go.SetActive(false);
            return go;
        }

        /// <summary>A worldspace "CHAPTER 2 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch2BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 2 COMPLETE Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 220f);
            rt.localScale = Vector3.one * 0.0015f;
            rt.position = position;
            rt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "CHAPTER 2 COMPLETE";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 54;
            label.color = new Color(0.9f, 0.92f, 1f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            canvasGo.SetActive(false);
            return canvasGo;
        }

        /// <summary>Market Row: a handful of stalls (counter + canopy) framing the lane.</summary>
        private static void Ch2BuildMarketStalls(Transform parent)
        {
            var stallColor = new Color(0.4f, 0.18f, 0.12f);
            var canopy = new Color(0.6f, 0.15f, 0.4f);
            Vector3[] stallPositions =
            {
                new Vector3(-5.5f, 0f, 8f), new Vector3(5.5f, 0f, 10f),
                new Vector3(-5.5f, 0f, 16f), new Vector3(5.5f, 0f, 18f),
            };
            foreach (var pos in stallPositions)
            {
                BuildProp(parent, "Stall_Counter", pos, new Vector3(1.6f, 0.9f, 0.8f), stallColor);
                BuildProp(parent, "Stall_Canopy", pos + new Vector3(0f, 1.6f, 0f), new Vector3(1.8f, 0.1f, 1.0f), canopy);
            }
        }

        /// <summary>Broker Front: the barred counter and smeared glass the broker refuses through.</summary>
        private static void Ch2BuildBrokerStall(Transform parent)
        {
            var barColor = new Color(0.15f, 0.16f, 0.18f);
            var glass = new Color(0.3f, 0.5f, 0.45f);
            BuildProp(parent, "Broker_Counter", new Vector3(-3.5f, 0.5f, 25f), new Vector3(1.6f, 1f, 0.5f), barColor);
            BuildProp(parent, "Broker_Glass", new Vector3(-3.5f, 1.4f, 25f), new Vector3(1.6f, 0.8f, 0.05f), glass);
        }

        /// <summary>Auction Floor: the raised stage, a caged lot pedestal, and the overseer's box.</summary>
        private static void Ch2BuildAuctionSet(Transform parent)
        {
            var stageColor = new Color(0.45f, 0.4f, 0.15f);
            var cageColor = new Color(0.2f, 0.2f, 0.22f);
            BuildProp(parent, "Auction_Stage", new Vector3(0f, 0.25f, 39f), new Vector3(6f, 0.5f, 6f), stageColor);

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Auction_LotPedestal";
            pedestal.transform.SetParent(parent, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.9f, 39f);
            pedestal.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
            TintShared(pedestal.GetComponent<Renderer>(), cageColor);

            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * 1.1f, 1.6f, 39f + Mathf.Sin(a) * 1.1f);
                BuildProp(parent, "Auction_CageBar", pos, new Vector3(0.06f, 1.4f, 0.06f), cageColor);
            }

            // Overseer's box: a raised booth on the west wall, watching the floor.
            BuildProp(parent, "OverseerBox", new Vector3(-8.5f, 2.8f, 44f), new Vector3(1.2f, 1.4f, 2f), new Color(0.15f, 0.2f, 0.3f));
        }

        /// <summary>Holding Cells: a scatter of collateral cages (bar clusters).</summary>
        private static void Ch2BuildCells(Transform parent)
        {
            var cageColor = new Color(0.18f, 0.2f, 0.22f);
            Vector3[] cagePositions =
            {
                new Vector3(-3.5f, 0f, 51f), new Vector3(3.5f, 0f, 51f),
                new Vector3(-3.5f, 0f, 58f), new Vector3(3.5f, 0f, 58f),
            };
            foreach (var pos in cagePositions)
                for (int i = 0; i < 4; i++)
                    BuildProp(parent, "CageBar", pos + new Vector3(-0.6f + i * 0.4f, 1.1f, 0f), new Vector3(0.05f, 2.2f, 0.05f), cageColor);
        }

        /// <summary>Records Vault: disposal-manifest racks, the terminal, and the diagnostic table.</summary>
        private static void Ch2BuildVault(Transform parent)
        {
            var rack = new Color(0.2f, 0.22f, 0.25f);
            var terminalColor = new Color(0.2f, 0.85f, 1f);
            for (int i = 0; i < 3; i++)
                BuildProp(parent, "RecordsRack", new Vector3(-4f + i * 0.5f, 1.2f, 63f), new Vector3(0.3f, 2.4f, 4f), rack);

            var terminal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name = "DisposalTerminal";
            terminal.transform.SetParent(parent, false);
            terminal.transform.localPosition = new Vector3(0f, 0.9f, 68f);
            terminal.transform.localScale = new Vector3(0.8f, 1.1f, 0.5f);
            TintShared(terminal.GetComponent<Renderer>(), terminalColor);

            BuildProp(parent, "DiagnosticTable", new Vector3(2.5f, 0.45f, 70f), new Vector3(1.6f, 0.1f, 0.7f), rack);
        }

        /// <summary>Escape/Dock: crates and tie-downs, the crew's own quiet at last.</summary>
        private static void Ch2BuildDock(Transform parent)
        {
            var crate = new Color(0.35f, 0.28f, 0.18f);
            Vector3[] cratePositions = { new Vector3(-4f, 0f, 80f), new Vector3(4f, 0f, 82f), new Vector3(-3f, 0f, 88f) };
            foreach (var pos in cratePositions)
                BuildProp(parent, "Crate", pos, new Vector3(0.8f, 0.8f, 0.8f), crate);
            BuildProp(parent, "TieDown", new Vector3(0f, 0.05f, 85f), new Vector3(3f, 0.05f, 0.15f), crate * 0.7f);
        }
    }
}
