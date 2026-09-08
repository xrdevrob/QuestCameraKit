using System;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR;

public class ObjectRenderer : MonoBehaviour
{
    [Header("Camera & Raycast Settings")]
    [SerializeField] private float mergeThreshold = 0.2f;
    
    [Header("Marker Settings")]
    [SerializeField] private GameObject markerPrefab;
    
    [Header("Label Filtering")]
    [SerializeField] private YOLOv9Labels[] labelFilters;
    [SerializeField, Range(0f, 1f)] private float minConfidence = 0.15f;

    private Camera _mainCamera;
    private const float ModelInputSize = 640f;
    private PassthroughCameraAccess _cameraAccess;
    private EnvironmentRaycastManager _envRaycastManager;
    private readonly Dictionary<string, MarkerController> _activeMarkers = new();
    private readonly List<MarkerController> _markerPool = new();

    private void Awake()
    {
        _cameraAccess = GetComponent<PassthroughCameraAccess>() ?? FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
        _envRaycastManager = GetComponent<EnvironmentRaycastManager>() ?? FindAnyObjectByType<EnvironmentRaycastManager>(FindObjectsInactive.Include);
        if (!_cameraAccess || !_envRaycastManager)
        {
            Debug.LogWarning("[Detection3DRenderer] Passthrough camera or Environment Raycast Manager is not ready.");
            return;
        }
        _mainCamera = Camera.main;
    }
    
