using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Meta.XR;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Meta.XR.EnvironmentDepth;

#if XROCULUS_INSTALLED
using Unity.XR.Oculus;
#endif

#if META_OPENXR_INSTALLED
using UnityEngine.XR.OpenXR;
#endif

namespace xrdevrob.QuestCameraKit.AI
{
    [RequireComponent(typeof(EnvironmentDepthManager))]
    public class DepthTextureAccess : MonoBehaviour
    {

        /// <summary>
        /// Edge-aligned square size (in pixels) of the downsampled depth texture
        /// used for CPU readback and intersection tests.
        /// This is dynamically determined from the depth texture at runtime.
        /// </summary>
        /// <remarks>
        /// Only access this property after <see cref="IsInitialized"/> is true to ensure
        /// the texture size has been properly determined from the actual depth texture.
        /// </remarks>
        public int TextureSize { get; private set; } = 320;

        /// <summary>
        /// Indicates whether the depth texture has been initialized and TextureSize has been set.
        /// </summary>
        public bool IsInitialized { get; private set; }

        private const int NumEyes = 2;
        private static readonly int CopiedDepthTextureId = Shader.PropertyToID("_CopiedDepthTexture");
        private static readonly int EnvironmentDepthTextureId = Shader.PropertyToID("_EnvironmentDepthTexture");
        private static readonly int EnvironmentDepthTextureSizeId = Shader.PropertyToID("_EnvironmentDepthTextureSize");

        private static readonly int EnvironmentDepthZBufferParamsId =
            Shader.PropertyToID("_EnvironmentDepthZBufferParams");

        private static readonly int EnvironmentDepthReprojId =
            Shader.PropertyToID("_EnvironmentDepthReprojectionMatrices");

        public struct DepthFrameData
        {
            public NativeArray<float> DepthTexturePixels; // packed L then R; len = 320*320*2
            public Matrix4x4[] ViewProjectionMatrix; // [0]=L, [1]=R
            public Pose CameraPose; // Camera pose at the time depth was captured
        }

        /// <summary>
        /// Invoked when a new depth frame has been copied to CPU memory.
        /// Supplies a <see cref="DepthFrameData"/> struct with depth values
        /// and view/projection data for consumers such as
        /// <see cref="ObjectDetectionVisualizer"/>.
        /// </summary>
        public Action<DepthFrameData> OnDepthTextureUpdateCPU;

        private EnvironmentDepthManager _edm;
        private XRDisplaySubsystem _xrDisplay;
        private ComputeShader _computeShader;
        private ComputeBuffer _computeBuffer;

        private NativeArray<float> _depthTexturePixels;
        private NativeArray<float> _gpuRequestBuffer;
        private AsyncGPUReadbackRequest? _currentGpuReadbackRequest;
        private readonly Matrix4x4[] _matrixVp = new Matrix4x4[NumEyes];
        private Pose _cachedCameraPose;
        private PassthroughCameraAccess _cameraAccess;
#if XROCULUS_INSTALLED
        private uint? _prevOculusTextureId;
#endif

        private enum Backend
        {
            Unknown,
#if XROCULUS_INSTALLED
            OculusXR,
#endif
#if META_OPENXR_INSTALLED
            OpenXRMeta
#endif
        }

        private Backend _backend = Backend.Unknown;

        private void Awake()
        {
            _edm = GetComponent<EnvironmentDepthManager>();
            if (!EnvironmentDepthManager.IsSupported)
            {
                Debug.Log("Environment Depth not supported on this device.");
                enabled = false;
                return;
            }

            var displays = new List<XRDisplaySubsystem>(1);
            SubsystemManager.GetSubsystems(displays);
            _xrDisplay = displays.FirstOrDefault();
            Assert.IsNotNull(_xrDisplay, nameof(_xrDisplay));

            _backend = DetectBackend();

            const string shaderName = "CopyDepthTextureIntoNativeArray";
            _computeShader = Resources.Load<ComputeShader>(shaderName);
            Assert.IsNotNull(_computeShader, $"Compute shader '{shaderName}' not found under Resources/.");

            _cameraAccess = FindAnyObjectByType<PassthroughCameraAccess>();
        }

        private void OnDestroy()
        {
            if (_currentGpuReadbackRequest is { done: false })
            {
                _currentGpuReadbackRequest.Value.WaitForCompletion();
            }

            _computeBuffer?.Dispose();
            if (_depthTexturePixels.IsCreated)
            {
                _depthTexturePixels.Dispose();
            }

            if (_gpuRequestBuffer.IsCreated)
            {
                _gpuRequestBuffer.Dispose();
            }
        }

        /// <summary>
        /// Triggers a depth sample immediately. Called by inference agents
        /// to synchronize depth sampling with inference runs.
        /// </summary>
        public void RequestDepthSample()
        {
            SampleDepth();
        }

