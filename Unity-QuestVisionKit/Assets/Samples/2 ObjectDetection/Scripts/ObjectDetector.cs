using System;
using UnityEngine;

using System.Collections;
using PassthroughCameraSamples;

public class ObjectDetector : MonoBehaviour
{
    [Header("Environment Sampling")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset sentisModel;
    [SerializeField] private Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.CPU;
    [SerializeField] private float inferenceInterval = 0.1f;
    [SerializeField] private int kLayersPerFrame = 20;
    
    [Header("Detection Settings")]
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private ObjectRenderer objectRenderer;

    private Unity.InferenceEngine.Model _model;
    private Unity.InferenceEngine.Worker _engine;
    private Texture2D _cpuTexture;
    private Coroutine _inferenceCoroutine;
    private WebCamTexture _webcamTexture;
    private const int InputSize = 640;

    private void Start()
    {
        print("[ObjectDetector] Starting up and acquiring webcam texture.");
        _webcamTexture = webCamTextureManager.WebCamTexture;
        if (_webcamTexture != null)
        {
            _cpuTexture = new Texture2D(_webcamTexture.width, _webcamTexture.height, TextureFormat.RGBA32, false);
            print($"[ObjectDetector] WebCamTexture dimensions: {_webcamTexture.width}x{_webcamTexture.height}");
        }
        else
        {
            Debug.LogError("[ObjectDetector] WebCamTexture is null at Start.");
        }

        LoadModel();
        _inferenceCoroutine = StartCoroutine(InferenceLoop());
    }

    private void OnDestroy()
    {
        if (_inferenceCoroutine != null)
        {
            StopCoroutine(_inferenceCoroutine);
            _inferenceCoroutine = null;
        }
        _engine?.Dispose();
        if (_cpuTexture != null)
        {
            Destroy(_cpuTexture);
            _cpuTexture = null;
        }
        
        print("[ObjectDetector] Destroyed and cleaned up.");
    }

    private void LoadModel()
    {
        try
        {
            _model = Unity.InferenceEngine.ModelLoader.Load(sentisModel);
            _engine = new Unity.InferenceEngine.Worker(_model, backend);
            print("[ObjectDetector] Model loaded successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[ObjectDetector] Failed to load model: " + e.Message);
        }
    }

    private IEnumerator InferenceLoop()
    {
        while (isActiveAndEnabled)
        {
            if (!_webcamTexture)
            {
                _webcamTexture = webCamTextureManager.WebCamTexture;
                
                if (_webcamTexture)
                {
                    _cpuTexture = new Texture2D(_webcamTexture.width, _webcamTexture.height, TextureFormat.RGBA32, false);
                    print("[ObjectDetector] WebCamTexture is now available; CPU texture created.");
                }
            }

            yield return new WaitForSeconds(inferenceInterval);

            if (!_cpuTexture)
            {
                Debug.LogWarning("[ObjectDetector] CPU texture is null, skipping iteration.");
                continue;
            }

            _cpuTexture.SetPixels(_webcamTexture.GetPixels());
            _cpuTexture.Apply();

            print("[ObjectDetector] Running inference iteration.");
            yield return StartCoroutine(PerformInference(_cpuTexture));
        }
    }

    private IEnumerator PerformInference(Texture texture)
    {
        var inputTensor = Unity.InferenceEngine.TextureConverter.ToTensor(texture, InputSize, InputSize, 3);
        print("[ObjectDetector] Input tensor created.");

        var schedule = _engine.ScheduleIterable(inputTensor);
        if (schedule == null)
        {
            Debug.LogWarning("[ObjectDetector] ScheduleIterable returned null; falling back to synchronous scheduling.");
            _engine.Schedule(inputTensor);
        }
        else
        {
            var it = 0;
            while (schedule.MoveNext())
            {
                if (++it % kLayersPerFrame == 0)
                    yield return null;
            }
            
            Debug.Log("[ObjectDetector] Inference schedule complete.");
        }

        Unity.InferenceEngine.Tensor<float> coordsOutput = null;
        Unity.InferenceEngine.Tensor<int> labelIDsOutput = null;
        Unity.InferenceEngine.Tensor<float> pullCoords = _engine.PeekOutput(0) as Unity.InferenceEngine.Tensor<float>;
        Unity.InferenceEngine.Tensor<int> pullLabelIDs = _engine.PeekOutput(1) as Unity.InferenceEngine.Tensor<int>;

        var isWaiting = false;
        var downloadState = 0;
        
        while (true)
        {
            switch (downloadState)
            {
                case 0:
                    if (pullCoords?.dataOnBackend == null)
                    {
                        Debug.LogError("[ObjectDetector] Coordinates output is null or missing backend data.");
                        inputTensor.Dispose();
                        yield break;
                    }
                    if (!isWaiting)
                    {
                        pullCoords.ReadbackRequest();
                        isWaiting = true;
                    }
                    else if (pullCoords.IsReadbackRequestDone())
                    {
                        coordsOutput = pullCoords.ReadbackAndClone();
                        isWaiting = false;
                        downloadState = 1;
                    }
                    break;
                case 1:
                    if (pullLabelIDs?.dataOnBackend == null)
                    {
                        Debug.LogError("[ObjectDetector] LabelIDs output is null or missing backend data.");
                        inputTensor.Dispose();
                        coordsOutput?.Dispose();
                        yield break;
                    }
                    if (!isWaiting)
                    {
                        pullLabelIDs.ReadbackRequest();
                        isWaiting = true;
                    }
                    else if (pullLabelIDs.IsReadbackRequestDone())
                    {
                        labelIDsOutput = pullLabelIDs.ReadbackAndClone();
                        isWaiting = false;
                        downloadState = 2;
                    }
                    break;
                case 2:
                    print("[ObjectDetector] Rendering detections.");
                    if (objectRenderer)
                    {
                        objectRenderer.RenderDetections(
                            coordsOutput, 
                            labelIDsOutput
                        );
                    }
                    downloadState = 3;
                    break;
                case 3:
                    print("[ObjectDetector] Inference iteration complete.");
                    inputTensor.Dispose();
                    coordsOutput?.Dispose();
                    labelIDsOutput?.Dispose();
                    yield break;
            }
            yield return null;
        }
    }
}
