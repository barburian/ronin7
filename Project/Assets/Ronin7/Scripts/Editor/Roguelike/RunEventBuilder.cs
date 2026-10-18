using Ronin7.Combat;
using Ronin7.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// P2-04: authors the four Event-node assets at <c>Data/Roguelike/Events/</c> and assigns them to
    /// Phase6_Boot's <see cref="RunDirector"/> (additive: opens the scene, sets one field, saves).
    /// Idempotent — re-running overwrites the assets' fields in place.
    /// </summary>
    public static class RunEventBuilder
    {
        public const string EventsFolder = "Assets/Ronin7/Data/Roguelike/Events";
        private const string BootScenePath = "Assets/Ronin7/Scenes/Phase6_Boot.unity";

        [MenuItem("Tools/Space Samurai/Roguelike/Build Run Events (+ wire RunDirector)")]
        public static void BuildAndWire()
        {
            BuildEvents();
            WireBootRunDirector();
        }

        public static RunEventDefinition[] BuildEvents()
        {
            if (!AssetDatabase.IsValidFolder(EventsFolder))
                AssetDatabase.CreateFolder("Assets/Ronin7/Data/Roguelike", "Events");

            var result = new[]
            {
                Ensure("BloodPact", "Blood Pact", "A red sigil hums on the floor. It offers power — for a piece of you.",
                    Opt("ACCEPT THE PACT", "-15 max HP, gain a Rare boon", 1f,
                        new[] { Fx(RunEventEffectKind.MaxHealthAdd, -15f), Boon(BoonRarity.Rare) }),
                    Opt("REFUSE", "Walk on unchanged", 1f)),
                Ensure("Gamble", "Gamble", "A cracked die spins in the air. Fortune or pain — even odds.",
                    Opt("ROLL THE DIE", "50%: Epic boon · 50%: take 25 damage", 0.5f,
                        new[] { Boon(BoonRarity.Epic) }, new[] { Fx(RunEventEffectKind.Damage, 25f) }),
                    Opt("WALK AWAY", "Keep what you have", 1f)),
                Ensure("Meditate", "Meditate", "A quiet alcove, out of the Fracture's noise.",
                    Opt("MEDITATE", "Heal 30% of max HP", 1f, new[] { Fx(RunEventEffectKind.HealFraction, 0.3f) }),
                    Opt("MOVE ON", "No time to rest", 1f)),
                Ensure("ForgottenShrine", "Forgotten Shrine", "An old shrine still answers to Shards.",
                    Opt("MAKE AN OFFERING", "Spend 40 Shards: +1 reroll, heal 15%", 1f,
                        new[] { Fx(RunEventEffectKind.SpendShards, 40f), Fx(RunEventEffectKind.AddReroll, 1f), Fx(RunEventEffectKind.HealFraction, 0.15f) }),
                    Opt("LEAVE", "Keep your Shards", 1f)),
            };
            AssetDatabase.SaveAssets();
            return result;
        }

        private static RunEventDefinition Ensure(string file, string title, string body, RunEventOption a, RunEventOption b)
        {
            string path = EventsFolder + "/" + file + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<RunEventDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<RunEventDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.title = title;
            def.body = body;
            def.optionA = a;
            def.optionB = b;
            EditorUtility.SetDirty(def);
            return def;
        }

        private static RunEventOption Opt(string label, string hint, float chance, RunEventEffect[] effects = null, RunEventEffect[] fail = null) =>
            new RunEventOption
            {
                label = label,
                hint = hint,
                chance = chance,
                effects = effects ?? new RunEventEffect[0],
                failEffects = fail ?? new RunEventEffect[0],
            };

        private static RunEventEffect Fx(RunEventEffectKind kind, float amount) => new RunEventEffect { kind = kind, amount = amount };
        private static RunEventEffect Boon(BoonRarity rarity) => new RunEventEffect { kind = RunEventEffectKind.GrantBoon, rarity = rarity };

        /// <summary>Additive: sets only RunDirector.runEvents in Phase6_Boot (mirrors
        /// RoguelikeArenaBuilder's boonCatalog wiring).</summary>
        public static void WireBootRunDirector()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            // Load after the scene switch (references taken before it can come back as fake-null).
            var guids = AssetDatabase.FindAssets("t:RunEventDefinition", new[] { EventsFolder });
            var events = new RunEventDefinition[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                events[i] = AssetDatabase.LoadAssetAtPath<RunEventDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            var director = Object.FindFirstObjectByType<RunDirector>();
            if (director == null)
            {
                Debug.LogError("[RunEventBuilder] No RunDirector in Phase6_Boot — run the arena builder first.");
                return;
            }
            var so = new SerializedObject(director);
            var prop = so.FindProperty("runEvents");
            prop.arraySize = events.Length;
            for (int i = 0; i < events.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = events[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RunEventBuilder] Wired {events.Length} run events into Phase6_Boot's RunDirector.");
        }
    }
}
