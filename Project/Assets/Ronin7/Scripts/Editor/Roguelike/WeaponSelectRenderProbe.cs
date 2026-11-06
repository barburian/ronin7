using System;
using System.IO;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>P9-01: renders the WEAPON SELECT panel to a PNG from a throwaway scene so it can be looked at.
    /// Edit mode, saves redirected to a temp folder (the real save is never touched), no open scene is modified.
    /// Writes Project/Logs/WeaponSelect.png. Editor render only: the Quest tier is not covered.</summary>
    public static class WeaponSelectRenderProbe
    {
        [MenuItem("Tools/Space Samurai/Roguelike/Render Weapon Select PNG (P9-01)", priority = 522)]
        public static void Render()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("[WeaponSelect] save or close the dirty scene first."); return; }
            string tmp = Path.Combine(Path.GetTempPath(), "ss_wsel_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            SaveSystem.DirectoryOverride = tmp;
            string prevPath = EditorSceneManager.GetActiveScene().path; // a clean scene only: the boot room would show through
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            try
            {
                MetaProgression.Reset();
                MetaProgression.Load();
                MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
                MetaProgression.TrySelectLoadout(WeaponId.TwinWakizashi);
                MetaProgression.NoteRunEnded(15, true, 7); // Polearm one win short, Whip best 7
                var cam = Camera.main;
                cam.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
                cam.fieldOfView = 50f;
                void Capture(string file)
                {
                    var rt = RenderTexture.GetTemporary(1920, 1200, 24);
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
                var panel = WeaponSelectPanelFactory.Build(cam, requireEventSystem: false);

                Capture("Logs/WeaponSelect.png");
                UnityEngine.Object.DestroyImmediate(panel.gameObject);
                MetaProgression.AddEchoes(200); // Armory: Polearm affordable-ish, Whip locked
                var shop = MetaShopPanelFactory.Build(cam, requireEventSystem: false);
                foreach (var b in shop.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if (b.name == "ARMORY") b.onClick.Invoke();
                Capture("Logs/Armory.png");
                UnityEngine.Object.DestroyImmediate(shop.gameObject);
                Debug.Log("[WeaponSelect] rendered Logs/WeaponSelect.png and Logs/Armory.png");
            }
            finally
            {
                MetaProgression.Reset();
                SaveSystem.DirectoryOverride = null;
                if (!string.IsNullOrEmpty(prevPath)) EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);
                try { Directory.Delete(tmp, true); } catch (IOException) { }
            }
        }
    }
}
