using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// The global post grade (DefaultVolumeProfile + the URP assets' profile) once mapped a 20% grey to
    /// black and zeroed red (ACES + teal split-toning), so the whole arena rendered black on Quest as
    /// soon as P0-06 turned post on. Renders unlit swatches through the Mobile (Quest) tier with post
    /// on and fails if dark tones are crushed.
    /// </summary>
    public class PostGradeQuestTierTests
    {
        private const int MobileQualityLevel = 0;

        [Test]
        public void MobileTier_PostGrade_DoesNotCrushDarkTones()
        {
            Assert.AreEqual("Mobile", QualitySettings.names[MobileQualityLevel]);
            int prevQ = QualitySettings.GetQualityLevel();
            var scene = EditorSceneManager.NewPreviewScene();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var rt = new RenderTexture(16, 16, 24);
            int mobileAA = -1;
            try
            {
                QualitySettings.SetQualityLevel(MobileQualityLevel, true);
                mobileAA = QualitySettings.antiAliasing; // URP overwrites it with the asset's MSAA on render

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.GetComponent<Renderer>().sharedMaterial = mat;
                quad.transform.position = new Vector3(0f, 0f, 1f);
                quad.transform.localScale = Vector3.one * 4f;
                EditorSceneManager.MoveGameObjectToScene(quad, scene);

                var camGo = new GameObject("GradeCam");
                EditorSceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.targetTexture = rt;
                camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

                Color grey = Render(cam, mat, new Color(0.2f, 0.2f, 0.2f));
                Color rust = Render(cam, mat, new Color(0.32f, 0.19f, 0.12f)); // arena floor tint

                Assert.Greater(grey.r, 0.08f, $"20% grey crushed to {grey}");
                Assert.Greater(grey.grayscale, 0.1f, $"20% grey crushed to {grey}");
                Assert.Greater(rust.r, 0.15f, $"arena floor tint lost its red: {rust}");
            }
            finally
            {
                if (mobileAA >= 0) QualitySettings.antiAliasing = mobileAA; // no QualitySettings.asset churn
                QualitySettings.SetQualityLevel(prevQ, true);
                EditorSceneManager.ClosePreviewScene(scene);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(mat);
            }
        }

        private static Color Render(Camera cam, Material mat, Color swatch)
        {
            mat.SetColor("_BaseColor", swatch);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;
            var tex = new Texture2D(16, 16, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color c = tex.GetPixel(8, 8);
            Object.DestroyImmediate(tex);
            return c;
        }
    }
}
