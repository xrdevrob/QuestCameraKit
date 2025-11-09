using System.Collections;
using Meta.XR;
using UnityEngine;

namespace QuestCameraKit.FrostedGlass
{
    public class CameraTextureMapper : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess passthroughCamera;
        [SerializeField] private Material mappingBlurMaterial;
        [SerializeField] private Color tintColor = Color.white;

        private Texture _cameraTexture;
        private bool _cameraFound = true;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int TextureSizeId = Shader.PropertyToID("_TextureSize");
        private static readonly int CameraPosId = Shader.PropertyToID("_CameraPos");
        private static readonly int FocalLengthId = Shader.PropertyToID("_FocalLength");
        private static readonly int PrincipalPointId = Shader.PropertyToID("_PrincipalPoint");
        private static readonly int IntrinsicResolutionId = Shader.PropertyToID("_IntrinsicResolution");
        private static readonly int CameraRotationMatrixId = Shader.PropertyToID("_CameraRotationMatrix");

        private IEnumerator Start()
        {
            passthroughCamera = ResolveCameraAccess(passthroughCamera);
            yield return new WaitUntil(() => passthroughCamera && passthroughCamera.IsPlaying);

            if (!passthroughCamera)
            {
                Debug.LogWarning("[CameraTextureMapper] Passthrough camera not found.");
                yield break;
            }

            _cameraTexture = passthroughCamera.GetTexture();
            if (!_cameraTexture)
            {
                Debug.LogWarning("[CameraTextureMapper] Passthrough texture missing.");
                yield break;
            }

            mappingBlurMaterial.SetTexture(MainTexId, _cameraTexture);
            mappingBlurMaterial.SetColor(TintColorId, tintColor);

            var intrinsics = passthroughCamera.Intrinsics;
            mappingBlurMaterial.SetVector(IntrinsicResolutionId,
                new Vector4(intrinsics.SensorResolution.x, intrinsics.SensorResolution.y, 0, 0));
        }

        private void Update()
        {
            if (!_cameraTexture || !_cameraFound || !passthroughCamera || !passthroughCamera.IsPlaying) return;

            var resolution = passthroughCamera.CurrentResolution;
            var texSize = new Vector2(resolution.x, resolution.y);
            mappingBlurMaterial.SetVector(TextureSizeId, texSize);

            try
            {
                var camPose = passthroughCamera.GetCameraPose();
                mappingBlurMaterial.SetVector(CameraPosId, camPose.position);
                mappingBlurMaterial.SetMatrix(CameraRotationMatrixId,
                    Matrix4x4.Rotate(Quaternion.Inverse(camPose.rotation)));

                var intrinsics = passthroughCamera.Intrinsics;
                mappingBlurMaterial.SetVector(FocalLengthId, intrinsics.FocalLength);
                mappingBlurMaterial.SetVector(PrincipalPointId, intrinsics.PrincipalPoint);
                mappingBlurMaterial.SetVector(IntrinsicResolutionId,
                    new Vector4(intrinsics.SensorResolution.x, intrinsics.SensorResolution.y, 0, 0));
            }
            catch (System.ApplicationException)
            {
                _cameraFound = false;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_cameraTexture == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            Graphics.Blit(_cameraTexture, destination, mappingBlurMaterial);
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
