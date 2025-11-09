using Meta.XR;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if WEBRTC_ENABLED
using SimpleWebRTC;
#endif

namespace QuestCameraKit.WebRTC {
    public class WebRTCController : MonoBehaviour {
        [SerializeField] private PassthroughCameraAccess cameraAccess;
        [SerializeField] private RawImage canvasRawImage;
        [SerializeField] private GameObject connectionGameObject;
        [SerializeField] private bool adaptFovToCustomValue;
        [SerializeField] private float customFovValue;
        [SerializeField] private Camera[] streamingCameras;

#if WEBRTC_ENABLED
        private Texture _cameraTexture;
        private WebRTCConnection _webRTCConnection;

        private IEnumerator Start() {
            cameraAccess = ResolveCameraAccess(cameraAccess);
            yield return new WaitUntil(() => cameraAccess && cameraAccess.IsPlaying);
            if (!cameraAccess) {
                Debug.LogWarning("[WebRTCController] Passthrough camera unavailable.");
                yield break;
            }

            _webRTCConnection = connectionGameObject.GetComponent<WebRTCConnection>();
            _cameraTexture = cameraAccess.GetTexture();
            canvasRawImage.texture = _cameraTexture;
        }

        private void Update() {
            if (OVRInput.Get(OVRInput.Button.Start)) {
                _webRTCConnection.StartVideoTransmission();
            }

            if (adaptFovToCustomValue && streamingCameras[0].fieldOfView != customFovValue) {
                foreach (var camera in streamingCameras) {
                    camera.fieldOfView = customFovValue;
                }
            }

#if UNITY_EDITOR
            if (Input.GetKeyUp(KeyCode.Space)) {
                _webRTCConnection.StartVideoTransmission();
            }
#endif
        }
#endif

        private static PassthroughCameraAccess ResolveCameraAccess(PassthroughCameraAccess configuredAccess) {
            if (configuredAccess) {
                return configuredAccess;
            }

            return FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
        }
    }
}
