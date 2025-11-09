using System.Collections;
using UnityEngine;
using Meta.XR;
using System;

public class ObjectDetector : MonoBehaviour
{
    [Header("Environment Sampling")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset sentisModel;
    [SerializeField] private Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.CPU;
    [SerializeField] private float inferenceInterval = 0.1f;
    [SerializeField] private int kLayersPerFrame = 20;
    
    private PassthroughCameraAccess _cameraAccess;
    private Unity.InferenceEngine.Model _model;
    private Unity.InferenceEngine.Worker _engine;
    private ObjectRenderer _objectRenderer;
    private Coroutine _inferenceCoroutine;
    private Texture _cameraTexture;
    private const int InputSize = 640;

    private void Start()
    {
        _cameraAccess = GetComponent<PassthroughCameraAccess>();
        _objectRenderer = GetComponent<ObjectRenderer>();
        
        if (!_cameraAccess || !_objectRenderer)
        {
            Debug.LogError("[ObjectDetector] PassthroughCameraAccess or Object Renderer not found in the scene.");
            return;
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
            if (!TryEnsureCameraTexture())
            {
                yield return null;
                continue;
            }

            yield return new WaitForSeconds(inferenceInterval);

            yield return StartCoroutine(PerformInference(_cameraTexture));
        }
    }

    private IEnumerator PerformInference(Texture texture)
    {
        var tensorShape = new Unity.InferenceEngine.TensorShape(1, 3, InputSize, InputSize);
        var inputTensor = new Unity.InferenceEngine.Tensor<float>(tensorShape);
        Unity.InferenceEngine.TextureConverter.ToTensor(texture, inputTensor);
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
                    if (_objectRenderer)
                    {
                        _objectRenderer.RenderDetections(
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

    private bool TryEnsureCameraTexture()
    {
        if (!_cameraAccess || !_cameraAccess.IsPlaying)
        {
            return false;
        }

        if (_cameraTexture)
        {
            return true;
        }

        _cameraTexture = _cameraAccess.GetTexture();
        if (_cameraTexture)
        {
            var resolution = _cameraAccess.CurrentResolution;
            print($"[ObjectDetector] Passthrough texture ready: {resolution.x}x{resolution.y}");
        }
        else
        {
            Debug.LogWarning("[ObjectDetector] Passthrough texture not available yet.");
        }

        return _cameraTexture;
    }
}
