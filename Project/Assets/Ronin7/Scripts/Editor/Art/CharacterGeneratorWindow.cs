using UnityEditor;
using UnityEngine;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Editor window for "Generate Character from Description": type a description, pick a role
    /// (Decorative or Combat), optionally set a seed, and bake a low-poly prefab via
    /// ArtPrefabBuilder.GenerateCharacterAsync. On success the new prefab is selected and pinged in
    /// the Project window. Mirrors GeminiApiKeyWindow's lightweight style.
    /// </summary>
    public class CharacterGeneratorWindow : EditorWindow
    {
        private string _description = "a chubby green goblin merchant with a brown apron";
        private ArtPrefabBuilder.CharacterRole _role = ArtPrefabBuilder.CharacterRole.Decorative;
        private int _seed = 1;
        private bool _busy;
        private string _status = "";

        [MenuItem("Tools/Space Samurai/Art/Generate Character from Description", priority = 2)]
        public static void ShowWindow()
        {
            var win = GetWindow<CharacterGeneratorWindow>(true, "Generate Character", true);
            win.minSize = new Vector2(460, 260);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Describe the character", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "e.g. \"a rusty barrel-bodied robot with one big eye\". Rounded blocky (Roblox/Minecraft) " +
                "style. Combat role wires the enemy rig (Health + hurtbox + held katana) so it can fight.",
                MessageType.Info);

            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));

            EditorGUILayout.Space();
            _role = (ArtPrefabBuilder.CharacterRole)EditorGUILayout.EnumPopup("Role", _role);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            EditorGUILayout.LabelField(" ", "Same description + seed reuses the cached spec (no re-bill).",
                EditorStyles.miniLabel);

            EditorGUILayout.Space();
            bool hasKey = !string.IsNullOrEmpty(EditorPrefs.GetString(GeminiClient.ApiKeyPref, ""));
            if (!hasKey)
                EditorGUILayout.HelpBox(
                    "No Gemini API key set. Use Tools/Space Samurai/Art/Set Gemini API Key first.",
                    MessageType.Warning);

            using (new EditorGUI.DisabledScope(_busy || !hasKey))
            {
                if (GUILayout.Button(_busy ? "Generating…" : "Generate", GUILayout.Height(32)))
                    Generate();
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedMiniLabel);
        }

        private async void Generate()
        {
            _busy = true;
            _status = "Asking Gemini…";
            Repaint();
            try
            {
                var prefab = await ArtPrefabBuilder.GenerateCharacterAsync(_description, _role, _seed);
                if (prefab != null)
                {
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                    _status = $"Done → {AssetDatabase.GetAssetPath(prefab)}";
                }
                else
                {
                    _status = "Generation failed — see the Console for details.";
                }
            }
            catch (System.Exception ex)
            {
                _status = "Error: " + ex.Message;
                Debug.LogError($"[CharacterGen] {ex}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }
    }
}
