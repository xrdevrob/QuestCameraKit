using System.Collections;
using Meta.XR;
using UnityEngine;

namespace QuestCameraKit.CameraMapping
{
    public class StereoCameraMappingController : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess leftCameraAccess;
        [SerializeField] private PassthroughCameraAccess rightCameraAccess;
        [SerializeField] private Material targetMaterial;
        [Header("Per-Eye UV Offset")]
        [SerializeField, Range(-0.2f, 0f)] private float leftUvOffsetX;
        [SerializeField, Range(-0.2f, 0f)] private float leftUvOffsetY;
        [SerializeField, Range(-0.2f, 0f)] private float rightUvOffsetX;
        [SerializeField, Range(-0.2f, 0f)] private float rightUvOffsetY;

        private static readonly int LeftTexId = Shader.PropertyToID("_LeftTex");
        private static readonly int RightTexId = Shader.PropertyToID("_RightTex");

        private static readonly int LeftCameraPosId = Shader.PropertyToID("_LeftCameraPos");
        private static readonly int RightCameraPosId = Shader.PropertyToID("_RightCameraPos");
        private static readonly int LeftCameraRotationMatrixId = Shader.PropertyToID("_LeftCameraRotationMatrix");
        private static readonly int RightCameraRotationMatrixId = Shader.PropertyToID("_RightCameraRotationMatrix");

        private static readonly int LeftFocalLengthId = Shader.PropertyToID("_LeftFocalLength");
        private static readonly int RightFocalLengthId = Shader.PropertyToID("_RightFocalLength");
        private static readonly int LeftPrincipalPointId = Shader.PropertyToID("_LeftPrincipalPoint");
        private static readonly int RightPrincipalPointId = Shader.PropertyToID("_RightPrincipalPoint");

        private static readonly int LeftSensorResolutionId = Shader.PropertyToID("_LeftSensorResolution");
        private static readonly int RightSensorResolutionId = Shader.PropertyToID("_RightSensorResolution");
        private static readonly int LeftCurrentResolutionId = Shader.PropertyToID("_LeftCurrentResolution");
        private static readonly int RightCurrentResolutionId = Shader.PropertyToID("_RightCurrentResolution");
        private static readonly int LeftUvOffsetId = Shader.PropertyToID("_LeftUvOffset");
        private static readonly int RightUvOffsetId = Shader.PropertyToID("_RightUvOffset");

        private IEnumerator Start()
        {
            leftCameraAccess = ResolveCamera(leftCameraAccess, PassthroughCameraAccess.CameraPositionType.Left);
            rightCameraAccess = ResolveCamera(rightCameraAccess, PassthroughCameraAccess.CameraPositionType.Right);

            if (!leftCameraAccess || !rightCameraAccess)
            {
                Debug.LogError("[StereoCameraMappingController] Left/Right PassthroughCameraAccess components are required.");
                yield break;
            }

            if (!targetMaterial)
            {
                Debug.LogError("[StereoCameraMappingController] Target material is not assigned.");
                yield break;
            }

            yield return new WaitUntil(() => leftCameraAccess.IsPlaying && rightCameraAccess.IsPlaying);

            targetMaterial.SetTexture(LeftTexId, leftCameraAccess.GetTexture());
            targetMaterial.SetTexture(RightTexId, rightCameraAccess.GetTexture());
            ApplyCalibrationToMaterial();

            UpdateEyeData(leftCameraAccess, true);
            UpdateEyeData(rightCameraAccess, false);
        }

        private void Update()
        {
            if (!targetMaterial || !leftCameraAccess || !rightCameraAccess || !leftCameraAccess.IsPlaying || !rightCameraAccess.IsPlaying)
            {
                return;
            }

            ApplyCalibrationToMaterial();

            var leftTexture = leftCameraAccess.GetTexture();
            if (leftTexture)
            {
                targetMaterial.SetTexture(LeftTexId, leftTexture);
            }

            var rightTexture = rightCameraAccess.GetTexture();
            if (rightTexture)
            {
                targetMaterial.SetTexture(RightTexId, rightTexture);
            }

            UpdateEyeData(leftCameraAccess, true);
            UpdateEyeData(rightCameraAccess, false);
        }

        private void UpdateEyeData(PassthroughCameraAccess cameraAccess, bool leftEye)
        {
            var pose = cameraAccess.GetCameraPose();
            var intrinsics = cameraAccess.Intrinsics;
            var currentResolution = cameraAccess.CurrentResolution;

            var cameraPositionId = leftEye ? LeftCameraPosId : RightCameraPosId;
            var cameraRotationId = leftEye ? LeftCameraRotationMatrixId : RightCameraRotationMatrixId;
            var focalLengthId = leftEye ? LeftFocalLengthId : RightFocalLengthId;
            var principalPointId = leftEye ? LeftPrincipalPointId : RightPrincipalPointId;
            var sensorResolutionId = leftEye ? LeftSensorResolutionId : RightSensorResolutionId;
            var currentResolutionId = leftEye ? LeftCurrentResolutionId : RightCurrentResolutionId;

            targetMaterial.SetVector(cameraPositionId, pose.position);
            targetMaterial.SetMatrix(cameraRotationId, Matrix4x4.Rotate(Quaternion.Inverse(pose.rotation)));
            targetMaterial.SetVector(focalLengthId, intrinsics.FocalLength);
            targetMaterial.SetVector(principalPointId, intrinsics.PrincipalPoint);
            targetMaterial.SetVector(sensorResolutionId, new Vector4(intrinsics.SensorResolution.x, intrinsics.SensorResolution.y, 0f, 0f));
            targetMaterial.SetVector(currentResolutionId, new Vector4(currentResolution.x, currentResolution.y, 0f, 0f));
        }

        private void ApplyCalibrationToMaterial()
        {
            targetMaterial.SetVector(LeftUvOffsetId, new Vector2(leftUvOffsetX, leftUvOffsetY));
            targetMaterial.SetVector(RightUvOffsetId, new Vector2(rightUvOffsetX, rightUvOffsetY));
        }

        private static PassthroughCameraAccess ResolveCamera(PassthroughCameraAccess configuredAccess, PassthroughCameraAccess.CameraPositionType cameraPosition)
        {
            if (configuredAccess)
            {
                if (configuredAccess.CameraPosition == cameraPosition)
                {
                    return configuredAccess;
                }

                Debug.LogWarning($"[StereoCameraMappingController] Assigned camera has position {configuredAccess.CameraPosition} but {cameraPosition} was expected.");
            }

            var allCameras = FindObjectsByType<PassthroughCameraAccess>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cameraAccess in allCameras)
            {
                if (cameraAccess && cameraAccess.CameraPosition == cameraPosition)
                {
                    return cameraAccess;
                }
            }

            return null;
        }
    }
}
