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
    
    public void RenderDetections(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<int> labelIDs, Unity.InferenceEngine.Tensor<float> confidences = null)
    {
        if (coords == null || labelIDs == null)
        {
            return;
        }

        if (!_cameraAccess || !_envRaycastManager)
        {
            Debug.LogWarning("[Detection3DRenderer] Missing dependencies.");
            return;
        }

        var numDetections = coords.shape[0];
        ClearPreviousMarkers();

        var imageWidth = ModelInputSize;
        var imageHeight = ModelInputSize;
        var halfWidth = imageWidth * 0.5f;
        var halfHeight = imageHeight * 0.5f;

        for (var i = 0; i < numDetections; i++)
        {
            var detectedCenterX = coords[i, 0];
            var detectedCenterY = coords[i, 1];
            var detectedWidth = coords[i, 2];
            var detectedHeight = coords[i, 3];

            var adjustedCenterX = detectedCenterX - halfWidth;
            var adjustedCenterY = detectedCenterY - halfHeight;

            var perX = (adjustedCenterX + halfWidth) / imageWidth;
            var perY = (adjustedCenterY + halfHeight) / imageHeight;
            var centerRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(perX, perY));

            if (!_envRaycastManager.Raycast(centerRay, out var centerHit))
            {
                Debug.LogWarning($"[Detection3DRenderer] Detection {i}: Environment raycast failed.");
                continue;
            }

            var markerWorldPos = centerHit.point;

            var u1 = (detectedCenterX - detectedWidth * 0.5f) / imageWidth;
            var v1 = (detectedCenterY - detectedHeight * 0.5f) / imageHeight;
            var u2 = (detectedCenterX + detectedWidth * 0.5f) / imageWidth;
            var v2 = (detectedCenterY + detectedHeight * 0.5f) / imageHeight;

            var tlRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v1));
            var trRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u2, v1));
            var blRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v2));
            var brRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u2, v2));

            var depth = Vector3.Distance(_mainCamera.transform.position, markerWorldPos);
            var worldTL = tlRay.GetPoint(depth);
            var worldTR = trRay.GetPoint(depth);
            var worldBL = blRay.GetPoint(depth);

            var markerWidth = Vector3.Distance(worldTR, worldTL);
            var markerHeight = Vector3.Distance(worldBL, worldTL);
            var markerScale = new Vector3(markerWidth, markerHeight, 1f);

            var detectedLabel = (YOLOv9Labels)labelIDs[i];
            if (labelFilters is { Length: > 0 } && !Array.Exists(labelFilters, label => label == detectedLabel))
            {
                continue;
            }

            var surfaceNormal = SampleSurfaceNormal(markerWorldPos, centerHit.normal);
            var markerRotation = Quaternion.LookRotation(-surfaceNormal, Vector3.up);

            var dictionaryKey = detectedLabel.ToString();
            var confidence = GetConfidence(coords, confidences, i);
            if (confidence >= 0f && confidence < minConfidence)
            {
                continue;
            }
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

            var markerGo = Instantiate(markerPrefab);
            var marker = markerGo.GetComponent<MarkerController>();
            if (!marker)
            {
                Debug.LogWarning($"[Detection3DRenderer] Detection {i}: Marker prefab is missing a MarkerController component.");
                continue;
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
                Destroy(marker.gameObject);
            }
        }
        _activeMarkers.Clear();
    }

    private Vector2 DetectionToViewport(float normalizedX, float normalizedY)
    {
        var resolution = (Vector2)_cameraAccess.CurrentResolution;
        if (resolution == Vector2.zero)
        {
            resolution = (Vector2)_cameraAccess.Intrinsics.SensorResolution;
        }
        if (resolution == Vector2.zero)
        {
            return new Vector2(Mathf.Clamp01(normalizedX), Mathf.Clamp01(1f - normalizedY));
        }

        var scaledX = Mathf.Clamp01(normalizedX) * ModelInputSize;
        var scaledY = Mathf.Clamp01(normalizedY) * ModelInputSize;

        var actualPixel = new Vector2(
            scaledX * (resolution.x / ModelInputSize),
            scaledY * (resolution.y / ModelInputSize));

        return new Vector2(
            Mathf.Clamp01(actualPixel.x / resolution.x),
            Mathf.Clamp01(1f - actualPixel.y / resolution.y));
    }

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
