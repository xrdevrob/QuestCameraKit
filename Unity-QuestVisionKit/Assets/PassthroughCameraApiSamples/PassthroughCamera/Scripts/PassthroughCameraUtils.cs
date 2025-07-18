// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Assertions;

namespace PassthroughCameraSamples
{
    public static class PassthroughCameraUtils
    {
        // The Horizon OS starts supporting PCA with v74.
        public const int MINSUPPORTOSVERSION = 74;

        // The only pixel format supported atm
        private const int YUV_420_888 = 0x00000023;

        private static AndroidJavaObject s_currentActivity;
        private static AndroidJavaObject s_cameraManager;
        private static bool? s_isSupported;
        private static int? s_horizonOsVersion;

        // Caches
        internal static readonly Dictionary<PassthroughCameraEye, (string id, int index)> CameraEyeToCameraIdMap = new();
        private static readonly ConcurrentDictionary<PassthroughCameraEye, List<Vector2Int>> s_cameraOutputSizes = new();
        private static readonly ConcurrentDictionary<string, AndroidJavaObject> s_cameraCharacteristicsMap = new();
        private static readonly OVRPose?[] s_cachedCameraPosesRelativeToHead = new OVRPose?[2];

        /// <summary>
        /// Get the Horizon OS version number on the headset
        /// </summary>
        public static int? HorizonOSVersion
        {
            get
            {
                if (!s_horizonOsVersion.HasValue)
                {
                    var vrosClass = new AndroidJavaClass("vros.os.VrosBuild");
                    s_horizonOsVersion = vrosClass.CallStatic<int>("getSdkVersion");
#if OVR_INTERNAL_CODE
                    // 10000 means that the build doesn't have a proper release version, and it is still in Mainline,
                    // not in a release branch.
#endif // OVR_INTERNAL_CODE
                    if (s_horizonOsVersion == 10000)
                    {
                        s_horizonOsVersion = -1;
                    }
                }

                return s_horizonOsVersion.Value != -1 ? s_horizonOsVersion.Value : null;
            }
        }

        /// <summary>
        /// Returns true if the current headset supports Passthrough Camera API
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                if (!s_isSupported.HasValue)
                {
                    var headset = OVRPlugin.GetSystemHeadsetType();
                    s_isSupported = (headset == OVRPlugin.SystemHeadset.Meta_Quest_3 ||
                                     headset == OVRPlugin.SystemHeadset.Meta_Quest_3S) &&
                                    (!HorizonOSVersion.HasValue || HorizonOSVersion >= MINSUPPORTOSVERSION);
                }