        private void SampleDepth()
        {
            if (!(_edm.enabled && _edm.IsDepthAvailable))
            {
                return;
            }

            if (!TryFetchDepthTexture(out var depthTexture))
            {
                return;
            }

            if (_cameraAccess)
            {
                _cachedCameraPose = _cameraAccess.GetCameraPose();
            }

            if (!_depthTexturePixels.IsCreated || TextureSize != depthTexture.width)
            {
                if (_depthTexturePixels.IsCreated)
                {
                    _computeBuffer?.Dispose();
                    _depthTexturePixels.Dispose();
                    _gpuRequestBuffer.Dispose();
                }

                TextureSize = depthTexture.width;
                var numPixels = TextureSize * TextureSize * NumEyes;
                _computeBuffer = new ComputeBuffer(numPixels, sizeof(float));
                _depthTexturePixels = new NativeArray<float>(numPixels, Allocator.Persistent);
                _gpuRequestBuffer = new NativeArray<float>(numPixels, Allocator.Persistent);
                IsInitialized = true;
            }

            _computeShader.SetTexture(0, EnvironmentDepthTextureId, depthTexture);
            _computeShader.SetFloat(EnvironmentDepthTextureSizeId, depthTexture.width);
            Assert.AreEqual(depthTexture.width, depthTexture.height, "Environment depth RT expected square");

            var zParams = Shader.GetGlobalVector(EnvironmentDepthZBufferParamsId);
            _computeShader.SetVector(EnvironmentDepthZBufferParamsId, zParams);
            var reprojectionMatrix = Shader.GetGlobalMatrixArray(EnvironmentDepthReprojId);
            for (var i = 0; i < Mathf.Min(reprojectionMatrix.Length, NumEyes); i++)
            {
                _matrixVp[i] = reprojectionMatrix[i];
            }

            _computeShader.SetBuffer(0, CopiedDepthTextureId, _computeBuffer);
            _computeShader.Dispatch(0, 1, 1, 1);

            if (_currentGpuReadbackRequest.HasValue)
            {
                return;
            }

            _currentGpuReadbackRequest = AsyncGPUReadback.RequestIntoNativeArray(ref _gpuRequestBuffer, _computeBuffer,
                req =>
                {
                    if (req.hasError)
                    {
                        Debug.LogWarning("Depth GPU readback error; skipping frame.");
                        _currentGpuReadbackRequest = null;
                        return;
                    }

                    var tmp = req.GetData<float>();
                    (_depthTexturePixels, _) = (tmp, _depthTexturePixels);

                    OnDepthTextureUpdateCPU?.Invoke(new DepthFrameData
                    {
                        DepthTexturePixels = _depthTexturePixels,
                        ViewProjectionMatrix = _matrixVp,
                        CameraPose = _cachedCameraPose
                    });

                    _currentGpuReadbackRequest = null;
                });
        }

        private static Backend DetectBackend()
        {
#if META_OPENXR_INSTALLED
            if (!string.IsNullOrEmpty(OpenXRRuntime.name))
            {
                return Backend.OpenXRMeta;
            }
#endif
#if XROCULUS_INSTALLED
            if (Utils.GetEnvironmentDepthSupported())
            {
                return Backend.OculusXR;
            }
#endif
            Debug.LogWarning("No supported depth backend detected.");
            return Backend.Unknown;
        }

        private bool TryFetchDepthTexture(out RenderTexture depthTexture)
        {
            depthTexture = null;
            if (_xrDisplay == null || !_xrDisplay.running)
            {
                return false;
            }

            switch (_backend)
            {
#if XROCULUS_INSTALLED
                case Backend.OculusXR:
                    return TryFetchOculus(out depthTexture);
#endif
#if META_OPENXR_INSTALLED
                case Backend.OpenXRMeta:
                    return TryFetchOpenXR(out depthTexture);
#endif
                case Backend.Unknown:
                default:
                    return false;
            }
        }

#if XROCULUS_INSTALLED
        private bool TryFetchOculus(out RenderTexture depthTexture)
        {
            depthTexture = null;
            uint texId = 0;
            if (!Utils.GetEnvironmentDepthTextureId(ref texId)) return false;
            if (_prevOculusTextureId == texId) return false;
            _prevOculusTextureId = texId;
            depthTexture = _xrDisplay.GetRenderTexture(texId);
            return depthTexture != null && depthTexture.IsCreated();
        }
#endif

#if META_OPENXR_INSTALLED
        private static bool TryFetchOpenXR(out RenderTexture depthTexture)
        {
            depthTexture = Shader.GetGlobalTexture(EnvironmentDepthTextureId) as RenderTexture;
            return depthTexture && depthTexture.IsCreated();
        }
#endif
    }
}
