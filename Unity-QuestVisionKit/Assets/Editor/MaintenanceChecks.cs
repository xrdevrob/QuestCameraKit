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
#if ZXING_ENABLED
            CheckQr();
#else
            throw new InvalidOperationException("ZXing must be present and ZXING_ENABLED set; QR sample would otherwise do nothing.");
#endif
            Debug.Log("PASS: QuestCameraKit maintenance regression checks.");
        }

#if ZXING_ENABLED
        private static void CheckQr()
        {
            var method = typeof(QrCodeScanner).GetMethod("GetFinderCorners", BindingFlags.NonPublic | BindingFlags.Static);
            Require(method != null, "QR finder points need a consistent four-corner conversion.");
            var points = new[] { new ZXing.ResultPoint(10, 90), new ZXing.ResultPoint(10, 10), new ZXing.ResultPoint(90, 10) };
            var corners = (Vector3[])method.Invoke(null, new object[] { points, 100, 100 });
            Require(corners.Length == 4 && Vector3.Distance(corners[3], new Vector3(0.9f, 0.9f, 0f)) < 0.0001f,
                "Version-one QR finder points must produce a fourth corner.");
            var invalid = (Vector3[])method.Invoke(null, new object[] { new ZXing.ResultPoint[0], 100, 100 });
            Require(invalid.Length == 0, "Incomplete finder points must be rejected.");
            var matrix = new ZXing.QrCode.QRCodeWriter().encode("QuestCameraKit regression", ZXing.BarcodeFormat.QR_CODE, 128, 128);
            var pixels = new byte[128 * 128];
            for (var y = 0; y < 128; y++)
                for (var x = 0; x < 128; x++) pixels[y * 128 + x] = matrix[x, y] ? (byte)0 : (byte)255;
            var bitmap = new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(
                new ZXing.RGBLuminanceSource(pixels, 128, 128, ZXing.RGBLuminanceSource.BitmapFormat.Gray8)));
            var result = new ZXing.QrCode.QRCodeReader().decode(bitmap);
            Require(result != null && result.Text == "QuestCameraKit regression", "Bundled ZXing must decode a generated QR.");
            var go = new GameObject("qr-no-camera-check");
            try
            {
                var scanner = go.AddComponent<QrCodeScanner>();
                var task = scanner.ScanFrameAsync();
                Require(task.IsCompleted && task.Result.Length == 0, "Absent camera must return immediately, not leave a pending scan.");
            }
            finally { Object.DestroyImmediate(go); }
        }
#endif

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