                return s_isSupported.Value;
            }
        }

        /// <summary>
        /// Provides a list of resolutions supported by the passthrough camera. Developers should use one of those
        /// when initializing the camera.
        /// </summary>
        /// <param name="cameraEye">The passthrough camera</param>
        public static List<Vector2Int> GetOutputSizes(PassthroughCameraEye cameraEye)
        {
            return s_cameraOutputSizes.GetOrAdd(cameraEye, GetOutputSizesInternal(cameraEye));
        }

        /// <summary>
        /// Returns the camera intrinsics for a specified passthrough camera. All the intrinsics values are provided
        /// in pixels. The resolution value is the maximum resolution available for the camera.
        /// </summary>
        /// <param name="cameraEye">The passthrough camera</param>
        public static PassthroughCameraIntrinsics GetCameraIntrinsics(PassthroughCameraEye cameraEye)
        {
            var cameraCharacteristics = GetCameraCharacteristics(cameraEye);
            var intrinsicsArr = GetCameraValueByKey<float[]>(cameraCharacteristics, "LENS_INTRINSIC_CALIBRATION");

            // Querying the camera resolution for which the intrinsics are provided
            // https://developer.android.com/reference/android/hardware/camera2/CameraCharacteristics#SENSOR_INFO_PRE_CORRECTION_ACTIVE_ARRAY_SIZE
            // This is a Rect of 4 elements: [bottom, left, right, top] with (0,0) at top-left corner.
            var sensorSize = GetCameraValueByKey<AndroidJavaObject>(cameraCharacteristics, "SENSOR_INFO_PRE_CORRECTION_ACTIVE_ARRAY_SIZE");

            return new PassthroughCameraIntrinsics
            {
                FocalLength = new Vector2(intrinsicsArr[0], intrinsicsArr[1]),
                PrincipalPoint = new Vector2(intrinsicsArr[2], intrinsicsArr[3]),
                Resolution = new Vector2Int(sensorSize.Get<int>("right"), sensorSize.Get<int>("bottom")),
                Skew = intrinsicsArr[4]
            };
        }

        /// <summary>
        /// Returns an Android Camera2 API's cameraId associated with the passthrough camera specified in the argument.
        /// </summary>
        /// <param name="cameraEye">The passthrough camera</param>
        /// <exception cref="ApplicationException">Throws an exception if the code was not able to find cameraId</exception>
        public static string GetCameraIdByEye(PassthroughCameraEye cameraEye)
        {
            _ = EnsureInitialized();

            return !CameraEyeToCameraIdMap.TryGetValue(cameraEye, out var value)
                ? throw new ApplicationException($"Cannot find cameraId for the eye {cameraEye}")
                : value.id;
        }

        /// <summary>
        /// Returns the world pose of a passthrough camera at a given time.
        /// The LENS_POSE_TRANSLATION and LENS_POSE_ROTATION keys in 'android.hardware.camera2' are relative to the origin, so they can be cached to improve performance.
        /// </summary>
        /// <param name="cameraEye">The passthrough camera</param>
        /// <returns>The passthrough camera's world pose</returns>
        public static Pose GetCameraPoseInWorld(PassthroughCameraEye cameraEye)
        {
            var index = cameraEye == PassthroughCameraEye.Left ? 0 : 1;

            if (s_cachedCameraPosesRelativeToHead[index] == null)
            {
                var cameraId = GetCameraIdByEye(cameraEye);
                var cameraCharacteristics = s_cameraManager.Call<AndroidJavaObject>("getCameraCharacteristics", cameraId);

                var cameraTranslation = GetCameraValueByKey<float[]>(cameraCharacteristics, "LENS_POSE_TRANSLATION");
                var p_headFromCamera = new Vector3(cameraTranslation[0], cameraTranslation[1], -cameraTranslation[2]);

                var cameraRotation = GetCameraValueByKey<float[]>(cameraCharacteristics, "LENS_POSE_ROTATION");
                var q_cameraFromHead = new Quaternion(-cameraRotation[0], -cameraRotation[1], cameraRotation[2], cameraRotation[3]);

                var q_headFromCamera = Quaternion.Inverse(q_cameraFromHead);

                s_cachedCameraPosesRelativeToHead[index] = new OVRPose
                {
                    position = p_headFromCamera,
                    orientation = q_headFromCamera
                };
            }

            var headFromCamera = s_cachedCameraPosesRelativeToHead[index].Value;
            var worldFromHead = OVRPlugin.GetNodePoseStateImmediate(OVRPlugin.Node.Head).Pose.ToOVRPose();
            var worldFromCamera = worldFromHead * headFromCamera;
            worldFromCamera.orientation *= Quaternion.Euler(180, 0, 0);

            return new Pose(worldFromCamera.position, worldFromCamera.orientation);
        }

        /// <summary>
        /// Returns a 3D ray in the world space which starts from the passthrough camera origin and passes through the
        /// 2D camera pixel.
        /// </summary>
        /// <param name="cameraEye">The passthrough camera</param>
        /// <param name="screenPoint">A 2D point on the camera texture. The point is positioned relative to the
        ///     maximum available camera resolution. This resolution can be obtained using <see cref="GetCameraIntrinsics"/>
        ///     or <see cref="GetOutputSizes"/> methods.
        /// </param>
        public static Ray ScreenPointToRayInWorld(PassthroughCameraEye cameraEye, Vector2Int screenPoint)
        {
            var rayInCamera = ScreenPointToRayInCamera(cameraEye, screenPoint);
            var cameraPoseInWorld = GetCameraPoseInWorld(cameraEye);
            var rayDirectionInWorld = cameraPoseInWorld.rotation * rayInCamera.direction;
            return new Ray(cameraPoseInWorld.position, rayDirectionInWorld);
        }

        /// <summary>
        /// Returns a 3D ray in the camera space which starts from the passthrough camera origin - which is always
        /// (0, 0, 0) - and passes through the 2D camera pixel.
        /// </summary>
        /// <param name="cameraEye">The passthrough camera</param>
        /// <param name="screenPoint">A 2D point on the camera texture. The point is positioned relative to the
        /// maximum available camera resolution. This resolution can be obtained using <see cref="GetCameraIntrinsics"/>
        /// or <see cref="GetOutputSizes"/> methods.
        /// </param>
        public static Ray ScreenPointToRayInCamera(PassthroughCameraEye cameraEye, Vector2Int screenPoint)
        {
            var intrinsics = GetCameraIntrinsics(cameraEye);
            var directionInCamera = new Vector3
            {
                x = (screenPoint.x - intrinsics.PrincipalPoint.x) / intrinsics.FocalLength.x,
                y = (screenPoint.y - intrinsics.PrincipalPoint.y) / intrinsics.FocalLength.y,
                z = 1
            };

            return new Ray(Vector3.zero, directionInCamera);
        }

        #region Private methods

        internal static bool EnsureInitialized()
        {
            if (CameraEyeToCameraIdMap.Count == 2)
            {
                return true;
            }

            Debug.Log($"PCA: PassthroughCamera - Initializing...");
            using var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            s_currentActivity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
            s_cameraManager = s_currentActivity.Call<AndroidJavaObject>("getSystemService", "camera");
            Assert.IsNotNull(s_cameraManager, "Camera manager has not been provided by the Android system");

            var cameraIds = GetCameraIdList();
            Debug.Log($"PCA: PassthroughCamera - cameraId list is {string.Join(", ", cameraIds)}");

            for (var idIndex = 0; idIndex < cameraIds.Length; idIndex++)
            {
                var cameraId = cameraIds[idIndex];
                CameraSource? cameraSource = null;
                CameraPosition? cameraPosition = null;

                var cameraCharacteristics = GetCameraCharacteristics(cameraId);
                using var keysList = cameraCharacteristics.Call<AndroidJavaObject>("getKeys");
                var size = keysList.Call<int>("size");
                for (var i = 0; i < size; i++)
                {
                    using var key = keysList.Call<AndroidJavaObject>("get", i);
                    var keyName = key.Call<string>("getName");

                    if (string.Equals(keyName, "com.meta.extra_metadata.camera_source", StringComparison.OrdinalIgnoreCase))
                    {
                        // Both `com.meta.extra_metadata.camera_source` and `com.meta.extra_metadata.camera_source` are
                        // custom camera fields which are stored as arrays of size 1, instead of single values.
                        // We have to read those values correspondingly
                        var cameraSourceArr = GetCameraValueByKey<sbyte[]>(cameraCharacteristics, key);
                        if (cameraSourceArr == null || cameraSourceArr.Length != 1)
                            continue;

                        cameraSource = (CameraSource)cameraSourceArr[0];
                    }
                    else if (string.Equals(keyName, "com.meta.extra_metadata.position", StringComparison.OrdinalIgnoreCase))
                    {
                        var cameraPositionArr = GetCameraValueByKey<sbyte[]>(cameraCharacteristics, key);
                        if (cameraPositionArr == null || cameraPositionArr.Length != 1)
                            continue;

                        cameraPosition = (CameraPosition)cameraPositionArr[0];
                    }
                }

                if (!cameraSource.HasValue || !cameraPosition.HasValue || cameraSource.Value != CameraSource.Passthrough)
                    continue;

                switch (cameraPosition)
                {
                    case CameraPosition.Left:
                        Debug.Log($"PCA: Found left passthrough cameraId = {cameraId}");
                        CameraEyeToCameraIdMap[PassthroughCameraEye.Left] = (cameraId, idIndex);
                        break;
                    case CameraPosition.Right:
                        Debug.Log($"PCA: Found right passthrough cameraId = {cameraId}");
                        CameraEyeToCameraIdMap[PassthroughCameraEye.Right] = (cameraId, idIndex);
                        break;
                    default:
                        throw new ApplicationException($"Cannot parse Camera Position value {cameraPosition}");
                }
            }

            return CameraEyeToCameraIdMap.Count == 2;
        }

        private static string[] GetCameraIdList()
        {
            return s_cameraManager.Call<string[]>("getCameraIdList");
        }

        private static List<Vector2Int> GetOutputSizesInternal(PassthroughCameraEye cameraEye)
        {
            _ = EnsureInitialized();

            var cameraId = GetCameraIdByEye(cameraEye);
            var cameraCharacteristics = GetCameraCharacteristics(cameraId);
            using var configurationMap =
                GetCameraValueByKey<AndroidJavaObject>(cameraCharacteristics, "SCALER_STREAM_CONFIGURATION_MAP");
            var outputSizes = configurationMap.Call<AndroidJavaObject[]>("getOutputSizes", YUV_420_888);

            var result = new List<Vector2Int>();
            foreach (var outputSize in outputSizes)
            {
                var width = outputSize.Call<int>("getWidth");
                var height = outputSize.Call<int>("getHeight");
                result.Add(new Vector2Int(width, height));
            }

            foreach (var obj in outputSizes)
            {
                obj?.Dispose();
            }

            return result;
        }

        private static AndroidJavaObject GetCameraCharacteristics(string cameraId)
        {
            return s_cameraCharacteristicsMap.GetOrAdd(cameraId,
                _ => s_cameraManager.Call<AndroidJavaObject>("getCameraCharacteristics", cameraId));
        }

        private static AndroidJavaObject GetCameraCharacteristics(PassthroughCameraEye eye)
        {
            var cameraId = GetCameraIdByEye(eye);
            return GetCameraCharacteristics(cameraId);
        }

        private static T GetCameraValueByKey<T>(AndroidJavaObject cameraCharacteristics, string keyStr)
        {
            var key = cameraCharacteristics.GetStatic<AndroidJavaObject>(keyStr);
            return GetCameraValueByKey<T>(cameraCharacteristics, key);
        }

        private static T GetCameraValueByKey<T>(AndroidJavaObject cameraCharacteristics, AndroidJavaObject key)
        {
            return cameraCharacteristics.Call<T>("get", key);
        }

        private enum CameraSource
        {
            Passthrough = 0
        }

        private enum CameraPosition
        {
            Left = 0,
            Right = 1
        }

        #endregion Private methods
    }

    /// <summary>
    /// Contains camera intrinsics, which describe physical characteristics of a passthrough camera
    /// </summary>
    public struct PassthroughCameraIntrinsics
    {
        /// <summary>
        /// The focal length in pixels
        /// </summary>
        public Vector2 FocalLength;
        /// <summary>
        /// The principal point from the top-left corner of the image, expressed in pixels
        /// </summary>
        public Vector2 PrincipalPoint;
        /// <summary>
        /// The resolution in pixels for which the intrinsics are defined
        /// </summary>
        public Vector2Int Resolution;
        /// <summary>
        /// The skew coefficient which represents the non-perpendicularity of the image sensor's x and y axes
        /// </summary>
        public float Skew;
    }
}
