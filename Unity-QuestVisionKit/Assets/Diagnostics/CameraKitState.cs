#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using Meta.XR.MRUtilityKitSamples.QRCodeDetection;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRDevRob.QUAK;

namespace QuestCameraKit.Diagnostics
{
    // Read-only observations of the actual sample. The bridge is excluded from release players.
    public sealed class CameraKitState : MonoBehaviour, IQuakStateProvider
    {
        private PassthroughCameraAccess[] _cameras;
        private DateTime[] _timestamps;
        private int[] _frames;
        private float[] _lastFrameTimes;
        private ObjectDetector _detector;
        private SampleMenu _menu;
        private int _errors;
        public string ProviderId => "quest-camera-kit";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= ObserveScene;
            SceneManager.sceneLoaded += ObserveScene;
        }

        private static void ObserveScene(Scene scene, LoadSceneMode mode) =>
            new GameObject("QuestCameraKit diagnostics").AddComponent<CameraKitState>();

        private void Awake()
        {
            _cameras = FindObjectsByType<PassthroughCameraAccess>(FindObjectsSortMode.None);
            _timestamps = new DateTime[_cameras.Length];
            _frames = new int[_cameras.Length];
            _lastFrameTimes = new float[_cameras.Length];
            _detector = FindAnyObjectByType<ObjectDetector>();
            _menu = FindAnyObjectByType<SampleMenu>();
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            QuakRegistry.Register(this);
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            QuakRegistry.Unregister(this);
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) _errors++;
        }

        private void Update()
        {
            for (var i = 0; i < _cameras.Length; i++)
            {
                if (!_cameras[i] || !_cameras[i].IsPlaying || _timestamps[i] == _cameras[i].Timestamp) continue;
                _timestamps[i] = _cameras[i].Timestamp;
                _frames[i]++;
                _lastFrameTimes[i] = Time.realtimeSinceStartup;
            }
        }

        public string CaptureStateJson()
        {
            var state = new Snapshot
            {
                scene = SceneManager.GetActiveScene().name,
                cameraCount = _cameras.Length,
                minFramesObserved = _cameras.Length == 0 ? 0 : int.MaxValue,
                inferenceCount = _detector ? _detector.CompletedInferenceCount : 0,
                errorCount = _errors,
                menuVisible = _menu && _menu.IsVisible,
                menuSampleCount = _menu ? _menu.SampleCount : 0,
                nativeQrHasPermissions = QRCodeManager.HasPermissions,
                nativeQrTrackedCount = QRCodeManager.ActiveTrackedCount,
                nativeQrSupported = MRUK.Instance && MRUK.Instance.QRCodeTrackingSupported,
                nativeQrTrackingEnabled = MRUK.Instance && MRUK.Instance.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled
            };
            for (var i = 0; i < _cameras.Length; i++)
            {
                if (_cameras[i] && _cameras[i].IsPlaying) state.playingCameraCount++;
                state.minFramesObserved = Mathf.Min(state.minFramesObserved, _frames[i]);
                state.maxFrameAgeSeconds = Mathf.Max(state.maxFrameAgeSeconds, Time.realtimeSinceStartup - _lastFrameTimes[i]);
            }
            return JsonUtility.ToJson(state);
        }

        [Serializable]
        private sealed class Snapshot
        {
            public string scene;
            public int cameraCount;
            public int playingCameraCount;
            public int minFramesObserved;
            public float maxFrameAgeSeconds;
            public int inferenceCount;
            public int errorCount;
            public bool menuVisible;
            public int menuSampleCount;
            public bool nativeQrHasPermissions;
            public int nativeQrTrackedCount;
            public bool nativeQrSupported;
            public bool nativeQrTrackingEnabled;
        }
    }
}
#endif
