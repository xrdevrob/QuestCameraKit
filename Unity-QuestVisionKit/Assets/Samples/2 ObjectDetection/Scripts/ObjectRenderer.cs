using Meta.XR;
using UnityEngine;

using PassthroughCameraSamples;
using System.Collections.Generic;

public class ObjectRenderer : MonoBehaviour
{
    [Header("Camera & Raycast Settings")]
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private EnvironmentRaycastManager envRaycastManager;
    [SerializeField] private float mergeThreshold = 0.2f;
    
    [Header("Marker Settings")]
    [SerializeField] private GameObject markerPrefab;
    
    [Header("Label Filtering")]
    [SerializeField] private YOLOv9Labels[] labelFilters;

    private Camera _mainCamera;
    private const float YoloInputSize = 640f;
    private readonly Dictionary<string, MarkerController> _activeMarkers = new();

    private void Awake()
    {
        _mainCamera = Camera.main;
    }
    
    public void RenderDetections(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<int> labelIDs)
    {
        var numDetections = coords.shape[0];
        print($"[Detection3DRenderer] RenderDetections: {numDetections} detections received.");
        ClearPreviousMarkers();

        var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(webCamTextureManager.Eye);
        var camRes = intrinsics.Resolution;
        
        var imageWidth = YoloInputSize;
        var imageHeight = YoloInputSize;
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

            var centerPixel = new Vector2(perX * camRes.x, (1.0f - perY) * camRes.y);
            print($"[Detection3DRenderer] Detection {i} Center Pixel: {centerPixel}");

            var centerRay = PassthroughCameraUtils.ScreenPointToRayInWorld(webCamTextureManager.Eye, new Vector2Int(Mathf.RoundToInt(centerPixel.x), Mathf.RoundToInt(centerPixel.y)));

            if (!envRaycastManager.Raycast(centerRay, out var centerHit))
            {
                Debug.LogWarning($"[Detection3DRenderer] Detection {i}: Environment raycast failed.");
                continue;
            }

            var markerWorldPos = centerHit.point;

            var u1 = (detectedCenterX - detectedWidth * 0.5f) / imageWidth;
            var v1 = (detectedCenterY - detectedHeight * 0.5f) / imageHeight;
            var u2 = (detectedCenterX + detectedWidth * 0.5f) / imageWidth;
            var v2 = (detectedCenterY + detectedHeight * 0.5f) / imageHeight;

            var tlPixel = new Vector2Int(
                Mathf.RoundToInt(u1 * camRes.x),
                Mathf.RoundToInt((1.0f - v1) * camRes.y)
            );
            
            var brPixel = new Vector2Int(
                Mathf.RoundToInt(u2 * camRes.x),
                Mathf.RoundToInt((1.0f - v2) * camRes.y)
            );

            var tlRay = PassthroughCameraUtils.ScreenPointToRayInWorld(webCamTextureManager.Eye, tlPixel);
            var brRay = PassthroughCameraUtils.ScreenPointToRayInWorld(webCamTextureManager.Eye, brPixel);

            var depth = Vector3.Distance(_mainCamera.transform.position, markerWorldPos);
            var worldTL = tlRay.GetPoint(depth);
            var worldBR = brRay.GetPoint(depth);

            var markerWidth = Mathf.Abs(worldBR.x - worldTL.x);
            var markerHeight = Mathf.Abs(worldBR.y - worldTL.y);
            var markerScale = new Vector3(markerWidth, markerHeight, 1f);

            var detectedLabel = (YOLOv9Labels)labelIDs[i];
            if (labelFilters != null && labelFilters.Length > 0 && !System.Array.Exists(labelFilters, label => label == detectedLabel))
            {
                print($"[Detection3DRenderer] Detection {i}: Skipped label: {detectedLabel}");
                continue;
            }

            var labelKey = detectedLabel.ToString();
            if (_activeMarkers.TryGetValue(labelKey, out MarkerController existingMarker))
            {
                if (Vector3.Distance(existingMarker.transform.position, markerWorldPos) < mergeThreshold)
                {
                    existingMarker.UpdateMarker(markerWorldPos, Quaternion.LookRotation(-centerHit.normal, Vector3.up), markerScale, labelKey);
                    continue;
                }
                labelKey += $"_{i}";
            }

            var markerGo = Instantiate(markerPrefab);
            var marker = markerGo.GetComponent<MarkerController>();
            if (!marker)
            {
                Debug.LogWarning($"[Detection3DRenderer] Detection {i}: Marker prefab is missing a MarkerController component.");
                continue;
            }

            marker.UpdateMarker(markerWorldPos, Quaternion.LookRotation(-centerHit.normal, Vector3.up), markerScale, labelKey);
            _activeMarkers[labelKey] = marker;
            print($"[Detection3DRenderer] Detection {i}: Marker placed with label: {labelKey}");
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
}
