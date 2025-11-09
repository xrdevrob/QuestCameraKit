using System.Collections;
using System.Linq;
using Meta.XR;
using Unity.Collections;
using UnityEngine;

namespace QuestCameraKit.OpenAI
{
    public class VoiceCommandHandler : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess cameraAccess;
        [SerializeField] private ImageOpenAIConnector imageConnector;

        [Header("Image (For testing in Editor)")]
        [Tooltip("Assign a dummy image for testing in the editor or if no webcam is available.")]
        [SerializeField]
        private Texture2D dummyImage;

        private SttManager _sttManager;

        private void Awake()
        {
            _sttManager = FindFirstObjectByType<SttManager>();
            if (_sttManager == null)
            {
                Debug.LogError("STTActivation not found in the scene.");
                return;
            }

            _sttManager.OnTranscriptionComplete += OnTranscriptionReceived;
        }

        private void OnDestroy()
        {
            if (_sttManager != null)
            {
                _sttManager.OnTranscriptionComplete -= OnTranscriptionReceived;
            }
        }

        private void OnTranscriptionReceived(string transcription)
        {
            StartCoroutine(CaptureAndSendImage(transcription));
        }

        private IEnumerator CaptureAndSendImage(string transcription)
        {
            Texture2D capturedTexture;

            if (Application.isEditor || !TryResolveCameraAccess(out var access) || !access.IsPlaying)
            {
                if (dummyImage)
                {
                    capturedTexture = dummyImage;
                }
                else
                {
                    capturedTexture = new Texture2D(512, 512, TextureFormat.RGB24, false);
                    var fillColor = Color.gray;
                    var fillPixels = Enumerable.Repeat(fillColor, 512 * 512).ToArray();
                    capturedTexture.SetPixels(fillPixels);
                    capturedTexture.Apply();
                }
            }
            else
            {
                // Delay so we can avoid having our controller or hand in the image
                yield return new WaitForSeconds(1.0f);
                yield return new WaitForEndOfFrame();
                capturedTexture = CapturePassthroughFrame(access);
                if (!capturedTexture)
                {
                    Debug.LogWarning("VoiceCommandHandler: Failed to capture passthrough frame, falling back to dummy image.");
                    capturedTexture = dummyImage ? dummyImage : CreateFallbackTexture();
                }
            }

            if (imageConnector)
            {
                imageConnector.SendImage(capturedTexture, transcription);
            }
            else
            {
                Debug.LogError("ImageOpenAIConnector not assigned in VoiceCommandHandler.");
            }
        }

        private bool TryResolveCameraAccess(out PassthroughCameraAccess access)
        {
            access = cameraAccess ? cameraAccess : FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
            cameraAccess = access;
            return access;
        }

        private static Texture2D CreateFallbackTexture()
        {
            var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var fillColor = Color.gray;
            var fillPixels = Enumerable.Repeat(fillColor, 512 * 512).ToArray();
            texture.SetPixels(fillPixels);
            texture.Apply();
            return texture;
        }

        private Texture2D CapturePassthroughFrame(PassthroughCameraAccess access)
        {
            var resolution = access.CurrentResolution;
            if (resolution == Vector2Int.zero)
            {
                return null;
            }

            var colors = access.GetColors();
            if (!colors.IsCreated)
            {
                return null;
            }

            var texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            texture.SetPixelData(colors, 0);
            texture.Apply();
            return texture;
        }
    }
}
