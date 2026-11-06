using System.IO;
using Ronin7.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>P9-04: renders the Polearm, Shield and Whip on a rig body (holstered) and held, next to Sword_Katana, at real
    /// scale, in a throwaway scene at the Mobile and PC quality tiers. Writes Project/Logs/WeaponMesh_{holster|held}_{tier}.png
    /// and logs mean luminance and magenta ratio. Editor render, no HMD: the headset is not covered.</summary>
    public static class WeaponMeshRenderProbe
    {
        [MenuItem("Tools/Space Samurai/Roguelike/Render Weapon Meshes PNG (P9-04)", priority = 523)]
        public static void Render()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("[WeaponMesh] save or close the dirty scene first."); return; }
            string prevPath = EditorSceneManager.GetActiveScene().path;
            int prevQ = QualitySettings.GetQualityLevel();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            try
            {
                var cam = Camera.main;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.35f, 0.38f, 0.42f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.localScale = new Vector3(6f, 0.02f, 6f); floor.transform.position = new Vector3(0f, -0.01f, 0f);
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                var rig = new GameObject("Rig");
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(rig.transform, false); body.transform.localPosition = new Vector3(0f, 0.85f, 0f); body.transform.localScale = new Vector3(0.35f, 0.85f, 0.25f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                XRRigBuilder.RogueEnsurePolearmShield(rig.transform);
                XRRigBuilder.RogueEnsureWhip(rig.transform);
                var gear = rig.GetComponent<PolearmShieldGear>();
                var katana = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ronin7/Prefabs/Art/Sword_Katana.prefab");
                foreach (var g in new[] { gear.polearm.gameObject, gear.shield.gameObject, gear.whip.gameObject }) g.SetActive(true);
                var k = (GameObject)PrefabUtility.InstantiatePrefab(katana);
                k.transform.position = new Vector3(0.7f, 1.0f, 0.0f);
                string[] tiers = { "Mobile", "PC" };
                foreach (var tier in tiers)
                {
                    for (int q = 0; q < QualitySettings.names.Length; q++) if (QualitySettings.names[q] == tier) QualitySettings.SetQualityLevel(q, false);
                    // holstered: rig as built, katana for scale
                    Capture(cam, $"Logs/WeaponMesh_holster_{tier}.png", new[] { (new Vector3(0.1f, 1.1f, -2.6f), new Vector3(0.1f, 0.9f, 0f)), (new Vector3(-2.2f, 1.3f, 0.4f), new Vector3(0f, 0.9f, 0.1f)) });
                    // held: each at chest height beside the katana
                    var held = new[] { gear.polearm.transform, gear.shield.transform, gear.whip.transform };
                    var anchors = new Transform[3]; var lp = new Vector3[3]; var lr = new Quaternion[3];
                    for (int i = 0; i < 3; i++) { anchors[i] = held[i].parent; held[i].SetParent(null); }
                    body.SetActive(false);
                    held[0].SetPositionAndRotation(new Vector3(-1.2f, 1.2f, 0.1f), Quaternion.identity);
                    held[1].SetPositionAndRotation(new Vector3(-0.2f, 1.2f, 0.1f), Quaternion.Euler(0f, 30f, 0f));
                    held[2].SetPositionAndRotation(new Vector3(0.6f, 1.2f, 0.1f), Quaternion.identity);
                    k.transform.position = new Vector3(1.2f, 1.2f, 0.1f);
                    Capture(cam, $"Logs/WeaponMesh_held_{tier}.png", new[] { (new Vector3(0f, 2.6f, -0.2f), new Vector3(0f, 1.2f, 0.4f)), (new Vector3(-0.2f, 1.4f, -2.8f), new Vector3(0f, 1.2f, 0.4f)) });
                    for (int i = 0; i < 3; i++) { held[i].SetParent(anchors[i], false); held[i].localPosition = Vector3.zero; held[i].localRotation = Quaternion.identity; }
                    body.SetActive(true);
                    k.transform.position = new Vector3(0.7f, 1.0f, 0.0f);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(prevQ, false);
                if (!string.IsNullOrEmpty(prevPath)) EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);
            }
        }

        private static void Capture(Camera cam, string file, (Vector3 pos, Vector3 look)[] views)
        {
            var sheet = new Texture2D(1600 * views.Length / 1, 800, TextureFormat.RGB24, false);
            for (int v = 0; v < views.Length; v++)
            {
                cam.transform.position = views[v].pos; cam.transform.LookAt(views[v].look); cam.fieldOfView = 55f;
                var rt = RenderTexture.GetTemporary(1600, 800, 24);
                cam.targetTexture = rt; cam.Render();
                var old = RenderTexture.active; RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, 1600, 800), v * 1600, 0);
                RenderTexture.active = old; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            }
            var px = sheet.GetPixels32(); double lum = 0; long mag = 0;
            foreach (var p in px) { lum += (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255.0; if (p.r > 200 && p.b > 200 && p.g < 80) mag++; }
            Debug.Log($"[WeaponMesh] {file} meanLum={lum / px.Length:F3} magenta={(double)mag / px.Length:F5}");
            File.WriteAllBytes(file, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
