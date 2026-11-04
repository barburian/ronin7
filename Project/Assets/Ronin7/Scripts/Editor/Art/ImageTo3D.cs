using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Image→3D (Tripo) driven from a texture asset PATH. The unity-mcp GenerateMesh tool takes the reference
    /// image as a 64-bit EntityId that loses precision in JSON, so it can't find the image. Here the texture is
    /// resolved in-process and handed to Unity AI's internal AssetGenerators.GenerateAsync directly.
    /// ponytail: reflection into com.unity.ai.assistant internals; re-check type names if the package updates.
    /// </summary>
    public static class ImageTo3D
    {
        const string Ns = "Unity.AI.Generators.Tools.";

        /// <summary>Starts a generation; logs "[IMG3D] done/failed ..." when it finishes. Returns immediately.</summary>
        public static void Generate(string imagePath, string savePath, string modelId = "model3d-tripo-p2", string format = "fbx")
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            if (image == null) throw new ArgumentException($"No Texture2D at '{imagePath}'.");

            var asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Unity.AI.Generators.Tools");
            Type T(string n) => asm.GetType(Ns + n, true);

            var refType = T("ObjectReference");
            var reference = Activator.CreateInstance(refType);
            refType.GetField("Image").SetValue(reference, image);
            var refs = Array.CreateInstance(refType, 1);
            refs.SetValue(reference, 0);

            var settingsType = T("MeshSettings");
            var settings = Activator.CreateInstance(settingsType);
            settingsType.GetField("ImageReferences").SetValue(settings, refs);
            settingsType.GetField("TargetFormat").SetValue(settings, format);

            var paramsType = T("GenerationParameters`1").MakeGenericType(settingsType);
            var parameters = Activator.CreateInstance(paramsType);
            paramsType.GetField("AssetType").SetValue(parameters, typeof(GameObject));
            paramsType.GetField("SavePath").SetValue(parameters, savePath);
            paramsType.GetField("ModelId").SetValue(parameters, modelId);
            paramsType.GetField("Settings").SetValue(parameters, settings);

            var generate = T("AssetGenerators").GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .First(m => m.Name == "GenerateAsync" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(settingsType);
            var handle = generate.Invoke(null, new[] { parameters, default(System.Threading.CancellationToken) });
            Await(handle, imagePath);
        }

        static async void Await(object handle, string imagePath)
        {
            // DownloadTask, not GenerationTask: the latter finishes while the prefab is still the placeholder cube.
            var task = (Task<Object>)handle.GetType().GetProperty("DownloadTask").GetValue(handle);
            try
            {
                var result = await task;
                Debug.Log($"[IMG3D] done {imagePath} -> {AssetDatabase.GetAssetPath(result)}");
            }
            catch (Exception e)
            {
                var msgs = handle.GetType().GetProperty("Messages").GetValue(handle) as System.Collections.Generic.IEnumerable<string>;
                Debug.LogError($"[IMG3D] failed {imagePath}: {e.Message} {string.Join(" | ", msgs ?? Enumerable.Empty<string>())}");
            }
        }

        [MenuItem("Assets/Ronin7/Image to 3D (Tripo P2)")]
        static void FromSelection()
        {
            foreach (var tex in Selection.GetFiltered<Texture2D>(SelectionMode.Assets))
            {
                var path = AssetDatabase.GetAssetPath(tex);
                Generate(path, System.IO.Path.ChangeExtension(path, null).Replace("_concept", "") + ".prefab");
            }
        }
    }
}
