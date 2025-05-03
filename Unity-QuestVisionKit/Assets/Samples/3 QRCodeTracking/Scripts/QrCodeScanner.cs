using System.Collections.Generic;
using PassthroughCameraSamples;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using UnityEngine;
using System;
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
}

public class QrCodeScanner : MonoBehaviour
{
#if ZXING_ENABLED
    [SerializeField] private int sampleFactor = 2;
    [SerializeField] private QrCodeDetectionMode detectionMode = QrCodeDetectionMode.Single;
    [SerializeField] private ComputeShader downsampleShader;

    private WebCamTextureManager _webCamTextureManager;
    private RenderTexture _downsampledTexture;
    private Texture2D _webcamTextureCache;
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
        _webCamTextureManager = FindAnyObjectByType<WebCamTextureManager>();
        _qrReader = new QRCodeReader();
    }

    private void OnDestroy()
    {
        if (_downsampledTexture != null)
        {
            _downsampledTexture.Release();
            Destroy(_downsampledTexture);
        }
        if (_webcamTextureCache != null)
        {
            Destroy(_webcamTextureCache);
        }
    }

    private Texture2D GetOrCreateTexture(int width, int height)
    {
        if (_webcamTextureCache && _webcamTextureCache.width == width && _webcamTextureCache.height == height)
        {
            return _webcamTextureCache;
        }

        if (_webcamTextureCache)
        {
            Destroy(_webcamTextureCache);
        }
        
        _webcamTextureCache = new Texture2D(width, height, TextureFormat.RGBA32, false);
        return _webcamTextureCache;
    }

    public async Task<QrCodeResult[]> ScanFrameAsync()
    {
        if (_isScanning)
            return null;

        _isScanning = true;
        try
        {
            if (!_webCamTextureManager)
            {
                Debug.LogWarning("[QRCodeScanner] Camera helper is not assigned.");
                return null;
            }

            var webCamTex = _webCamTextureManager.WebCamTexture;
            while (!webCamTex || !webCamTex.isPlaying)
            {
                await Task.Delay(16);
                webCamTex = _webCamTextureManager.WebCamTexture;
            }

            var texture = GetOrCreateTexture(webCamTex.width, webCamTex.height);
            texture.SetPixels(webCamTex.GetPixels());
            texture.Apply();

            var originalWidth = texture.width;
            var originalHeight = texture.height;
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
            downsampleShader.SetTexture(kernel, Input1, texture);
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
                            return new[] { ProcessDecodeResult(decodeResult, targetWidth, targetHeight) };
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
                                results.Add(ProcessDecodeResult(decodeResult, targetWidth, targetHeight));
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

    private QrCodeResult ProcessDecodeResult(Result decodeResult, int targetWidth, int targetHeight)
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
            corners = uvCorners
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
}
