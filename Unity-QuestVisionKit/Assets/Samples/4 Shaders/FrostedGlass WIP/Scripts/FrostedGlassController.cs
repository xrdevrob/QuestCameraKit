using System.Collections;
using Meta.XR;
using UnityEngine;

namespace QuestCameraKit.FrostedGlass
{
    public class FrostedGlassController : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess passthroughCamera;
        [SerializeField] private Material baseMapMaterial;

        private Texture _cameraTexture;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private IEnumerator Start()
        {
            passthroughCamera = ResolveCameraAccess(passthroughCamera);
            yield return new WaitUntil(() => passthroughCamera && passthroughCamera.IsPlaying);

            if (!passthroughCamera)
            {
                Debug.LogWarning("[FrostedGlassController] Passthrough camera not located.");
                yield break;
            }

            _cameraTexture = passthroughCamera.GetTexture();
            if (_cameraTexture)
            {
                baseMapMaterial.SetTexture(BaseMapId, _cameraTexture);
            }
            else
            {
                Debug.LogWarning("[FrostedGlassController] Passthrough texture unavailable.");
            }
        }

        private static PassthroughCameraAccess ResolveCameraAccess(PassthroughCameraAccess configuredAccess)
        {
            if (configuredAccess)
            {
                return configuredAccess;
            }

            return FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
        }
    }
}
