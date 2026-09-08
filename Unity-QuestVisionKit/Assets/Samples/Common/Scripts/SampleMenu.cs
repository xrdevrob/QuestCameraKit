using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace QuestCameraKit
{
    public sealed class SampleMenu : MonoBehaviour
    {
        private string[] _paths;
        private Canvas _canvas;
        private Text _text;
        private Camera _camera;
        private int _selected;
        private bool _stickHeld;
        private bool _loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (AvailableScenes().Length > 1)
                new GameObject("Sample menu").AddComponent<SampleMenu>();
        }

        private void Awake()
        {
            _paths = AvailableScenes();
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            var panel = new GameObject("Samples", typeof(RectTransform), typeof(Canvas), typeof(Image));
            panel.transform.SetParent(transform, false);
            _canvas = panel.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(720, 640);
            rect.localScale = Vector3.one * 0.0012f;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.065f, 0.97f);
            var label = new GameObject("Sample list", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            _text = label.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 27;
            _text.color = Color.white;
            _text.supportRichText = true;
            _text.alignment = TextAnchor.MiddleLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.rectTransform.anchorMin = Vector2.zero;
            _text.rectTransform.anchorMax = Vector2.one;
            _text.rectTransform.offsetMin = new Vector2(32, 24);
            _text.rectTransform.offsetMax = new Vector2(-32, -24);
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= SceneLoaded;

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _camera = Camera.main;
            _selected = Mathf.Max(0, System.Array.IndexOf(_paths, scene.path));
            _loading = false;
            Refresh();
        }

        public static string[] AvailableScenes()
        {
#if UNITY_EDITOR
            // Editor sceneCountInBuildSettings also includes disabled scenes.
            return UnityEditor.EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
#else
            return Enumerable.Range(0, SceneManager.sceneCountInBuildSettings).Select(SceneUtility.GetScenePathByBuildIndex).ToArray();
#endif
        }

        public static string Label(string scenePath)
        {
            return Path.GetFileNameWithoutExtension(scenePath) switch
            {
                "ColorPicker" => "Color picker",
                "ObjectDetection" => "Object detection",
                "QRCodeTracking" => "QR from camera (ZXing)",
                "CameraMappingForShaders" => "Camera shaders",
                "ImageLLM" => "Image + voice AI",
                "WebRTC-Quest" => "WebRTC streaming",
                "QRCodeDetection" => "QR tracking (Meta native)",
                var name => name
            };
        }

        private void Update()
        {
            if (_loading) return;
            var keyboard = Keyboard.current;
            if (OVRInput.GetDown(OVRInput.RawButton.Y) || keyboard?.mKey.wasPressedThisFrame == true)
            {
                _canvas.enabled = !_canvas.enabled;
                _stickHeld = true;
            }
            if (!_canvas.enabled) return;
            var axis = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick).y;
            var direction = keyboard?.downArrowKey.wasPressedThisFrame == true ? 1 : keyboard?.upArrowKey.wasPressedThisFrame == true ? -1 : 0;
            if (!_stickHeld && Mathf.Abs(axis) > 0.6f) direction = axis < 0 ? 1 : -1;
            _stickHeld = Mathf.Abs(axis) > 0.3f;
            if (direction != 0)
            {
                _selected = (_selected + direction + _paths.Length) % _paths.Length;
                Refresh();
            }
            if (OVRInput.GetDown(OVRInput.RawButton.X) || keyboard?.enterKey.wasPressedThisFrame == true)
            {
                _canvas.enabled = false;
                if (_paths[_selected] == SceneManager.GetActiveScene().path) return;
                _loading = true;
                SceneManager.LoadSceneAsync(_paths[_selected], LoadSceneMode.Single);
            }
        }

        private void LateUpdate()
        {
            if (!_camera) _camera = Camera.main;
            if (!_camera || !_canvas.enabled) return;
            _canvas.worldCamera = _camera;
            _canvas.transform.SetPositionAndRotation(_camera.transform.position + _camera.transform.forward * 1.15f,
                _camera.transform.rotation);
        }

        private void Refresh()
        {
            var content = "<size=36><b>QuestCameraKit</b></size>\n\n";
            for (var i = 0; i < _paths.Length; i++)
            {
                var name = Label(_paths[i]);
                content += i == _selected ? "<color=#74DEFF>› <b>" + name + "</b></color>\n" : "   " + name + "\n";
            }
            _text.text = content + "\n<size=22>Left stick: choose   X: open   Y: menu\nEditor: ↑/↓, Enter, M</size>";
        }
    }
}
