using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.XR;
using UnityEngine;
using UnityEngine.Rendering;
#if ZXING_ENABLED
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Multi;
#endif

public enum QrCodeDetectionMode
{
    Single,
    Multiple
}

[Serializable]
public class QrCodeResult
{
    public string text;
    public Vector3[] corners;
    public Pose cameraPose;
    public PassthroughCameraAccess.CameraIntrinsics intrinsics;
    public Vector2Int captureResolution;
}

public class QrCodeScanner : MonoBehaviour
{
#if ZXING_ENABLED
    [SerializeField] private int sampleFactor = 2;
    [SerializeField] private QrCodeDetectionMode detectionMode = QrCodeDetectionMode.Single;
    [SerializeField] private ComputeShader downsampleShader;
    [SerializeField] private PassthroughCameraAccess cameraAccess;

    private RenderTexture _downsampledTexture;
    private QRCodeReader _qrReader;
    private bool _isScanning;
    
    private static readonly int Input1 = Shader.PropertyToID("_Input");
    private static readonly int Output = Shader.PropertyToID("_Output");
    private static readonly int InputWidth = Shader.PropertyToID("_InputWidth");
    private static readonly int InputHeight = Shader.PropertyToID("_InputHeight");
    private static readonly int OutputWidth = Shader.PropertyToID("_OutputWidth");
    private static readonly int OutputHeight = Shader.PropertyToID("_OutputHeight");

    private void Awake()
    {
        cameraAccess = ResolveCameraAccess(cameraAccess);
        _qrReader = new QRCodeReader();
    }

    private void OnDestroy()
    {
        if (_downsampledTexture != null)
        {
            _downsampledTexture.Release();
            Destroy(_downsampledTexture);
        }
    }

    public async Task<QrCodeResult[]> ScanFrameAsync()
    {
        if (_isScanning)
            return null;

        _isScanning = true;
        try
        {
            while (true)
            {
                cameraAccess = ResolveCameraAccess(cameraAccess);
                if (cameraAccess && cameraAccess.IsPlaying)
                {
                    break;
                }
                await Task.Delay(16);
            }

            if (cameraAccess == null)
            {
                Debug.LogWarning("[QRCodeScanner] Passthrough camera is unavailable.");
                return null;
            }

            var cameraTexture = cameraAccess.GetTexture() as Texture2D;
            if (!cameraTexture)
            {
                Debug.LogWarning("[QRCodeScanner] Passthrough camera texture is not a Texture2D.");
                return null;
            }

            var capturePose = cameraAccess.GetCameraPose();
            var captureIntrinsics = cameraAccess.Intrinsics;
            var captureResolution = cameraAccess.CurrentResolution;

            var originalWidth = cameraTexture.width;
            var originalHeight = cameraTexture.height;
            var targetWidth = Mathf.Max(1, originalWidth / sampleFactor);
            var targetHeight = Mathf.Max(1, originalHeight / sampleFactor);

            if (!_downsampledTexture || _downsampledTexture.width != targetWidth || _downsampledTexture.height != targetHeight)
            {
                if (_downsampledTexture)
                {
                    _downsampledTexture.Release();
                }

                _downsampledTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.R8)
                {
                    enableRandomWrite = true
                };
                
                _downsampledTexture.Create();
            }

            var kernel = downsampleShader.FindKernel("CSMain");
            downsampleShader.SetTexture(kernel, Input1, cameraTexture);
            downsampleShader.SetTexture(kernel, Output, _downsampledTexture);
            downsampleShader.SetInt(InputWidth, originalWidth);
            downsampleShader.SetInt(InputHeight, originalHeight);
            downsampleShader.SetInt(OutputWidth, targetWidth);
            downsampleShader.SetInt(OutputHeight, targetHeight);

            var threadGroupsX = Mathf.CeilToInt(targetWidth / 8f);
            var threadGroupsY = Mathf.CeilToInt(targetHeight / 8f);
            downsampleShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

            var grayBytes = await ReadPixelsAsync(_downsampledTexture);
            var luminanceSource = new RGBLuminanceSource(grayBytes, targetWidth, targetHeight, RGBLuminanceSource.BitmapFormat.Gray8);
            var binaryBitmap = new BinaryBitmap(new HybridBinarizer(luminanceSource));

            return await Task.Run(() =>
            {
                try
                {
                    if (detectionMode == QrCodeDetectionMode.Single)
                    {
                        var decodeResult = _qrReader.decode(binaryBitmap);
                        if (decodeResult != null)
                            return new[] { ProcessDecodeResult(decodeResult, targetWidth, targetHeight, capturePose, captureIntrinsics, captureResolution) };
                    }
                    else
                    {
                        var multiReader = new GenericMultipleBarcodeReader(_qrReader);
                        var decodeResults = multiReader.decodeMultiple(binaryBitmap);
                        if (decodeResults != null)
                        {
                            var results = new List<QrCodeResult>();
                            foreach (var decodeResult in decodeResults)
                            {
                                results.Add(ProcessDecodeResult(decodeResult, targetWidth, targetHeight, capturePose, captureIntrinsics, captureResolution));
                            }

                            return results.ToArray();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[QRCodeScanner] Error decoding QR code(s): {ex.Message}");
                }
                return null;
            });
        }
        finally
        {
            _isScanning = false;
        }
    }

    private QrCodeResult ProcessDecodeResult(Result decodeResult, int targetWidth, int targetHeight, Pose capturePose, PassthroughCameraAccess.CameraIntrinsics captureIntrinsics, Vector2Int captureResolution)
    {
        var points = decodeResult.ResultPoints;
        var uvCorners = new Vector3[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            uvCorners[i] = new Vector3(points[i].X / targetWidth, points[i].Y / targetHeight, 0);
        }

        return new QrCodeResult
        {
            text = decodeResult.Text,
            corners = uvCorners,
            cameraPose = capturePose,
            intrinsics = captureIntrinsics,
            captureResolution = captureResolution
        };
    }

    private Task<byte[]> ReadPixelsAsync(RenderTexture rt)
    {
        var tcs = new TaskCompletionSource<byte[]>();

        AsyncGPUReadback.Request(rt, 0, TextureFormat.R8, request =>
        {
            if (request.hasError)
            {
                tcs.SetException(new Exception("GPU readback error."));
            }
            else
            {
                tcs.SetResult(request.GetData<byte>().ToArray());
            }
        });
        return tcs.Task;
    }
#endif

    private static PassthroughCameraAccess ResolveCameraAccess(PassthroughCameraAccess configuredAccess)
    {
        if (configuredAccess)
        {
            return configuredAccess;
        }

        return FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
    }
}
