using System;
using System.Collections;
using Meta.XR;
using Unity.InferenceEngine;
using UnityEngine;

public class ObjectDetector : MonoBehaviour
{
    [Header("Inference")]
    [SerializeField] private ModelAsset sentisModel;
    [SerializeField] private BackendType backend = BackendType.CPU;
    [SerializeField, Min(0f)] private float inferenceInterval = 0.1f;
    [SerializeField, Min(1)] private int kLayersPerFrame = 20;

    private PassthroughCameraAccess _cameraAccess;
    private ObjectRenderer _objectRenderer;
    private Model _model;
    private Worker _engine;
    private Tensor<float> _input;
    private Tensor<float> _coords;
    private Tensor<int> _labels;
    private Tensor<float> _confidences;
    private const int InputSize = 640;
    public int CompletedInferenceCount { get; private set; }

    private void OnEnable()
    {
        _cameraAccess = GetComponent<PassthroughCameraAccess>();
        _objectRenderer = GetComponent<ObjectRenderer>();
        if (!_cameraAccess || !_objectRenderer || !sentisModel)
        {
            Debug.LogError("[ObjectDetector] Assign a model, PassthroughCameraAccess and ObjectRenderer.");
            enabled = false;
            return;
        }

        try
        {
            _model = ModelLoader.Load(sentisModel);
            if (_model.outputs.Count < 2)
                throw new InvalidOperationException("Model needs coordinates and integer label outputs.");
            _engine = new Worker(_model, backend);
            _input = new Tensor<float>(new TensorShape(1, 3, InputSize, InputSize));
        }
        catch (Exception e)
        {
            Debug.LogError("[ObjectDetector] Failed to load model: " + e.Message);
            enabled = false;
            return;
        }

        StartCoroutine(InferenceLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ReleaseReadbacks();
        _engine?.Dispose();
        _engine = null;
        _input?.Dispose();
        _input = null;
    }

    private void ReleaseReadbacks()
    {
        _coords?.Dispose();
        _labels?.Dispose();
        _confidences?.Dispose();
        _coords = null;
        _labels = null;
        _confidences = null;
    }

    private IEnumerator InferenceLoop()
    {
        while (isActiveAndEnabled)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, inferenceInterval));
            if (!_cameraAccess || !_cameraAccess.IsPlaying) continue;
            // Camera textures can be replaced after a pause or resolution change.
            var texture = _cameraAccess.GetTexture();
            if (!texture) continue;
            yield return PerformInference(texture, _cameraAccess.GetCameraPose());
        }
    }

    private IEnumerator PerformInference(Texture texture, Pose capturePose)
    {
        TextureConverter.ToTensor(texture, _input);
        var schedule = _engine.ScheduleIterable(_input);
        if (schedule == null)
        {
            _engine.Schedule(_input);
        }
        else
        {
            var layers = 0;
            while (schedule.MoveNext())
            {
                if (++layers % Mathf.Max(1, kLayersPerFrame) == 0) yield return null;
            }
        }

        var coords = _engine.PeekOutput(0) as Tensor<float>;
        var labels = _engine.PeekOutput(1) as Tensor<int>;
        var confidences = _model.outputs.Count > 2 ? _engine.PeekOutput(2) as Tensor<float> : null;
        if (coords?.dataOnBackend == null || labels?.dataOnBackend == null)
        {
            Debug.LogError("[ObjectDetector] Model must output float coordinates and integer label IDs.");
            enabled = false;
            yield break;
        }

        coords.ReadbackRequest();
        labels.ReadbackRequest();
        confidences?.ReadbackRequest();
        while (!coords.IsReadbackRequestDone() || !labels.IsReadbackRequestDone() ||
               (confidences != null && !confidences.IsReadbackRequestDone()))
        {
            yield return null;
        }

        try
        {
            _coords = coords.ReadbackAndClone();
            _labels = labels.ReadbackAndClone();
            _confidences = confidences?.ReadbackAndClone();
            if (_objectRenderer) _objectRenderer.RenderDetections(_coords, _labels, _confidences, capturePose);
            CompletedInferenceCount++;
        }
        finally
        {
            ReleaseReadbacks();
        }
    }
}
