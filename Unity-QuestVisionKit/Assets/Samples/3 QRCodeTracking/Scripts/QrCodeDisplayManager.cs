using System.Collections.Generic;
using PassthroughCameraSamples;
using UnityEngine;
using Meta.XR;
using System;

public class QrCodeDisplayManager : MonoBehaviour
{
#if ZXING_ENABLED
    [SerializeField] private QrCodeScanner scanner;
    [SerializeField] private EnvironmentRaycastManager envRaycastManager;

    private readonly Dictionary<string, MarkerController> _activeMarkers = new();
    private WebCamTextureManager _webCamTextureManager;
    private PassthroughCameraEye _passthroughCameraEye;

    private enum QrRaycastMode
    {
        CenterOnly,
        PerCorner
    }
    
    [SerializeField] private QrRaycastMode raycastMode = QrRaycastMode.PerCorner;

    private void Awake()
    {
        _webCamTextureManager = FindAnyObjectByType<WebCamTextureManager>();
        _passthroughCameraEye = _webCamTextureManager.Eye;
    }

    private void Update()
    {
        UpdateMarkers();
    }
    
    private async void UpdateMarkers()
    {
        var qrResults = await scanner.ScanFrameAsync() ?? Array.Empty<QrCodeResult>();

        foreach (var qrResult in qrResults)
        {
            if (qrResult?.corners == null || qrResult.corners.Length < 4)
            {
                continue;
            }

            var count = qrResult.corners.Length;
            var uvs = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                uvs[i] = new Vector2(qrResult.corners[i].x, qrResult.corners[i].y);
            }

            var centerUV = Vector2.zero;
            foreach (var uv in uvs) centerUV += uv;
            centerUV /= count;

            var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(_passthroughCameraEye);
            var centerPixel = new Vector2Int(
                Mathf.RoundToInt(centerUV.x * intrinsics.Resolution.x),
                Mathf.RoundToInt(centerUV.y * intrinsics.Resolution.y)
            );

            var centerRay = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, centerPixel);
            if (!envRaycastManager || !envRaycastManager.Raycast(centerRay, out var centerHitInfo))
            {
                continue;
            }

            var center = centerHitInfo.point;
            var distance = Vector3.Distance(centerRay.origin, center);
            var worldCorners = new Vector3[count];

            for (var i = 0; i < count; i++)
            {
                var pixelCoord = new Vector2Int(
                    Mathf.RoundToInt(uvs[i].x * intrinsics.Resolution.x),
                    Mathf.RoundToInt(uvs[i].y * intrinsics.Resolution.y)
                );
                var r = PassthroughCameraUtils.ScreenPointToRayInWorld(_passthroughCameraEye, pixelCoord);

                if (raycastMode == QrRaycastMode.PerCorner)
                {
                    if (envRaycastManager.Raycast(r, out var cornerHit))
                    {
                        worldCorners[i] = cornerHit.point;
                    }
                    else
                    {
                        worldCorners[i] = r.origin + r.direction * distance;
                    }
                }
                else // CenterOnly
                {
                    worldCorners[i] = r.origin + r.direction * distance;
                }
            }

            // Pose estimation
            center = Vector3.zero;
            foreach (var c in worldCorners)
            {
                center += c;
            }
            center /= count;

            var up = (worldCorners[1] - worldCorners[0]).normalized;
            var right = (worldCorners[2] - worldCorners[1]).normalized;
            var normal = -Vector3.Cross(up, right).normalized;
            var poseRot = Quaternion.LookRotation(normal, up);

            var width = Vector3.Distance(worldCorners[0], worldCorners[1]);
            var height = Vector3.Distance(worldCorners[0], worldCorners[3]);
            var scaleFactor = 1.5f;
            var scale = new Vector3(width * scaleFactor, height * scaleFactor, 1f);

            if (_activeMarkers.TryGetValue(qrResult.text, out var marker))
            {
                marker.UpdateMarker(center, poseRot, scale, qrResult.text);
            }
            else
            {
                var markerGo = MarkerPool.Instance.GetMarker();
                if (!markerGo)
                {
                    continue;
                }

                marker = markerGo.GetComponent<MarkerController>();
                if (!marker)
                {
                    continue;
                }

                marker.UpdateMarker(center, poseRot, scale, qrResult.text);
                _activeMarkers[qrResult.text] = marker;
            }
        }

        // Cleanup
        var keysToRemove = new List<string>();
        foreach (var kvp in _activeMarkers)
        {
            if (!kvp.Value.gameObject.activeSelf)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _activeMarkers.Remove(key);
        }
    }
#endif
}
