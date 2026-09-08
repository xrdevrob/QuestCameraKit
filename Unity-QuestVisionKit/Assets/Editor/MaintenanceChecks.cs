using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using QuestCameraKit.OpenAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace QuestCameraKit.Editor
{
    public static class MaintenanceChecks
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        public static void CheckWavStereo()
        {
            var clip = AudioClip.Create("stereo-regression", 3, 2, 44100, false);
            try
            {
                clip.SetData(new[] { 0f, 0.5f, -0.5f, 1f, -1f, 0.25f }, 0);
                var wav = SaveWav.Save("test.wav", clip);
                Require(wav.Length == 56, $"Stereo WAV lost channel samples: expected 56 bytes, got {wav.Length}.");
                Require(BitConverter.ToInt32(wav, 40) == wav.Length - 44, "WAV data length disagrees with header.");
                Require(BitConverter.ToInt16(wav, 22) == 2, "WAV channel count changed.");
                Require(BitConverter.ToInt16(wav, 46) > 0 && BitConverter.ToInt16(wav, 48) < 0, "Stereo samples are not interleaved correctly.");
                Debug.Log("PASS: stereo WAV preserves every channel and has a correct header.");
            }
            finally { Object.DestroyImmediate(clip); }
        }

        public static void Run()
        {
            Require(typeof(QuestCameraKit.WebRTC.WebRTCController).GetField("_webRTCConnection",
                BindingFlags.NonPublic | BindingFlags.Instance) != null, "WEBRTC_ENABLED must compile the streaming controller.");
            Require(SampleMenu.Label("Assets/Samples/3 QRCodeDetection/QRCodeDetection.unity") == "QR tracking (Meta native)",
                "Native QR must be clearly distinguished from the raw-camera sample.");
            Require(SampleMenu.AvailableScenes().Length == 6 && !SampleMenu.AvailableScenes().Any(path => path.Contains("WebRTC-SingleClient")), "The combined build must contain all six headset samples.");
            CheckNativeQrBounds();
            CheckWavStereo();
            CheckModelContract();
            var escape = typeof(ImageOpenAIConnector).GetMethod("EscapeJson", BindingFlags.NonPublic | BindingFlags.Instance);
            var owner = new GameObject("json-check");
            owner.SetActive(false);
            try
            {
                var connector = owner.AddComponent<ImageOpenAIConnector>();
                const string input = "quote \" slash \\ newline\n tab\t return\r";
                var escaped = (string)escape.Invoke(connector, new object[] { input });
                var decoded = OVRSimpleJSON.JSON.Parse("{\"text\":\"" + escaped + "\"}")["text"].Value;
                Require(decoded == input, "Command text must survive JSON encoding unchanged.");
                Require((string)escape.Invoke(connector, new object[] { null }) == "", "Null command must serialize safely.");
            }
            finally { Object.DestroyImmediate(owner); }
            Debug.Log("PASS: QuestCameraKit maintenance regression checks.");
        }

        private static void CheckNativeQrBounds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Samples/3 QRCodeDetection/Prefabs/QRCodePrefab.prefab");
            Require(prefab, "Native QR overlay prefab must exist.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var visualizer = instance.GetComponent<Meta.XR.MRUtilityKitSamples.QRCodeDetection.Bounded2DVisualizer>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = visualizer.GetType();
                var line = (LineRenderer)type.GetField("_lineRenderer", flags).GetValue(visualizer);
                var canvas = (RectTransform)type.GetField("_canvasRect", flags).GetValue(visualizer);
                var update = type.GetMethod("SetBounds", flags);
                Require(line && canvas, "Native QR prefab needs its boundary and payload canvas.");
                update.Invoke(visualizer, new object[] { null });
                Require(!line.enabled && !canvas.gameObject.activeSelf, "Missing native bounds must hide the overlay.");
                update.Invoke(visualizer, new object[] { new Rect(1, 2, 3, 4) });
                Require(line.enabled && canvas.gameObject.activeSelf && line.positionCount == 4 &&
                    line.GetPosition(0) == new Vector3(1, 2, 0) && line.GetPosition(2) == new Vector3(4, 6, 0),
                    "Native bounds must draw the correct rectangle immediately after becoming available.");
                update.Invoke(visualizer, new object[] { null });
                Require(!line.enabled && !canvas.gameObject.activeSelf, "Losing native bounds must hide stale geometry.");
                Debug.Log("PASS: native QR overlay handles missing, available, and lost bounds.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void CheckModelContract()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(
                "Assets/Samples/2 ObjectDetection/Resources/yolov9sentis.sentis");
            Require(asset, "The bundled detection model must import successfully.");
            var model = Unity.InferenceEngine.ModelLoader.Load(asset);
            using var worker = new Unity.InferenceEngine.Worker(model, Unity.InferenceEngine.BackendType.CPU);
            using var input = new Unity.InferenceEngine.Tensor<float>(
                new Unity.InferenceEngine.TensorShape(1, 3, 640, 640), new float[3 * 640 * 640]);
            worker.Schedule(input);
            Require(worker.PeekOutput(0) is Unity.InferenceEngine.Tensor<float>, "Coordinates must be floats.");
            Require(worker.PeekOutput(1) is Unity.InferenceEngine.Tensor<int>, "Class IDs must be integers.");
            using var coords = ((Unity.InferenceEngine.Tensor<float>)worker.PeekOutput(0)).ReadbackAndClone();
            using var labels = ((Unity.InferenceEngine.Tensor<int>)worker.PeekOutput(1)).ReadbackAndClone();
            Require(coords.shape.rank == 2 && coords.shape[1] >= 4, "Model coordinate shape changed.");
            Require(coords.shape[0] == labels.shape.length, "Detection and label counts disagree.");
            Debug.Log("PASS: bundled model executes on CPU and preserves the output contract (synthetic black input).");
        }

        public static void PreviewMenu()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Preview camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.15f, 0.19f);
            var menu = new GameObject("Menu preview").AddComponent<SampleMenu>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(SampleMenu).GetMethod("Awake", flags).Invoke(menu, null);
            typeof(SampleMenu).GetMethod("Refresh", flags).Invoke(menu, null);
            typeof(SampleMenu).GetMethod("LateUpdate", flags).Invoke(menu, null);
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1200, 1000, 24);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../../menu-preview.png")), image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(menu.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        public static void AuditScenes()
        {
            var report = new StringBuilder();
            var missing = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Samples" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.AppendLine(path);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                        if (count == 0) continue;
                        missing += count;
                        report.AppendLine($"  MISSING {count}: {transform.name}");
                    }
            }
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../scene-audit.txt"));
            File.WriteAllText(output, report.ToString());
            Debug.Log($"Scene audit: {missing} missing scripts. Report: {output}");
            Require(missing == 0, "One or more sample scenes contain missing scripts; inspect scene-audit.txt.");
        }
    }
}
