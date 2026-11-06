using System;
using System.IO;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.EditorTools
{
    /// <summary>Playtest 1: renders the real Phase6_Boot main menu at the Mobile tier to Logs/MainMenu_*.png
    /// (no saved run, then the CONTINUE RUN + armed NEW RUN look). Edit mode, saves redirected, the scene is not saved.</summary>
    public static class MainMenuRenderProbe
    {
        [MenuItem("Tools/Space Samurai/Roguelike/Render Main Menu PNG (playtest 1)", priority = 523)]
        public static void Render()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("[MainMenuProbe] save or close the dirty scene first."); return; }
            string tmp = Path.Combine(Path.GetTempPath(), "ss_menu_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            SaveSystem.DirectoryOverride = tmp;
            string prevPath = EditorSceneManager.GetActiveScene().path;
            int prevLevel = QualitySettings.GetQualityLevel();
            try
            {
                QualitySettings.SetQualityLevel(0, true); // Mobile_RPAsset
                EditorSceneManager.OpenScene("Assets/Ronin7/Scenes/Phase6_Boot.unity", OpenSceneMode.Single);
                MetaProgression.Reset();
                MetaProgression.Load();
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
                var canvas = menu.GetComponent<Canvas>();
                canvas.enabled = true;
                menu.gameObject.SetActive(true);
                var cam = Camera.main;
                Vector3 c = canvas.transform.position;
                cam.transform.position = new Vector3(c.x, c.y + 0.1f, c.z) - canvas.transform.forward * 2.2f;
                cam.transform.rotation = Quaternion.LookRotation(canvas.transform.forward, Vector3.up);
                cam.fieldOfView = 60f;
                // Awake/Start do not run in edit mode: drive the same refresh the player gets (no save in the temp dir, so the gap closes).
                typeof(MainMenuController).GetMethod("RefreshRunButtons", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, null);
                Capture(cam, "Logs/MainMenu_NoRun.png");
                typeof(MainMenuController).GetMethod("CloseContinueGap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, new object[] { false });
                foreach (var b in menu.GetComponentsInChildren<Button>(true))
                {
                    var t = b.GetComponentInChildren<Text>(true);
                    if (b.name == "Btn_ContinueRun") b.gameObject.SetActive(true);
                    if (b.name == "Btn_EnterTheFracture" && t != null) t.text = RunEntryOptions.ConfirmAbandonLabel;
                }
                Capture(cam, "Logs/MainMenu_SavedRun.png");
                Debug.Log("[MainMenuProbe] rendered Logs/MainMenu_NoRun.png and Logs/MainMenu_SavedRun.png");
            }
            finally
            {
                MetaProgression.Reset();
                SaveSystem.DirectoryOverride = null;
                QualitySettings.SetQualityLevel(prevLevel, true);
                if (!string.IsNullOrEmpty(prevPath)) EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);
                try { Directory.Delete(tmp, true); } catch (IOException) { }
            }
        }

        private static void Capture(Camera cam, string file)
        {
            var rt = RenderTexture.GetTemporary(1280, 1280, 24);
            cam.targetTexture = rt;
            cam.Render();
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = old;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
