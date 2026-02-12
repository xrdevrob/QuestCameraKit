using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Meta.XR;
using Unity.InferenceEngine;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using xrdevrob.QuestCameraKit.AI;

public class ObjectDetectorUnityIE : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PassthroughCameraAccess passthroughCamera;
    [SerializeField] private DepthTextureAccess depthTextureAccess;
    [SerializeField] private GameObject boundingBoxPrefab;

    [Header("Model")]
    [SerializeField] private ModelAsset sentisModel;
    [SerializeField] private TextAsset classLabels;

    [Header("Inference")]
    [SerializeField] private BackendType backend = BackendType.GPUCompute;
    [SerializeField] private float inferenceInterval = 0.1f;
    [SerializeField, Range(0f, 1f)] private float scoreThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float nmsThreshold = 0.5f;
    [SerializeField] private int layersPerFrame = 20;
    [SerializeField] private bool warmupOnStart = true;
    [SerializeField, Min(1)] private int warmupRuns = 1;

    [Header("Visualization")]
    [SerializeField] private bool showBoundingBoxes = true;
    [SerializeField, Range(0f, 1f)] private float labelScale = 0.5f;

    private struct Detection
    {
        public Vector4 Box;
        public float Score;
        public int ClassId;
    }

    private struct DepthFrame
    {
        public Pose Pose;
        public NativeArray<float> Depth;
        public Matrix4x4[] ViewProjection;
        public bool IsValid;
    }

    private readonly List<Detection> _detections = new();
    private readonly List<GameObject> _liveBoxes = new();
    private readonly Queue<GameObject> _boxPool = new();

    private Worker _worker;
    private Model _model;
    private Tensor<float> _input;
    private CancellationTokenSource _cancellation;
    private string[] _labels;
    private bool _busy;
    private float _nextInferenceTime;
    private int _eyeIndex;
    private DepthFrame _depthFrame;

    private void Awake()
    {
        passthroughCamera ??= GetComponent<PassthroughCameraAccess>();
        depthTextureAccess ??= GetComponent<DepthTextureAccess>();
        if (!depthTextureAccess)
        {
            depthTextureAccess = gameObject.AddComponent<DepthTextureAccess>();
        }

        sentisModel ??= Resources.Load<ModelAsset>("yolov9sentis");
        classLabels ??= Resources.Load<TextAsset>("SentisYoloClasses");
        boundingBoxPrefab ??= Resources.Load<GameObject>("BoundingBoxPrefab");

        if (passthroughCamera)
        {
            _eyeIndex = passthroughCamera.CameraPosition == PassthroughCameraAccess.CameraPositionType.Left ? 0 : 1;
        }

        _labels = ParseLabels(classLabels);
    }

    private async void Start()
    {
        if (!passthroughCamera)
        {
            Debug.LogError("[ObjectDetectorUnityIE] Missing PassthroughCameraAccess reference.");
            enabled = false;
            return;
        }

        if (!depthTextureAccess)
        {
            Debug.LogError("[ObjectDetectorUnityIE] Missing DepthTextureAccess component.");
            enabled = false;
            return;
        }

        if (!boundingBoxPrefab)
        {
            Debug.LogError("[ObjectDetectorUnityIE] Missing BoundingBoxPrefab reference.");
            enabled = false;
            return;
        }

        if (!sentisModel)
        {
            Debug.LogError("[ObjectDetectorUnityIE] Missing ModelAsset.");
            enabled = false;
            return;
        }

        _cancellation = new CancellationTokenSource();
        _model = ModelLoader.Load(sentisModel);
        _worker = new Worker(_model, backend);

        var inputShape = _model.inputs[0].shape;
        if (!inputShape.IsStatic())
        {
            Debug.LogError("[ObjectDetectorUnityIE] Model input shape is dynamic; static shape is required.");
            enabled = false;
            return;
        }

        _input = new Tensor<float>(inputShape.ToTensorShape());
        if (warmupOnStart)
        {
            await WarmupAsync(_cancellation.Token);
            _nextInferenceTime = Time.time + Mathf.Max(0.01f, inferenceInterval);
        }
    }

    private void OnEnable()
    {
        if (depthTextureAccess)
        {
            depthTextureAccess.OnDepthTextureUpdateCPU += HandleDepthFrame;
        }
    }

    private void OnDisable()
    {
        if (depthTextureAccess)
        {
            depthTextureAccess.OnDepthTextureUpdateCPU -= HandleDepthFrame;
        }
    }

    private void Update()
    {
        if (!isActiveAndEnabled || _busy || _worker == null || !passthroughCamera || !passthroughCamera.IsPlaying)
        {
            return;
        }

        if (Time.time < _nextInferenceTime)
        {
            return;
        }

        var source = passthroughCamera.GetTexture();
        if (!source)
        {
            return;
        }

        _nextInferenceTime = Time.time + Mathf.Max(0.01f, inferenceInterval);
        _ = RunInferenceAsync(source, source.width, source.height, _cancellation.Token);
    }

    private void HandleDepthFrame(DepthTextureAccess.DepthFrameData data)
    {
        _depthFrame.Pose = data.CameraPose;
        _depthFrame.Depth = data.DepthTexturePixels;
        _depthFrame.ViewProjection = data.ViewProjectionMatrix;
        _depthFrame.IsValid = data.DepthTexturePixels.IsCreated && data.DepthTexturePixels.Length > 0 &&
                              data.ViewProjectionMatrix != null && data.ViewProjectionMatrix.Length > _eyeIndex;
    }

    private async Task RunInferenceAsync(Texture source, int sourceWidth, int sourceHeight, CancellationToken ct)
    {
        _busy = true;
        try
        {
            depthTextureAccess.RequestDepthSample();
            TextureConverter.ToTensor(source, _input);
            await ScheduleModelExecutionAsync(ct);

            var inputWidth = _input.shape[3];
            var inputHeight = _input.shape[2];
            var scaleX = sourceWidth / (float)inputWidth;
            var scaleY = sourceHeight / (float)inputHeight;

            _detections.Clear();
            if (_model.outputs.Count == 1)
            {
                await ProcessRawYoloOutput(scaleX, scaleY);
            }
            else if (_model.outputs.Count >= 2)
            {
                await ProcessTwoOutputModel(scaleX, scaleY, inputWidth, inputHeight);
            }

            RenderDetections(sourceWidth, sourceHeight);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ObjectDetectorUnityIE] Inference failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ScheduleModelExecutionAsync(CancellationToken ct)
    {
        if (layersPerFrame > 0)
        {
            var schedule = _worker.ScheduleIterable(_input);
            if (schedule == null)
            {
                _worker.Schedule(_input);
                return;
            }

            var layer = 0;
            while (schedule.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                if (++layer % layersPerFrame == 0)
                {
                    await Task.Yield();
                }
            }
            return;
        }

        _worker.Schedule(_input);
    }

    private async Task WarmupAsync(CancellationToken ct)
    {
        if (_worker == null || _input == null)
        {
            return;
        }

        _busy = true;
        try
        {
            var runs = Mathf.Max(1, warmupRuns);
            for (var i = 0; i < runs; i++)
            {
                ct.ThrowIfCancellationRequested();
                var source = passthroughCamera ? passthroughCamera.GetTexture() : null;
                TextureConverter.ToTensor(source ? source : Texture2D.blackTexture, _input);
                await ScheduleModelExecutionAsync(ct);

                var output = _worker.PeekOutput(0) as Tensor<float>;
                if (output != null)
                {
                    using var outputClone = await output.ReadbackAndCloneAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ObjectDetectorUnityIE] Warmup failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ProcessRawYoloOutput(float scaleX, float scaleY)
    {
        var output = _worker.PeekOutput(0) as Tensor<float>;
        if (output == null)
        {
            return;
        }

        using var outputClone = await output.ReadbackAndCloneAsync();
        var shape = outputClone.shape;
        int numChannels;
        int numBoxes;

        if (shape.rank == 3)
        {
            numChannels = shape[1];
            numBoxes = shape[2];
        }
        else if (shape.rank == 2)
        {
            numChannels = shape[0];
            numBoxes = shape[1];
        }
        else
        {
            return;
        }

        var numClasses = numChannels - 4;
        var data = outputClone.DownloadToArray();

        for (var b = 0; b < numBoxes; b++)
        {
            var bestScore = 0f;
            var bestClass = 0;

            for (var c = 0; c < numClasses; c++)
            {
                var score = data[(4 + c) * numBoxes + b];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestScore < scoreThreshold)
            {
                continue;
            }

            var cx = data[b];
            var cy = data[(1 * numBoxes) + b];
            var w = data[(2 * numBoxes) + b];
            var h = data[(3 * numBoxes) + b];

            _detections.Add(new Detection
            {
                Box = new Vector4(
                    (cx - w * 0.5f) * scaleX,
                    (cy - h * 0.5f) * scaleY,
                    (cx + w * 0.5f) * scaleX,
                    (cy + h * 0.5f) * scaleY),
                Score = bestScore,
                ClassId = bestClass
            });
        }

        if (_detections.Count > 1)
        {
            ApplyNms(_detections, nmsThreshold);
        }
    }

    private async Task ProcessTwoOutputModel(float scaleX, float scaleY, int inputWidth, int inputHeight)
    {
        var output0 = _worker.PeekOutput(0) as Tensor<float>;
        var output1 = _worker.PeekOutput(1) as Tensor<float>;
        if (output0 == null || output1 == null)
        {
            return;
        }

        using var output0Clone = await output0.ReadbackAndCloneAsync();
        using var output1Clone = await output1.ReadbackAndCloneAsync();

        var shape0 = output0Clone.shape;
        var shape1 = output1Clone.shape;

        var output0Name = _model.outputs[0].name.ToLowerInvariant();
        var output0IsBoxes = output0Name.Contains("box") || (shape0.rank >= 2 && shape0[shape0.rank - 1] == 4);

        float[] scores;
        float[] boxes;
        TensorShape scoresShape;
        TensorShape boxesShape;

        if (output0IsBoxes)
        {
            boxes = output0Clone.DownloadToArray();
            scores = output1Clone.DownloadToArray();
            boxesShape = shape0;
            scoresShape = shape1;
        }
        else
        {
            scores = output0Clone.DownloadToArray();
            boxes = output1Clone.DownloadToArray();
            scoresShape = shape0;
            boxesShape = shape1;
        }

        var isDetrStyle = boxesShape.rank == 3 && scoresShape.rank == 3;
        var numBoxes = isDetrStyle ? boxesShape[1] : boxesShape[0];
        var numClasses = isDetrStyle ? scoresShape[2] : scoresShape[1];

        for (var i = 0; i < numBoxes; i++)
        {
            var scoreOffset = i * numClasses;
            float bestScore = 0f;
            var bestClass = 0;

            if (isDetrStyle)
            {
                var maxLogit = float.MinValue;
                for (var c = 0; c < numClasses; c++)
                {
                    if (scores[scoreOffset + c] > maxLogit)
                    {
                        maxLogit = scores[scoreOffset + c];
                    }
                }

                var sumExp = 0f;
                for (var c = 0; c < numClasses; c++)
                {
                    sumExp += Mathf.Exp(scores[scoreOffset + c] - maxLogit);
                }

                for (var c = 0; c < numClasses; c++)
                {
                    var probability = Mathf.Exp(scores[scoreOffset + c] - maxLogit) / sumExp;
                    if (probability > bestScore)
                    {
                        bestScore = probability;
                        bestClass = c;
                    }
                }
            }
            else
            {
                for (var c = 0; c < numClasses; c++)
                {
                    if (scores[scoreOffset + c] > bestScore)
                    {
                        bestScore = scores[scoreOffset + c];
                        bestClass = c;
                    }
                }
            }

            if (bestScore < scoreThreshold)
            {
                continue;
            }

            var boxOffset = i * 4;
            float xmin;
            float ymin;
            float xmax;
            float ymax;

            if (isDetrStyle)
            {
                var centerX = boxes[boxOffset] * inputWidth * scaleX;
                var centerY = boxes[boxOffset + 1] * inputHeight * scaleY;
                var width = boxes[boxOffset + 2] * inputWidth * scaleX;
                var height = boxes[boxOffset + 3] * inputHeight * scaleY;

                xmin = centerX - width * 0.5f;
                ymin = centerY - height * 0.5f;
                xmax = centerX + width * 0.5f;
                ymax = centerY + height * 0.5f;
            }
            else
            {
                xmin = boxes[boxOffset] * scaleX;
                ymin = boxes[boxOffset + 1] * scaleY;
                xmax = boxes[boxOffset + 2] * scaleX;
                ymax = boxes[boxOffset + 3] * scaleY;
            }

            _detections.Add(new Detection
            {
                Box = new Vector4(xmin, ymin, xmax, ymax),
                Score = bestScore,
                ClassId = bestClass
            });
        }

        if (!isDetrStyle && _detections.Count > 1)
        {
            ApplyNms(_detections, nmsThreshold);
        }
    }

    private void RenderDetections(int sourceWidth, int sourceHeight)
    {
        foreach (var box in _liveBoxes)
        {
            box.SetActive(false);
            _boxPool.Enqueue(box);
        }
        _liveBoxes.Clear();

        if (!showBoundingBoxes || !_depthFrame.IsValid)
        {
            return;
        }

        foreach (var detection in _detections)
        {
            if (!TryProject(detection.Box.x, detection.Box.y, detection.Box.z, detection.Box.w, sourceWidth, sourceHeight,
                    out var world, out var rotation, out var scale))
            {
                continue;
            }

            var box = _boxPool.Count > 0 ? _boxPool.Dequeue() : Instantiate(boundingBoxPrefab);
            box.SetActive(true);
            box.transform.SetPositionAndRotation(world, rotation);
            box.transform.localScale = scale;

            var label = box.GetComponentInChildren<Text>();
            if (label)
            {
                var className = (_labels != null && detection.ClassId >= 0 && detection.ClassId < _labels.Length)
                    ? _labels[detection.ClassId]
                    : $"cls_{detection.ClassId}";

                label.text = $"{className} {detection.Score:0.00}";
                label.enabled = showBoundingBoxes;

                var averageScale = (scale.x + scale.y + scale.z) / 3f;
                var uniform = averageScale * labelScale;
                label.transform.localScale = new Vector3(
                    uniform / Mathf.Max(scale.x, 0.001f),
                    uniform / Mathf.Max(scale.y, 0.001f),
                    uniform / Mathf.Max(scale.z, 0.001f));
            }

            _liveBoxes.Add(box);
        }
    }

    private bool TryProject(float xmin, float ymin, float xmax, float ymax, int sourceWidth, int sourceHeight,
        out Vector3 world, out Quaternion rotation, out Vector3 scale)
    {
        world = default;
        rotation = default;
        scale = default;

        if (!_depthFrame.IsValid || !passthroughCamera || !depthTextureAccess || !depthTextureAccess.IsInitialized)
        {
            return false;
        }

        var centerX = (xmin + xmax) * 0.5f;
        var centerY = (ymin + ymax) * 0.5f;

        var normalizedCenterX = centerX / sourceWidth;
        var normalizedCenterY = centerY / sourceHeight;
        var ray = passthroughCamera.ViewportPointToRay(new Vector2(normalizedCenterX, 1f - normalizedCenterY), _depthFrame.Pose);

        var world1M = ray.origin + ray.direction;
        var clip = _depthFrame.ViewProjection[_eyeIndex] * new Vector4(world1M.x, world1M.y, world1M.z, 1f);
        if (clip.w <= 0f)
        {
            return false;
        }

        var uv = (new Vector2(clip.x, clip.y) / clip.w) * 0.5f + Vector2.one * 0.5f;
        var texSize = depthTextureAccess.TextureSize;
        var sx = Mathf.Clamp((int)(uv.x * texSize), 0, texSize - 1);
        var sy = Mathf.Clamp((int)(uv.y * texSize), 0, texSize - 1);
        var depthIndex = _eyeIndex * texSize * texSize + sy * texSize + sx;

        if (!_depthFrame.Depth.IsCreated || depthIndex < 0 || depthIndex >= _depthFrame.Depth.Length)
        {
            return false;
        }

        var depth = _depthFrame.Depth[depthIndex];
        if (depth <= 0f || depth > 20f || float.IsInfinity(depth))
        {
            return false;
        }

        world = ray.origin + ray.direction * depth;
        rotation = Quaternion.LookRotation(world - _depthFrame.Pose.position);

        var normalizedWidth = (xmax - xmin) / sourceWidth;
        var normalizedHeight = (ymax - ymin) / sourceHeight;

        var leftRay = passthroughCamera.ViewportPointToRay(
            new Vector2(normalizedCenterX - normalizedWidth * 0.5f, 1f - normalizedCenterY), _depthFrame.Pose);
        var rightRay = passthroughCamera.ViewportPointToRay(
            new Vector2(normalizedCenterX + normalizedWidth * 0.5f, 1f - normalizedCenterY), _depthFrame.Pose);
        var topRay = passthroughCamera.ViewportPointToRay(
            new Vector2(normalizedCenterX, 1f - (normalizedCenterY - normalizedHeight * 0.5f)), _depthFrame.Pose);
        var bottomRay = passthroughCamera.ViewportPointToRay(
            new Vector2(normalizedCenterX, 1f - (normalizedCenterY + normalizedHeight * 0.5f)), _depthFrame.Pose);

        var worldLeft = leftRay.origin + leftRay.direction * depth;
        var worldRight = rightRay.origin + rightRay.direction * depth;
        var worldTop = topRay.origin + topRay.direction * depth;
        var worldBottom = bottomRay.origin + bottomRay.direction * depth;

        scale = new Vector3(Vector3.Distance(worldLeft, worldRight), Vector3.Distance(worldTop, worldBottom), 1f);
        return true;
    }

    private static void ApplyNms(List<Detection> detections, float iouThreshold)
    {
        detections.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = new List<Detection>();
        var suppressed = new bool[detections.Count];

        for (var i = 0; i < detections.Count; i++)
        {
            if (suppressed[i])
            {
                continue;
            }

            kept.Add(detections[i]);
            var boxA = detections[i].Box;

            for (var j = i + 1; j < detections.Count; j++)
            {
                if (suppressed[j])
                {
                    continue;
                }

                if (IoU(boxA, detections[j].Box) > iouThreshold)
                {
                    suppressed[j] = true;
                }
            }
        }

        detections.Clear();
        detections.AddRange(kept);
    }

    private static float IoU(Vector4 a, Vector4 b)
    {
        var interW = Mathf.Max(0f, Mathf.Min(a.z, b.z) - Mathf.Max(a.x, b.x));
        var interH = Mathf.Max(0f, Mathf.Min(a.w, b.w) - Mathf.Max(a.y, b.y));
        var interArea = interW * interH;
        var areaA = Mathf.Max(0f, (a.z - a.x) * (a.w - a.y));
        var areaB = Mathf.Max(0f, (b.z - b.x) * (b.w - b.y));
        return interArea / (areaA + areaB - interArea + 1e-6f);
    }

    private static string[] ParseLabels(TextAsset labelsAsset)
    {
        if (!labelsAsset)
        {
            return null;
        }

        return labelsAsset.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private void OnDestroy()
    {
        _cancellation?.Cancel();
        _worker?.Dispose();
        _input?.Dispose();

        while (_boxPool.Count > 0)
        {
            var box = _boxPool.Dequeue();
            if (box)
            {
                Destroy(box);
            }
        }

        foreach (var box in _liveBoxes)
        {
            if (box)
            {
                Destroy(box);
            }
        }
        _liveBoxes.Clear();
    }
}
