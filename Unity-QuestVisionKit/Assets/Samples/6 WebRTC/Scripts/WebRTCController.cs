using PassthroughCameraSamples;
#if WEBRTC_ENABLED
using SimpleWebRTC;
#endif
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace QuestCameraKit.WebRTC {
    public class WebRTCController : MonoBehaviour {
        [SerializeField] private WebCamTextureManager passthroughCameraManager;
        [SerializeField] private RawImage canvasRawImage;
        [SerializeField] private GameObject connectionGameObject;
        [SerializeField] private bool adaptFovToCustomValue;
        [SerializeField] private float customFovValue;
        [SerializeField] private Camera[] streamingCameras;

#if WEBRTC_ENABLED
        private WebCamTexture _webcamTexture;
        private WebRTCConnection _webRTCConnection;

        private IEnumerator Start() {
            yield return new WaitUntil(() => passthroughCameraManager.WebCamTexture != null && passthroughCameraManager.WebCamTexture.isPlaying);

            _webRTCConnection = connectionGameObject.GetComponent<WebRTCConnection>();
            _webcamTexture = passthroughCameraManager.WebCamTexture;
            canvasRawImage.texture = _webcamTexture;
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
    }
}