    public void RenderDetections(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<int> labelIDs, Unity.InferenceEngine.Tensor<float> confidences = null, Pose? capturePose = null)
    {
        if (coords == null || labelIDs == null || coords.shape.rank != 2 || coords.shape[1] < 4)
        {
            return;
        }

        if (!_cameraAccess || !_envRaycastManager || !markerPrefab || !_mainCamera)
        {
            Debug.LogWarning("[Detection3DRenderer] Missing dependencies.");
            return;
        }

        var numDetections = Mathf.Min(coords.shape[0], labelIDs.shape.length);
        ClearPreviousMarkers();

        var imageWidth = ModelInputSize;
        var imageHeight = ModelInputSize;

        for (var i = 0; i < numDetections; i++)
        {
            var detectedCenterX = coords[i, 0];
            var detectedCenterY = coords[i, 1];
            var detectedWidth = coords[i, 2];
            var detectedHeight = coords[i, 3];

            var detectedLabel = (YOLOv9Labels)labelIDs[i];
            if (labelFilters is { Length: > 0 } && !Array.Exists(labelFilters, label => label == detectedLabel)) continue;
            var confidence = GetConfidence(coords, confidences, i);
            if (confidence >= 0f && confidence < minConfidence) continue;
            if (!float.IsFinite(detectedCenterX) || !float.IsFinite(detectedCenterY) ||
                !float.IsFinite(detectedWidth) || !float.IsFinite(detectedHeight) ||
                detectedWidth <= 0f || detectedHeight <= 0f) continue;

            var perX = detectedCenterX / imageWidth;
            var perY = detectedCenterY / imageHeight;
            var centerRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(perX, perY), capturePose);

            if (!_envRaycastManager.Raycast(centerRay, out var centerHit))
            {
                continue;
            }

            var markerWorldPos = centerHit.point;

            var u1 = (detectedCenterX - detectedWidth * 0.5f) / imageWidth;
            var v1 = (detectedCenterY - detectedHeight * 0.5f) / imageHeight;
            var u2 = (detectedCenterX + detectedWidth * 0.5f) / imageWidth;
            var v2 = (detectedCenterY + detectedHeight * 0.5f) / imageHeight;

            var tlRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v1), capturePose);
            var trRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u2, v1), capturePose);
            var blRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v2), capturePose);

            var depth = Vector3.Distance(centerRay.origin, markerWorldPos);
            var plane = new Plane(centerHit.normal, markerWorldPos);
            var worldTL = tlRay.GetPoint(plane.Raycast(tlRay, out var tlDistance) ? tlDistance : depth);
            var worldTR = trRay.GetPoint(plane.Raycast(trRay, out var trDistance) ? trDistance : depth);
            var worldBL = blRay.GetPoint(plane.Raycast(blRay, out var blDistance) ? blDistance : depth);

            var markerWidth = Vector3.Distance(worldTR, worldTL);
            var markerHeight = Vector3.Distance(worldBL, worldTL);
            var markerScale = new Vector3(markerWidth, markerHeight, 1f);

            var surfaceNormal = SampleSurfaceNormal(markerWorldPos, centerHit.normal);
            var markerRotation = Quaternion.LookRotation(-surfaceNormal, Vector3.up);

            var dictionaryKey = detectedLabel.ToString();
            var labelWithConfidence = confidence >= 0f
                ? $"{dictionaryKey} ({confidence * 100f:F0}%)"
                : dictionaryKey;

            var lookupKey = dictionaryKey;
            if (_activeMarkers.TryGetValue(lookupKey, out MarkerController existingMarker))
            {
                if (Vector3.Distance(existingMarker.transform.position, markerWorldPos) < mergeThreshold)
                {
                    existingMarker.UpdateMarker(markerWorldPos, markerRotation, markerScale, labelWithConfidence);
                    continue;
                }
                lookupKey = $"{dictionaryKey}_{i}";
            }

            var marker = _markerPool.Find(item => item && !item.gameObject.activeSelf);
            if (!marker)
            {
                var markerGo = Instantiate(markerPrefab);
                marker = markerGo.GetComponent<MarkerController>();
                if (!marker)
                {
                    Destroy(markerGo);
                    Debug.LogError("[Detection3DRenderer] Marker prefab needs a MarkerController.");
                    return;
                }
                _markerPool.Add(marker);
            }

            marker.UpdateMarker(markerWorldPos, markerRotation, markerScale, labelWithConfidence);
            _activeMarkers[lookupKey] = marker;
        }
    }

    private void ClearPreviousMarkers()
    {
        foreach (var marker in _activeMarkers.Values)
        {
            if (marker && marker.gameObject)
            {
                marker.gameObject.SetActive(false);
            }
        }
        _activeMarkers.Clear();
    }

    private void OnDisable() => ClearPreviousMarkers();

    private void OnDestroy()
    {
        foreach (var marker in _markerPool)
            if (marker) Destroy(marker.gameObject);
    }

    private static Vector2 DetectionToViewport(float normalizedX, float normalizedY)
        => new(Mathf.Clamp01(normalizedX), Mathf.Clamp01(1f - normalizedY));

    private static float GetConfidence(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<float> confidenceTensor, int index)
    {
        var sampled = SampleConfidence(confidenceTensor, index);
        if (sampled >= 0f)
        {
            return Mathf.Clamp01(sampled);
        }

        if (coords == null || coords.shape.rank < 2)
        {
            return -1f;
        }

        var channels = coords.shape[coords.shape.rank - 1];
        if (channels <= 4)
        {
            return -1f;
        }

        try
        {
            return Mathf.Clamp01(coords[index, 4]);
        }
        catch (Exception)
        {
            return -1f;
        }
    }

    private static float SampleConfidence(Unity.InferenceEngine.Tensor<float> tensor, int index)
    {
        if (tensor == null)
        {
            return -1f;
        }

        var length = tensor.shape.length;
        if (index < 0 || index >= length)
        {
            return -1f;
        }

        try
        {
            return tensor[index];
        }
        catch
        {
            return -1f;
        }
    }

    private Vector3 SampleSurfaceNormal(Vector3 position, Vector3 fallbackNormal)
    {
        if (_envRaycastManager == null)
        {
            return fallbackNormal;
        }

        var origin = _mainCamera ? _mainCamera.transform.position : position - fallbackNormal * 0.1f;
        var direction = position - origin;
        if (direction.sqrMagnitude > 0.0001f)
        {
            if (_envRaycastManager.Raycast(new Ray(origin, direction.normalized), out var hit, direction.magnitude + 0.05f))
            {
                return hit.normal;
            }
        }

        var offsetOrigin = position + fallbackNormal.normalized * 0.05f;
        if (_envRaycastManager.Raycast(new Ray(offsetOrigin, -fallbackNormal.normalized), out var reverseHit, 0.2f))
        {
            return reverseHit.normal;
        }

        return fallbackNormal;
    }
}
