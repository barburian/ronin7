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
    /// Galaxy 2 solar-system builder (EP09+ sessions). Builds the ash-gray/ember-tinted hub with
    /// Dead World Kethrin, Char Spire Station, and a single GuardEncounter on the Cinders moon.
    /// Lives in the same <see cref="XRRigBuilder"/> partial class so it can call all private
    /// static helpers directly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        // Scene paths for Galaxy 2 and its connected episode scenes.
        private const string Galaxy2ScenePath = SceneFolder + "/Galaxy2.unity";
        private const string Galaxy2Ep09CharSpireScenePath = SceneFolder + "/Galaxy2_EP09_CharSpire.unity";
        private const string Galaxy2Ep09SpireDuelScenePath = SceneFolder + "/Galaxy2_EP09_SpireDuel.unity";
        private const string Galaxy2Ep09Kethel7MemoryScenePath = SceneFolder + "/Galaxy2_EP09_Kethel7Memory.unity";
        private const string Galaxy2Ep09CindersRefineryScenePath = SceneFolder + "/Galaxy2_EP09_CindersRefinery.unity";
        private const string Galaxy2Ep09ExtractionScenePath = SceneFolder + "/Galaxy2_EP09_Extraction.unity";

        private static readonly string Galaxy2SceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2ScenePath);
        private static readonly string Galaxy2Ep09CharSpireSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep09CharSpireScenePath);
        private static readonly string Galaxy2Ep09SpireDuelSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep09SpireDuelScenePath);
        private static readonly string Galaxy2Ep09Kethel7MemorySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep09Kethel7MemoryScenePath);
        private static readonly string Galaxy2Ep09CindersRefinerySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep09CindersRefineryScenePath);
        private static readonly string Galaxy2Ep09ExtractionSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep09ExtractionScenePath);

        // EP10 scene paths and names
        private const string Galaxy2Ep10VellochSurfaceScenePath = SceneFolder + "/Galaxy2_EP10_VellochSurface.unity";
        private const string Galaxy2Ep10SanctuaryScenePath = SceneFolder + "/Galaxy2_EP10_Sanctuary.unity";
        private const string Galaxy2Ep10RuinsDuelScenePath = SceneFolder + "/Galaxy2_EP10_RuinsDuel.unity";
        private const string Galaxy2Ep10RescueScenePath = SceneFolder + "/Galaxy2_EP10_Rescue.unity";
        private const string Galaxy2Ep10LandingZoneScenePath = SceneFolder + "/Galaxy2_EP10_LandingZone.unity";
        private const string Galaxy2Ep10ExtractionScenePath = SceneFolder + "/Galaxy2_EP10_Extraction.unity";

        private static readonly string Galaxy2Ep10VellochSurfaceSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep10VellochSurfaceScenePath);
        private static readonly string Galaxy2Ep10SanctuarySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep10SanctuaryScenePath);
        private static readonly string Galaxy2Ep10RuinsDuelSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep10RuinsDuelScenePath);
        private static readonly string Galaxy2Ep10RescueSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep10RescueScenePath);
        private static readonly string Galaxy2Ep10LandingZoneSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep10LandingZoneScenePath);
        private static readonly string Galaxy2Ep10ExtractionSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep10ExtractionScenePath);

        // EP11 scene paths and names
        private const string Galaxy2Ep11CourtyardScenePath = SceneFolder + "/Galaxy2_EP11_Courtyard.unity";
        private const string Galaxy2Ep11DojoScenePath = SceneFolder + "/Galaxy2_EP11_Dojo.unity";
        private const string Galaxy2Ep11ArchiveCollapseScenePath = SceneFolder + "/Galaxy2_EP11_ArchiveCollapse.unity";
        private const string Galaxy2Ep11AscentScenePath = SceneFolder + "/Galaxy2_EP11_Ascent.unity";
        private const string Galaxy2Ep11ExitPointScenePath = SceneFolder + "/Galaxy2_EP11_ExitPoint.unity";

        private static readonly string Galaxy2Ep11CourtyardSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep11CourtyardScenePath);
        private static readonly string Galaxy2Ep11DojoSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep11DojoScenePath);
        private static readonly string Galaxy2Ep11ArchiveCollapseSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep11ArchiveCollapseScenePath);
        private static readonly string Galaxy2Ep11AscentSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep11AscentScenePath);
        private static readonly string Galaxy2Ep11ExitPointSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep11ExitPointScenePath);

        // EP12 scene paths and names
        private const string Galaxy2Ep12ShardMarketScenePath = SceneFolder + "/Galaxy2_EP12_ShardMarket.unity";
        private const string Galaxy2Ep12VaultScenePath = SceneFolder + "/Galaxy2_EP12_Vault.unity";
        private const string Galaxy2Ep12GraveyardScenePath = SceneFolder + "/Galaxy2_EP12_Graveyard.unity";
        private const string Galaxy2Ep12CutterScenePath = SceneFolder + "/Galaxy2_EP12_Cutter.unity";
        private const string Galaxy2Ep12TheShardScenePath = SceneFolder + "/Galaxy2_EP12_TheShard.unity";
        private const string Galaxy2Ep12PursuitScenePath = SceneFolder + "/Galaxy2_EP12_Pursuit.unity";

        private static readonly string Galaxy2Ep12ShardMarketSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep12ShardMarketScenePath);
        private static readonly string Galaxy2Ep12VaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep12VaultScenePath);
        private static readonly string Galaxy2Ep12GraveyardSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep12GraveyardScenePath);
        private static readonly string Galaxy2Ep12CutterSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep12CutterScenePath);
        private static readonly string Galaxy2Ep12TheShardSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep12TheShardScenePath);
        private static readonly string Galaxy2Ep12PursuitSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep12PursuitScenePath);

        // EP13 "The Carnival of Forgotten Names" scenes.
        private const string Galaxy2Ep13CarouselScenePath = SceneFolder + "/Galaxy2_EP13_Carousel.unity";
        private const string Galaxy2Ep13MirrorMazeScenePath = SceneFolder + "/Galaxy2_EP13_MirrorMaze.unity";
        private const string Galaxy2Ep13CenterTentScenePath = SceneFolder + "/Galaxy2_EP13_CenterTent.unity";
        private const string Galaxy2Ep13VaultScenePath = SceneFolder + "/Galaxy2_EP13_Vault.unity";
        private const string Galaxy2Ep13CoreFightScenePath = SceneFolder + "/Galaxy2_EP13_CoreFight.unity";
        private const string Galaxy2Ep13EscapeScenePath = SceneFolder + "/Galaxy2_EP13_Escape.unity";

        private static readonly string Galaxy2Ep13CarouselSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep13CarouselScenePath);
        private static readonly string Galaxy2Ep13MirrorMazeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep13MirrorMazeScenePath);
        private static readonly string Galaxy2Ep13CenterTentSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep13CenterTentScenePath);
        private static readonly string Galaxy2Ep13VaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep13VaultScenePath);
        private static readonly string Galaxy2Ep13CoreFightSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep13CoreFightScenePath);
        private static readonly string Galaxy2Ep13EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep13EscapeScenePath);

        // EP14 "Deep Station Mercer" scenes.
        private const string Galaxy2Ep14DockingRingScenePath = SceneFolder + "/Galaxy2_EP14_DockingRing.unity";
        private const string Galaxy2Ep14ObservationScenePath = SceneFolder + "/Galaxy2_EP14_Observation.unity";
        private const string Galaxy2Ep14ArchiveDefenseScenePath = SceneFolder + "/Galaxy2_EP14_ArchiveDefense.unity";
        private const string Galaxy2Ep14RevelationScenePath = SceneFolder + "/Galaxy2_EP14_Revelation.unity";
        private const string Galaxy2Ep14SiegeScenePath = SceneFolder + "/Galaxy2_EP14_Siege.unity";
        private const string Galaxy2Ep14EscapeScenePath = SceneFolder + "/Galaxy2_EP14_Escape.unity";

        private static readonly string Galaxy2Ep14DockingRingSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep14DockingRingScenePath);
        private static readonly string Galaxy2Ep14ObservationSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep14ObservationScenePath);
        private static readonly string Galaxy2Ep14ArchiveDefenseSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep14ArchiveDefenseScenePath);
        private static readonly string Galaxy2Ep14RevelationSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep14RevelationScenePath);
        private static readonly string Galaxy2Ep14SiegeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep14SiegeScenePath);
        private static readonly string Galaxy2Ep14EscapeSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep14EscapeScenePath);

        // EP15 "The Frequency" scenes.
        private const string Galaxy2Ep15RelayShaftsScenePath = SceneFolder + "/Galaxy2_EP15_RelayShafts.unity";
        private const string Galaxy2Ep15CorvetteBoardingScenePath = SceneFolder + "/Galaxy2_EP15_CorvetteBoarding.unity";
        private const string Galaxy2Ep15SulfurThroneScenePath = SceneFolder + "/Galaxy2_EP15_SulfurThrone.unity";
        private const string Galaxy2Ep15SunkenArchivesScenePath = SceneFolder + "/Galaxy2_EP15_SunkenArchives.unity";
        private const string Galaxy2Ep15DesertReckoningScenePath = SceneFolder + "/Galaxy2_EP15_DesertReckoning.unity";
        private const string Galaxy2Ep15DreadnoughtScenePath = SceneFolder + "/Galaxy2_EP15_Dreadnought.unity";

        private static readonly string Galaxy2Ep15RelayShaftsSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep15RelayShaftsScenePath);
        private static readonly string Galaxy2Ep15CorvetteBoardingSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep15CorvetteBoardingScenePath);
        private static readonly string Galaxy2Ep15SulfurThroneSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep15SulfurThroneScenePath);
        private static readonly string Galaxy2Ep15SunkenArchivesSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep15SunkenArchivesScenePath);
        private static readonly string Galaxy2Ep15DesertReckoningSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep15DesertReckoningScenePath);
        private static readonly string Galaxy2Ep15DreadnoughtSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep15DreadnoughtScenePath);

        // EP16 scenes (Cinder Vale arc).
        private const string Galaxy2Ep16DockingTrenchScenePath = SceneFolder + "/Galaxy2_EP16_DockingTrench.unity";
        private const string Galaxy2Ep16MonasteryScenePath = SceneFolder + "/Galaxy2_EP16_Monastery.unity";
        private const string Galaxy2Ep16BladeGardenScenePath = SceneFolder + "/Galaxy2_EP16_BladeGarden.unity";
        private const string Galaxy2Ep16CorvetteAssaultScenePath = SceneFolder + "/Galaxy2_EP16_CorvetteAssault.unity";
        private const string Galaxy2Ep16TheDuelScenePath = SceneFolder + "/Galaxy2_EP16_TheDuel.unity";
        private const string Galaxy2Ep16SilentGardenScenePath = SceneFolder + "/Galaxy2_EP16_SilentGarden.unity";

        private static readonly string Galaxy2Ep16DockingTrenchSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep16DockingTrenchScenePath);
        private static readonly string Galaxy2Ep16MonasterySceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep16MonasteryScenePath);
        private static readonly string Galaxy2Ep16BladeGardenSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep16BladeGardenScenePath);
        private static readonly string Galaxy2Ep16CorvetteAssaultSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep16CorvetteAssaultScenePath);
        private static readonly string Galaxy2Ep16TheDuelSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep16TheDuelScenePath);
        private static readonly string Galaxy2Ep16SilentGardenSceneName =
            System.IO.Path.GetFileNameWithoutExtension(Galaxy2Ep16SilentGardenScenePath);

        /// <summary>
        /// Builds an ember-tinted sun for Galaxy 2: smaller, dimmer, more orange than the bright
        /// gold Galaxy 1 sun. Uses unlit material for self-luminosity + a warm glow light.
        /// </summary>
        private static GameObject BuildEmberSun(Transform universe, Light sunLight, Vector3 center, float scale)
        {
            var sun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sun.name = "Sun Visual";
            Object.DestroyImmediate(sun.GetComponent<Collider>());
            sun.transform.SetParent(universe, false);
            sun.transform.localPosition = center;
            sun.transform.localScale = Vector3.one * scale;

            var renderer = sun.GetComponent<Renderer>();
            renderer.sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.55f, 0.30f));

            sunLight.transform.rotation = Quaternion.LookRotation((Vector3.zero - center).normalized);
            sunLight.intensity = 1.0f;
            sunLight.color = new Color(1.0f, 0.7f, 0.5f);
            RenderSettings.sun = sunLight;

            // Warm ember glow light.
            var glowGo = new GameObject("Sun Glow Light");
            glowGo.transform.SetParent(sun.transform, false);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.45f, 0.2f);
            glow.intensity = 1.5f;
            glow.range = 600f;
            glow.shadows = LightShadows.None;

            return sun;
        }

        /// <summary>
        /// Builds Char Spire Station: a tall, vertical ash/rust derelict mining station with a
        /// docking spine and a few ember window glows. Returns the docking spine for landable wiring.
        /// </summary>
        private static GameObject BuildCharSpireStation(Transform universe, Vector3 position)
        {
            var root = new GameObject("Char Spire Station");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var rust = new Color(0.45f, 0.32f, 0.22f);
            var darkRust = new Color(0.3f, 0.22f, 0.16f);
            var emberGlow = new Color(1f, 0.5f, 0.2f);

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

            // Stacked narrow cubes rising vertically: the spire's spine.
            Part("SpireBase", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(6f, 6f, 8f), darkRust);
            Part("SpireSection1", PrimitiveType.Cube, new Vector3(2f, 8f, 1f), new Vector3(5f, 5f, 6f), rust);
            Part("SpireSection2", PrimitiveType.Cube, new Vector3(-1.5f, 14f, -2f), new Vector3(4.5f, 5f, 5f), rust);
            Part("SpireSection3", PrimitiveType.Cube, new Vector3(1f, 20f, 0.5f), new Vector3(4f, 4f, 5f), darkRust);
            Part("SpireSection4", PrimitiveType.Cube, new Vector3(-0.5f, 25f, -1f), new Vector3(3.5f, 4f, 4f), rust);

            // Patched hull plates, slightly proud of the spire.
            Part("PatchA", PrimitiveType.Cube, new Vector3(3.5f, 10f, 2f), new Vector3(2f, 3f, 2f), rust);
            Part("PatchB", PrimitiveType.Cube, new Vector3(-4f, 16f, 3f), new Vector3(1.5f, 2.5f, 2f), darkRust);
            Part("PatchC", PrimitiveType.Cube, new Vector3(2.5f, 22f, -2.5f), new Vector3(1.8f, 2f, 1.5f), rust);

            // A few ember window glows scattered on the spire.
            AddUnlitVisual(root.transform, "WindowGlow1", new Vector3(2f, 12f, 3f),
                new Vector3(0.3f, 0.3f, 0.1f), PrimitiveType.Cube, emberGlow);
            AddUnlitVisual(root.transform, "WindowGlow2", new Vector3(-3f, 18f, 2.5f),
                new Vector3(0.35f, 0.3f, 0.1f), PrimitiveType.Cube, emberGlow);
            AddUnlitVisual(root.transform, "WindowGlow3", new Vector3(1f, 24f, 1f),
                new Vector3(0.3f, 0.25f, 0.1f), PrimitiveType.Cube, emberGlow);

            // Docking spine: a long horizontal dock protruding from the middle section with a green guide strip.
            var dockingSpine = Part("DockingSpine", PrimitiveType.Cube, new Vector3(8f, 12f, 0f), new Vector3(12f, 3f, 4f), rust);
            var strip = Part("DockStrip", PrimitiveType.Cube, new Vector3(8f, 13.2f, 0f), new Vector3(0.5f, 0.15f, 3.6f), Color.green);
            strip.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.2f, 1f, 0.4f));

            // Warm dock light.
            var dockLight = new GameObject("DockingLight");
            dockLight.transform.SetParent(root.transform, false);
            dockLight.transform.localPosition = new Vector3(14f, 12f, 0f);
            var dockLightComp = dockLight.AddComponent<Light>();
            dockLightComp.type = LightType.Point;
            dockLightComp.color = new Color(1f, 0.7f, 0.3f);
            dockLightComp.intensity = 1.5f;
            dockLightComp.range = 20f;

            return dockingSpine;
        }

        /// <summary>
        /// Builds the Memory Bazaar: a loose cluster of dark cargo boxes at orbit ~1700 with faint
        /// violet unlit glow strips for atmosphere. Static (no orbit), purely visual.
        /// </summary>
        private static GameObject BuildMemoryBazaar(Transform universe, Vector3 position)
        {
            var root = new GameObject("Memory Bazaar");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var darkBox = new Color(0.18f, 0.16f, 0.20f);
            var violetGlow = new Color(0.6f, 0.3f, 0.8f);

            // Scattered dark boxes in a loose cluster.
            for (int i = 0; i < 8; i++)
            {
                float x = (i % 3 - 1) * 10f;
                float y = (i / 3 - 1) * 8f;
                float z = (i % 2) * 6f;
                AddVisualTinted(root.transform, $"Box{i}", new Vector3(x, y, z),
                    new Vector3(4f + i * 0.5f, 3f + (i % 2), 3f), PrimitiveType.Cube, darkBox);
            }

            // Faint violet glow strips on the cluster for mystique.
            AddUnlitVisual(root.transform, "GlowStrip1", new Vector3(-8f, 4f, 2f),
                new Vector3(20f, 0.1f, 0.2f), PrimitiveType.Cube, violetGlow);
            AddUnlitVisual(root.transform, "GlowStrip2", new Vector3(5f, -3f, 0f),
                new Vector3(0.2f, 15f, 0.2f), PrimitiveType.Cube, violetGlow);

            return root;
        }

        /// <summary>
        /// Builds Velloch's Reach: an amber-dust rocky planet with patchy darker dust bands.
        /// Used in EP10 as the landing target for the Haven Home sanctuary mission.
        /// </summary>
        private static GameObject BuildVellochsReach(Transform universe, Vector3 position)
        {
            var velloch = BuildCanonPlanet(universe, "Velloch's Reach",
                position, 100f, new Color(0.72f, 0.55f, 0.35f), "Rocky");
            AddSurfacePatches(velloch, 5, new Color(0.58f, 0.42f, 0.25f), 50000012);
            return velloch;
        }

        /// <summary>
        /// Builds Verdis Prime: a green-blue jungle-moon rocky planet with darker surface patches.
        /// Used in EP11 as the landing target for the monastery mission.
        /// </summary>
        private static GameObject BuildVerdisPrime(Transform universe, Vector3 position)
        {
            var verdis = BuildCanonPlanet(universe, "Verdis Prime",
                position, 90f, new Color(0.30f, 0.55f, 0.42f), "Rocky");
            AddSurfacePatches(verdis, 5, new Color(0.20f, 0.42f, 0.30f), 50000013);
            return verdis;
        }

        /// <summary>
        /// Builds the Shard Market: a hollowed, rotating mining asteroid (dark grey rock with
        /// excavation-scarred surface patches). EP12 landing target for the Memory Merchant mission.
        /// </summary>
        private static GameObject BuildShardMarket(Transform universe, Vector3 position)
        {
            var shard = BuildCanonPlanet(universe, "Shard Market",
                position, 80f, new Color(0.34f, 0.32f, 0.30f), "Rocky");
            AddSurfacePatches(shard, 6, new Color(0.22f, 0.21f, 0.20f), 50000014);
            return shard;
        }

        /// <summary>
        /// Builds the Carnival of Forgotten Names: a Vellum Exchange orbital platform — a dark hull
        /// slab wrapped in neon signage (reds, golds, neural-storage blue) cycling across the frame.
        /// EP13 landing target. Returns the platform root for landable/objective wiring.
        /// </summary>
        private static GameObject BuildCarnival(Transform universe, Vector3 position)
        {
            var root = new GameObject("Carnival of Forgotten Names");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var darkHull = new Color(0.14f, 0.13f, 0.16f);
            var neonRed = new Color(0.95f, 0.20f, 0.30f);
            var neonGold = new Color(1f, 0.75f, 0.25f);
            var neonBlue = new Color(0.25f, 0.55f, 1f);

            // Central platform hull (the body the player docks against).
            AddVisualTinted(root.transform, "PlatformHull", Vector3.zero,
                new Vector3(70f, 24f, 70f), PrimitiveType.Cube, darkHull);
            // Docking ring slab.
            AddVisualTinted(root.transform, "OuterRing", new Vector3(0f, 16f, 0f),
                new Vector3(95f, 6f, 95f), PrimitiveType.Cube, darkHull);

            // Neon signage strips banding the hull (self-luminous).
            AddUnlitVisual(root.transform, "NeonRed1", new Vector3(0f, 6f, 36f),
                new Vector3(60f, 1.2f, 0.4f), PrimitiveType.Cube, neonRed);
            AddUnlitVisual(root.transform, "NeonGold1", new Vector3(0f, 0f, 36f),
                new Vector3(60f, 1.2f, 0.4f), PrimitiveType.Cube, neonGold);
            AddUnlitVisual(root.transform, "NeonBlue1", new Vector3(0f, -6f, 36f),
                new Vector3(60f, 1.2f, 0.4f), PrimitiveType.Cube, neonBlue);
            AddUnlitVisual(root.transform, "NeonRed2", new Vector3(36f, 4f, 0f),
                new Vector3(0.4f, 1.2f, 60f), PrimitiveType.Cube, neonRed);
            AddUnlitVisual(root.transform, "NeonBlue2", new Vector3(-36f, -2f, 0f),
                new Vector3(0.4f, 1.2f, 60f), PrimitiveType.Cube, neonBlue);
            // Vertical center-tent spire glow.
            AddUnlitVisual(root.transform, "TentSpire", new Vector3(0f, 22f, 0f),
                new Vector3(2f, 28f, 2f), PrimitiveType.Cube, neonGold);

            return root;
        }

        /// <summary>
        /// Builds Deep Station Mercer: a derelict Dominion research station in the Veil Nebula with
        /// a cold blue-grey hull, half-buried appearance, dim obsolete markers, and faint beacon glow.
        /// EP14 landing target. Returns the station root for landable/objective wiring.
        /// </summary>
        private static GameObject BuildDeepStationMercer(Transform universe, Vector3 position)
        {
            var root = new GameObject("Deep Station Mercer");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var coldHull = new Color(0.20f, 0.28f, 0.35f);
            var darkCold = new Color(0.12f, 0.18f, 0.24f);
            var dimBeacon = new Color(0.30f, 0.70f, 0.95f);

            // Central research dome (the body the player docks against).
            AddVisualTinted(root.transform, "ResearchDome", Vector3.zero,
                new Vector3(65f, 22f, 65f), PrimitiveType.Cube, coldHull);
            // Docking collar slab (half-buried appearance).
            AddVisualTinted(root.transform, "DockingCollar", new Vector3(0f, -10f, 0f),
                new Vector3(85f, 4f, 85f), PrimitiveType.Cube, darkCold);

            // Science antenna array (tall spire).
            AddVisualTinted(root.transform, "AntennaSpire", new Vector3(0f, 18f, 0f),
                new Vector3(3f, 30f, 3f), PrimitiveType.Cube, darkCold);
            AddVisualTinted(root.transform, "AntennaArray", new Vector3(0f, 28f, 0f),
                new Vector3(20f, 2f, 20f), PrimitiveType.Cube, coldHull);

            // Dim beacon glow strips (faint and obsolete).
            AddUnlitVisual(root.transform, "BeaconGlow1", new Vector3(0f, 8f, 33f),
                new Vector3(55f, 0.8f, 0.3f), PrimitiveType.Cube, dimBeacon);
            AddUnlitVisual(root.transform, "BeaconGlow2", new Vector3(33f, 2f, 0f),
                new Vector3(0.3f, 0.8f, 55f), PrimitiveType.Cube, dimBeacon);
            // Vertical beacon spire at top (faint pulse).
            AddUnlitVisual(root.transform, "BeaconSpire", new Vector3(0f, 20f, 0f),
                new Vector3(1.5f, 24f, 1.5f), PrimitiveType.Cube, dimBeacon);

            return root;
        }

        private static GameObject BuildRelay9(Transform universe, Vector3 position)
        {
            var root = new GameObject("Relay-9");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = position;

            var coldHull = new Color(0.18f, 0.24f, 0.30f);
            var darkCold = new Color(0.10f, 0.15f, 0.20f);
            var ghostBeacon = new Color(0.35f, 0.65f, 0.85f);

            // Central relay core (the body the player docks against).
            AddVisualTinted(root.transform, "RelayCore", Vector3.zero,
                new Vector3(45f, 18f, 45f), PrimitiveType.Cube, coldHull);
            // Maintenance ring slab.
            AddVisualTinted(root.transform, "RelayRing", new Vector3(0f, -8f, 0f),
                new Vector3(70f, 3f, 70f), PrimitiveType.Cube, darkCold);

            // Listening-array mast + dish.
            AddVisualTinted(root.transform, "RelayMast", new Vector3(0f, 16f, 0f),
                new Vector3(2.5f, 34f, 2.5f), PrimitiveType.Cube, darkCold);
            AddVisualTinted(root.transform, "RelayDish", new Vector3(0f, 34f, 0f),
                new Vector3(26f, 2f, 26f), PrimitiveType.Cube, coldHull);

            // Faint dead-channel beacon glow (still broadcasting after years).
            AddUnlitVisual(root.transform, "RelayBeacon1", new Vector3(0f, 6f, 23f),
                new Vector3(40f, 0.7f, 0.3f), PrimitiveType.Cube, ghostBeacon);
            AddUnlitVisual(root.transform, "RelayBeacon2", new Vector3(23f, 0f, 0f),
                new Vector3(0.3f, 0.7f, 40f), PrimitiveType.Cube, ghostBeacon);

            return root;
        }

        /// <summary>
        /// Builds Cinder Vale: a barren ash-colored dead world with surface patches indicating
        /// past volcanic activity. EP16 landing target.
        /// </summary>
        private static GameObject BuildCinderVale(Transform universe, Vector3 position)
        {
            var cinderVale = BuildCanonPlanet(universe, "Cinder Vale",
                position, 95f, new Color(0.50f, 0.45f, 0.42f), "Rocky");
            AddSurfacePatches(cinderVale, 5, new Color(0.38f, 0.32f, 0.28f), 50000015);
            return cinderVale;
        }

        /// <summary>
        /// Encloses the Galaxy 2 cockpit in a solid cabin with a front windshield, seats Kessler in
        /// the back-right as co-pilot, and adds auto-playing briefings wired to a BriefingSelector.
        /// Similar structure to BuildGalaxy1Cabin but with 3 briefings (pre-mission EP09, post-missions EP09/10/11).
        /// </summary>
        private static void BuildGalaxy2Cabin(Transform cockpit)
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

            // Kessler as co-pilot in back-right.
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

            // Enemy warning system.
            EnemyWarningBuilder.AddTo(cockpit, new Vector3(0.85f, 1.4f, -1.3f));

            // Pre-mission briefing (EP09 space briefing from Ep09Lines).
            var preMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep09Briefing", Vector3.zero,
                Ep09Lines.Get("space_ep09_briefing"), null, "space_ep09_briefing", "ep09");
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

            // Post-mission briefing (EP09 post from Ep09Lines).
            var postMissionDialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep09Post", Vector3.zero,
                Ep09Lines.Get("space_ep09_post"), null, "space_ep09_post", "ep09");
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

            // Post-mission briefing (EP10 post from Ep10Lines).
            var postMission2Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep10Post", Vector3.zero,
                Ep10Lines.Get("space_ep10_post"), null, "space_ep10_post", "ep10");
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

            // Post-mission briefing (EP11 post from Ep11Lines).
            var postMission3Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep11Post", Vector3.zero,
                Ep11Lines.Get("space_ep11_post"), null, "space_ep11_post", "ep11");
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

            // Post-mission briefing (EP12 post from Ep12Lines).
            var postMission4Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep12Post", Vector3.zero,
                Ep12Lines.Get("space_ep12_post"), null, "space_ep12_post", "ep12");
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

            // Post-mission briefing (EP13 post from Ep13Lines).
            var postMission5Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep13Post", Vector3.zero,
                Ep13Lines.Get("space_ep13_post"), null, "space_ep13_post", "ep13");
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

            // Post-mission briefing (EP14 post from Ep14Lines).
            var postMission6Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep14Post", Vector3.zero,
                Ep14Lines.Get("space_ep14_post"), null, "space_ep14_post", "ep14");
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

            // Post-mission briefing (EP15 post from Ep15Lines).
            var postMission7Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep15Post", Vector3.zero,
                Ep15Lines.Get("space_ep15_post"), null, "space_ep15_post", "ep15");
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

            // Post-mission briefing (EP16 post from Ep16Lines).
            var postMission8Dialogue = BuildDialoguePlayer("Dialogue_Galaxy2Ep16Post", Vector3.zero,
                Ep16Lines.Get("space_ep16_post"), null, "space_ep16_post", "ep16");
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

            // Wire the BriefingSelector: preMission + postMission + postMission2 + postMission3 + postMission4 + postMission5 + postMission6 + postMission7 + postMission8 + planet scenes.
            var selectorGo = new GameObject("BriefingSelector");
            selectorGo.transform.SetParent(cockpit, false);
            var selector = selectorGo.AddComponent<BriefingSelector>();
            var selSo = new SerializedObject(selector);
            SetObjectRef(selSo, "preMission", preMissionDialogue);
            SetObjectRef(selSo, "postMission", postMissionDialogue);
            selSo.FindProperty("planetScene").stringValue = Galaxy2Ep09ExtractionSceneName;
            SetObjectRef(selSo, "postMission2", postMission2Dialogue);
            selSo.FindProperty("planetScene2").stringValue = Galaxy2Ep10ExtractionSceneName;
            SetObjectRef(selSo, "postMission3", postMission3Dialogue);
            selSo.FindProperty("planetScene3").stringValue = Galaxy2Ep11ExitPointSceneName;
            SetObjectRef(selSo, "postMission4", postMission4Dialogue);
            selSo.FindProperty("planetScene4").stringValue = Galaxy2Ep12PursuitSceneName;
            SetObjectRef(selSo, "postMission5", postMission5Dialogue);
            selSo.FindProperty("planetScene5").stringValue = Galaxy2Ep13EscapeSceneName;
            SetObjectRef(selSo, "postMission6", postMission6Dialogue);
            selSo.FindProperty("planetScene6").stringValue = Galaxy2Ep14EscapeSceneName;
            SetObjectRef(selSo, "postMission7", postMission7Dialogue);
            selSo.FindProperty("planetScene7").stringValue = Galaxy2Ep15DreadnoughtSceneName;
            SetObjectRef(selSo, "postMission8", postMission8Dialogue);
            selSo.FindProperty("planetScene8").stringValue = Galaxy2Ep16SilentGardenSceneName;
            selSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A swirling wormhole the player flies into to teleport to the Galaxy 3 hub. Restyled from the
        /// Galaxy 2 jump gate: concentric glowing event-horizon discs in a blue-violet warp palette,
        /// an outer accretion ring, and a bright beacon. Activation is gated by EP16 completion via the
        /// landable wired in BuildGalaxy2Scene; this method only builds the visual + beacon.
        /// </summary>
        private static GameObject BuildGalaxy3Wormhole(Transform universe, Vector3 localPos)
        {
            var root = new GameObject("Galaxy 3 Wormhole");
            root.transform.SetParent(universe, false);
            root.transform.localPosition = localPos;

            var violet = new Color(0.5f, 0.3f, 0.9f);
            var cyan = new Color(0.3f, 0.7f, 1f);

            // Outer accretion ring: 16 tangent cube segments standing vertically around a circle.
            int segments = 16;
            float ringRadius = 14f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f);
                var seg = AddVisualTinted(root.transform, $"RingSegment{i}", pos,
                    new Vector3(5f, 1.2f, 1.2f), PrimitiveType.Cube, violet);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }

            // Concentric event-horizon discs, flattened on Z, each with increasing transparency toward the bright core.
            // Outer disc: violet, low alpha.
            var outerDisc = AddUnlitVisual(root.transform, "EventHorizon_Outer", Vector3.zero,
                new Vector3(22f, 22f, 0.2f), PrimitiveType.Sphere, violet);
            outerDisc.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(violet, 0.12f);

            // Middle disc: cyan, medium alpha.
            var midDisc = AddUnlitVisual(root.transform, "EventHorizon_Middle", Vector3.zero,
                new Vector3(15f, 15f, 0.2f), PrimitiveType.Sphere, cyan);
            midDisc.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(cyan, 0.2f);

            // Inner disc: bright near-white cyan, higher alpha to read as a glowing throat.
            var innerDisc = AddUnlitVisual(root.transform, "EventHorizon_Inner", Vector3.zero,
                new Vector3(8f, 8f, 0.2f), PrimitiveType.Sphere, new Color(0.6f, 1f, 1f));
            innerDisc.GetComponent<Renderer>().sharedMaterial = MakeGlassMaterial(new Color(0.6f, 1f, 1f), 0.35f);

            // Bright cyan beacon so it reads from across the system.
            var beaconGo = new GameObject("WormholeBeacon");
            beaconGo.transform.SetParent(root.transform, false);
            var beacon = beaconGo.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.color = cyan;
            beacon.intensity = 3f;
            beacon.range = 80f;
            beacon.shadows = LightShadows.None;

            return root;
        }

        /// <summary>
        /// Builds the Galaxy 2 SPACE flight scene: the ash-gray/ember-tinted hub for EP09+, orbiting
        /// Dead World Kethrin with Char Spire Station and Cinders moon (refinery). One GuardEncounter
        /// guards the refinery. The Corsair is dockable as a fallback.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Galaxy 2/Build Galaxy 2 Space Scene", priority = 80)]
        public static void BuildGalaxy2Scene()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var shipWeapon = EnsureShipWeaponDefinition();
            var enemyShipDef = EnsureEnemyShipDefinition();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ash-gray space with warm ember-tinted lighting.
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

            // Ship hull selector (same as Galaxy1).
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

            // Build the Galaxy 2 cabin with EP09 briefings.
            BuildGalaxy2Cabin(cockpit);

            // Universe: ember sun + Dead World Kethrin + Char Spire Station + Cinders moon + Memory Bazaar + etc.
            var universe = new GameObject("Universe").transform;
            Vector3 sunCenter = new Vector3(0f, 0f, 1500f);
            var sunVisual = BuildEmberSun(universe, light, sunCenter, 400f);

            var aimer = lightGo.AddComponent<SunLightAimer>();
            var aimerSo = new SerializedObject(aimer);
            SetObjectRef(aimerSo, "sunLight", light);
            SetObjectRef(aimerSo, "sunVisual", sunVisual.transform);
            aimerSo.ApplyModifiedPropertiesWithoutUndo();

            // Dead World Kethrin: the anchor planet for EP09, gray-brown rocky at 850u orbit.
            Vector3 kethrinPos = OrbitPos(sunCenter, 850f, 0f, 0f);
            var kethrin = BuildCanonPlanet(universe, "Dead World Kethrin (Ashfall)",
                kethrinPos, 120f, new Color(0.55f, 0.50f, 0.45f), "Rocky");
            AddSurfacePatches(kethrin, 6, new Color(0.40f, 0.35f, 0.30f), 50000009);
            AddPlanetOrbit(kethrin, sunVisual.transform, 850f, 0.25f, 0f, 0f);

            // Char Spire Station orbits Dead World Kethrin (like Velorum orbits Aquilane).
            var charSpire = BuildCharSpireStation(universe, kethrinPos + new Vector3(70f, 0f, 0f));
            AddPlanetOrbit(charSpire, kethrin != null ? kethrin.transform : sunVisual.transform, 70f, 0.8f, 0f);

            // Cinders moon (refinery): industrial moon with city lights, orbiting Kethrin.
            var cindersPos = kethrinPos + new Vector3(200f, 0f, 0f);
            var cinders = BuildCanonPlanet(universe, "Moon Cinders (Refinery)",
                cindersPos, 80f, new Color(0.40f, 0.35f, 0.30f), "Rocky");
            AddSurfacePatches(cinders, 4, new Color(0.30f, 0.25f, 0.20f), 50000010);
            AddCityLights(cinders, 12, new Color(1f, 0.5f, 0.2f), 50000011);
            AddPlanetOrbit(cinders, kethrin != null ? kethrin.transform : sunVisual.transform, 200f, 0.6f, 180f);

            // Velloch's Reach: EP10 amber-dust planet at ~1200u orbit, between Kethrin and Memory Bazaar.
            Vector3 vellochPos = OrbitPos(sunCenter, 1200f, 45f, 0f);
            var velloch = BuildVellochsReach(universe, vellochPos);
            AddPlanetOrbit(velloch, sunVisual.transform, 1200f, 0.15f, 45f, 0f);

            // Verdis Prime: EP11 green-blue jungle moon at ~1400u orbit, past Velloch's Reach.
            Vector3 verdisPos = OrbitPos(sunCenter, 1400f, 120f, 0f);
            var verdis = BuildVerdisPrime(universe, verdisPos);
            AddPlanetOrbit(verdis, sunVisual.transform, 1400f, 0.12f, 120f, 0f);

            // Shard Market: EP12 hollowed mining asteroid at ~1650u orbit, past Verdis Prime.
            Vector3 shardMarketPos = OrbitPos(sunCenter, 1650f, 200f, 0f);
            var shardMarket = BuildShardMarket(universe, shardMarketPos);
            AddPlanetOrbit(shardMarket, sunVisual.transform, 1650f, 0.10f, 200f, 0f);

            // Asteroid ring (ash debris) around the Kethrin system.
            BuildAsteroidRing(universe, sunCenter, 1550f, 120f, 40f, 80, scaleMul: 8f);
            BuildWreckHulks(universe, sunCenter, 1550f, 120f, 12);

            // Memory Bazaar: static visual cluster at ~1700u for EP12 preview.
            var memoryBazaar = BuildMemoryBazaar(universe,
                sunCenter + new Vector3(Mathf.Cos(90f * Mathf.Deg2Rad) * 1700f, 0f, Mathf.Sin(90f * Mathf.Deg2Rad) * 1700f));

            // Carnival of Forgotten Names: EP13 Vellum Exchange platform at ~1850u orbit, past the Shard Market.
            Vector3 carnivalPos = OrbitPos(sunCenter, 1850f, 260f, 0f);
            var carnival = BuildCarnival(universe, carnivalPos);
            AddPlanetOrbit(carnival, sunVisual.transform, 1850f, 0.08f, 260f, 0f);

            // Deep Station Mercer: EP14 derelict research station at ~2100u orbit, past the Carnival.
            Vector3 mercerPos = OrbitPos(sunCenter, 2100f, 330f, 0f);
            var mercer = BuildDeepStationMercer(universe, mercerPos);
            AddPlanetOrbit(mercer, sunVisual.transform, 2100f, 0.06f, 330f, 0f);

            // Relay-9: EP15 dead Veil-Expanse listening post at ~2350u orbit, past Mercer.
            Vector3 relay9Pos = OrbitPos(sunCenter, 2350f, 380f, 0f);
            var relay9 = BuildRelay9(universe, relay9Pos);
            AddPlanetOrbit(relay9, sunVisual.transform, 2350f, 0.05f, 380f, 0f);

            // Cinder Vale: EP16 barren ash-colored dead world at ~2600u orbit, past Relay-9.
            Vector3 cinderValePos = OrbitPos(sunCenter, 2600f, 440f, 0f);
            var cinderVale = BuildCinderVale(universe, cinderValePos);
            AddPlanetOrbit(cinderVale, sunVisual.transform, 2600f, 0.04f, 440f, 0f);

            // Starfield dome.
            BuildStarfield(null, 5000f, 2200);

            // The Corsair: parked at (250, 20, 350), dockable as fallback.
            var corsair = BuildCorsairExterior(universe, new Vector3(250f, 20f, 350f), 6f);

            // Galaxy 3 Wormhole — unlocked by finishing EP16 (The Silent Garden). Flying into it teleports
            // to the Galaxy 3 hub. Parked opposite the Corsair so the two docks never read as one.
            var wormhole = BuildGalaxy3Wormhole(universe, new Vector3(-350f, 30f, 250f));

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
            SetObjectRef(mSo, "target", charSpire != null ? charSpire.transform : null);
            SetObjectRef(mSo, "universe", universe);
            SetObjectRef(mSo, "arrowRenderer", arrowGo.GetComponent<Renderer>());
            mSo.FindProperty("fadeStartRange").floatValue = 3000f;
            mSo.FindProperty("arriveRange").floatValue = 150f;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            var arrowCtrl = arrowGo.AddComponent<Ronin7.Ship.ObjectiveArrowController>();
            var acSo = new SerializedObject(arrowCtrl);
            SetObjectRef(acSo, "marker", marker);
            SetObjectRef(acSo, "defaultObjective", charSpire != null ? charSpire.transform : null);
            acSo.ApplyModifiedPropertiesWithoutUndo();

            // Campaign Objective Selector: 8 entries for Galaxy 2 (EP09/10/11/12/13/14/15/16 progression).
            var selectorGo = new GameObject("Campaign Objective Selector");
            selectorGo.transform.SetParent(arrowGo.transform, false);
            var selector = selectorGo.AddComponent<CampaignObjectiveSelector>();
            var selSo = new SerializedObject(selector);
            var entriesProp = selSo.FindProperty("entries");
            entriesProp.arraySize = 8;
            // Entry 0: Char Spire (always available at start)
            var entry0 = entriesProp.GetArrayElementAtIndex(0);
            entry0.FindPropertyRelative("requiredCompletedScene").stringValue = "";
            entry0.FindPropertyRelative("objective").objectReferenceValue = charSpire != null ? charSpire.transform : null;
            // Entry 1: Velloch's Reach (EP10 preview, available after EP09)
            var entry1 = entriesProp.GetArrayElementAtIndex(1);
            entry1.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep09ExtractionSceneName;
            entry1.FindPropertyRelative("objective").objectReferenceValue = velloch != null ? velloch.transform : null;
            // Entry 2: Verdis Prime (EP11 preview, available after EP10)
            var entry2 = entriesProp.GetArrayElementAtIndex(2);
            entry2.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep10ExtractionSceneName;
            entry2.FindPropertyRelative("objective").objectReferenceValue = verdis != null ? verdis.transform : null;
            // Entry 3: Shard Market (EP12 preview, available after EP11)
            var entry3 = entriesProp.GetArrayElementAtIndex(3);
            entry3.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep11ExitPointSceneName;
            entry3.FindPropertyRelative("objective").objectReferenceValue = shardMarket != null ? shardMarket.transform : null;
            // Entry 4: Carnival (EP13 preview, available after EP12)
            var entry4 = entriesProp.GetArrayElementAtIndex(4);
            entry4.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep12PursuitSceneName;
            entry4.FindPropertyRelative("objective").objectReferenceValue = carnival != null ? carnival.transform : null;
            // Entry 5: Deep Station Mercer (EP14 preview, available after EP13)
            var entry5 = entriesProp.GetArrayElementAtIndex(5);
            entry5.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep13EscapeSceneName;
            entry5.FindPropertyRelative("objective").objectReferenceValue = mercer != null ? mercer.transform : null;
            // Entry 6: Relay-9 (EP15 preview, available after EP14)
            var entry6 = entriesProp.GetArrayElementAtIndex(6);
            entry6.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep14EscapeSceneName;
            entry6.FindPropertyRelative("objective").objectReferenceValue = relay9 != null ? relay9.transform : null;
            // Entry 7: Cinder Vale (EP16 preview, available after EP15)
            var entry7 = entriesProp.GetArrayElementAtIndex(7);
            entry7.FindPropertyRelative("requiredCompletedScene").stringValue = Galaxy2Ep15DreadnoughtSceneName;
            entry7.FindPropertyRelative("objective").objectReferenceValue = cinderVale != null ? cinderVale.transform : null;
            SetObjectRef(selSo, "arrow", arrowCtrl);
            selSo.ApplyModifiedPropertiesWithoutUndo();

            // Space Encounter Manager.
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

            // Cinders Picket: guards the Cinders refinery moon, 3 ships.
            var picketGo = new GameObject("Cinders Picket");
            var picket = picketGo.AddComponent<GuardEncounter>();
            var picketSo = new SerializedObject(picket);
            SetObjectRef(picketSo, "player", shipCtrl);
            SetObjectRef(picketSo, "universe", universe);
            SetObjectRef(picketSo, "pool", pool);
            SetObjectRef(picketSo, "definition", enemyShipDef);
            SetObjectRef(picketSo, "guardTarget", cinders != null ? cinders.transform : null);
            picketSo.FindProperty("shipCount").intValue = 3;
            picketSo.FindProperty("spawnRadius").floatValue = 120f;
            picketSo.FindProperty("activateRange").floatValue = 400f;
            picketSo.FindProperty("requiredCompletedScene").stringValue = Galaxy2Ep09Kethel7MemorySceneName;
            picketSo.FindProperty("suppressIfCompletedScene").stringValue = Galaxy2Ep09CindersRefinerySceneName;
            picketSo.FindProperty("clearedFlag").stringValue = "ep09_cinders_picket_cleared";
            picketSo.ApplyModifiedPropertiesWithoutUndo();

            // Landing approach: 5 landable targets.
            var prompt = BuildLandingPrompt(cockpit);
            var landingGo = new GameObject("Landing Approach");
            var landing = landingGo.AddComponent<LandingApproach>();
            var lso = new SerializedObject(landing);
            SetObjectRef(lso, "ship", shipCtrl);
            SetObjectRef(lso, "universe", universe);
            SetObjectRef(lso, "landAction", FindRef(refs, "Right Hand", "Select"));
            SetObjectRef(lso, "promptText", prompt);
            lso.FindProperty("maxLandingSpeed").floatValue = 12f;
            lso.FindProperty("firstPlanetScene").stringValue = Galaxy2Ep09CharSpireSceneName;
            var landables = lso.FindProperty("landables");
            if (landables != null)
            {
                landables.arraySize = 11;
                // Char Spire Station: no requirement
                SetLandable(landables, 0, charSpire, 90f, Galaxy2Ep09CharSpireSceneName);
                // Cinders moon: requires Kethel7Memory completion
                SetLandable(landables, 1, cinders, 90f, Galaxy2Ep09CindersRefinerySceneName, Galaxy2Ep09Kethel7MemorySceneName);
                // The Corsair: reuse Galaxy1 interior
                SetLandable(landables, 2, corsair, 220f, Galaxy1CorsairSceneName);
                // Velloch's Reach: requires EP09 Extraction completion
                SetLandable(landables, 3, velloch, 90f, Galaxy2Ep10VellochSurfaceSceneName, Galaxy2Ep09ExtractionSceneName);
                // Verdis Prime: requires EP10 Extraction completion
                SetLandable(landables, 4, verdis, 90f, Galaxy2Ep11CourtyardSceneName, Galaxy2Ep10ExtractionSceneName);
                // Shard Market: requires EP11 Exit Point completion
                SetLandable(landables, 5, shardMarket, 90f, Galaxy2Ep12ShardMarketSceneName, Galaxy2Ep11ExitPointSceneName);
                // Carnival of Forgotten Names: requires EP12 Pursuit completion
                SetLandable(landables, 6, carnival, 110f, Galaxy2Ep13CarouselSceneName, Galaxy2Ep12PursuitSceneName);
                // Deep Station Mercer: requires EP13 Escape completion
                SetLandable(landables, 7, mercer, 110f, Galaxy2Ep14DockingRingSceneName, Galaxy2Ep13EscapeSceneName);
                // Relay-9: requires EP14 Escape completion
                SetLandable(landables, 8, relay9, 110f, Galaxy2Ep15RelayShaftsSceneName, Galaxy2Ep14EscapeSceneName);
                // Cinder Vale: requires EP15 Dreadnought completion
                SetLandable(landables, 9, cinderVale, 110f, Galaxy2Ep16DockingTrenchSceneName, Galaxy2Ep15DreadnoughtSceneName);
                // Galaxy 3 Wormhole: teleports to the Galaxy 3 hub, gated by EP16 (Silent Garden) completion.
                SetLandable(landables, 10, wormhole, 120f, Galaxy3SceneName, Galaxy2Ep16SilentGardenSceneName);
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
            EditorSceneManager.SaveScene(scene, Galaxy2ScenePath);
            EnsureScenesInBuild(Galaxy2ScenePath, Galaxy2Ep09CharSpireScenePath, Galaxy2Ep09SpireDuelScenePath,
                Galaxy2Ep09Kethel7MemoryScenePath, Galaxy2Ep09CindersRefineryScenePath, Galaxy2Ep09ExtractionScenePath,
                Galaxy2Ep10VellochSurfaceScenePath, Galaxy2Ep10SanctuaryScenePath, Galaxy2Ep10RuinsDuelScenePath,
                Galaxy2Ep10RescueScenePath, Galaxy2Ep10LandingZoneScenePath, Galaxy2Ep10ExtractionScenePath,
                Galaxy2Ep11CourtyardScenePath, Galaxy2Ep11DojoScenePath, Galaxy2Ep11ArchiveCollapseScenePath,
                Galaxy2Ep11AscentScenePath, Galaxy2Ep11ExitPointScenePath,
                Galaxy2Ep12ShardMarketScenePath, Galaxy2Ep12VaultScenePath, Galaxy2Ep12GraveyardScenePath,
                Galaxy2Ep12CutterScenePath, Galaxy2Ep12TheShardScenePath, Galaxy2Ep12PursuitScenePath,
                Galaxy2Ep13CarouselScenePath, Galaxy2Ep13MirrorMazeScenePath, Galaxy2Ep13CenterTentScenePath,
                Galaxy2Ep13VaultScenePath, Galaxy2Ep13CoreFightScenePath, Galaxy2Ep13EscapeScenePath,
                Galaxy2Ep14DockingRingScenePath, Galaxy2Ep14ObservationScenePath, Galaxy2Ep14ArchiveDefenseScenePath,
                Galaxy2Ep14RevelationScenePath, Galaxy2Ep14SiegeScenePath, Galaxy2Ep14EscapeScenePath,
                Galaxy2Ep15RelayShaftsScenePath, Galaxy2Ep15CorvetteBoardingScenePath, Galaxy2Ep15SulfurThroneScenePath,
                Galaxy2Ep15SunkenArchivesScenePath, Galaxy2Ep15DesertReckoningScenePath, Galaxy2Ep15DreadnoughtScenePath,
                Galaxy2Ep16DockingTrenchScenePath, Galaxy2Ep16MonasteryScenePath, Galaxy2Ep16BladeGardenScenePath,
                Galaxy2Ep16CorvetteAssaultScenePath, Galaxy2Ep16TheDuelScenePath, Galaxy2Ep16SilentGardenScenePath,
                Galaxy1CorsairScenePath);

            Debug.Log($"[Space Samurai] Galaxy 2 SPACE scene built at {Galaxy2ScenePath}. " +
                      "Ash-gray ember-tinted sun over Dead World Kethrin + Char Spire Station + Cinders moon (refinery) + Velloch's Reach (EP10) + Verdis Prime (EP11). " +
                      "Single GuardEncounter (Cinders Picket, 3 ships, gated by EP09 Kethel-7 Memory). " +
                      "Landing: Char Spire (no gate) + Cinders (gated) + Corsair + Velloch's Reach (EP10, gated) + Verdis Prime (EP11, gated by EP10 Extraction) + Galaxy 3 Wormhole (gated by EP16). " +
                      "Briefings: pre-mission (EP09), post-missions (EP09/10/11). Arrow targets Char Spire initially; progression unlocks Velloch's Reach (EP10) and Verdis Prime (EP11).");
        }
    }
}
