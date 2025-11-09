using System;
using System.Collections.Generic;
using Meta.XR;
using UnityEngine;

public class QrCodeDisplayManager : MonoBehaviour
{
#if ZXING_ENABLED
    [SerializeField] private QrCodeScanner scanner;
    [SerializeField] private EnvironmentRaycastManager envRaycastManager;

    private readonly Dictionary<string, MarkerController> _activeMarkers = new();

    private enum QrRaycastMode
    {
        CenterOnly,
        PerCorner
    }
    
    [SerializeField] private QrRaycastMode raycastMode = QrRaycastMode.PerCorner;

    private void Update()
    {
        UpdateMarkers();
    }
    
    private async void UpdateMarkers()
    {
        if (!envRaycastManager)
        {
            return;
        }

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

            var centerRay = BuildWorldRay(qrResult, centerUV);
            if (!envRaycastManager.Raycast(centerRay, out var centerHitInfo))
            {
                continue;
            }

            var center = centerHitInfo.point;
            var distance = Vector3.Distance(centerRay.origin, center);
            var qrPlane = new Plane(centerHitInfo.normal, centerHitInfo.point);
            var worldCorners = new Vector3[count];

            for (var i = 0; i < count; i++)
            {
                var r = BuildWorldRay(qrResult, uvs[i]);

                if (raycastMode == QrRaycastMode.PerCorner)
                {
                    if (envRaycastManager.Raycast(r, out var cornerHit))
                    {
                        worldCorners[i] = cornerHit.point;
                    }
                    else
                    {
                        worldCorners[i] = ProjectOntoPlane(qrPlane, r, distance);
                    }
                }
                else // CenterOnly
                {
                    worldCorners[i] = ProjectOntoPlane(qrPlane, r, distance);
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

    private static Vector2 ToViewport(Vector2 uv) => new(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));

    private static Ray BuildWorldRay(QrCodeResult result, Vector2 uv)
    {
        var viewport = ToViewport(uv);
        var intrinsics = result.intrinsics;
        var sensorResolution = (Vector2)intrinsics.SensorResolution;
        var currentResolution = (Vector2)result.captureResolution;
        if (currentResolution == Vector2.zero)
        {
            currentResolution = sensorResolution;
        }

        var crop = ComputeSensorCrop(sensorResolution, currentResolution);
        var sensorPoint = new Vector2(
            crop.x + crop.width * viewport.x,
            crop.y + crop.height * viewport.y);

        var localDirection = new Vector3(
            (sensorPoint.x - intrinsics.PrincipalPoint.x) / intrinsics.FocalLength.x,
            (sensorPoint.y - intrinsics.PrincipalPoint.y) / intrinsics.FocalLength.y,
            1f).normalized;

        var worldDirection = result.cameraPose.rotation * localDirection;
        return new Ray(result.cameraPose.position, worldDirection);
    }

    private static Rect ComputeSensorCrop(Vector2 sensorResolution, Vector2 currentResolution)
    {
        if (sensorResolution == Vector2.zero)
        {
            return new Rect(0, 0, currentResolution.x, currentResolution.y);
        }

        var scaleFactor = new Vector2(
            currentResolution.x / sensorResolution.x,
            currentResolution.y / sensorResolution.y);
        var maxScale = Mathf.Max(scaleFactor.x, scaleFactor.y);
        if (maxScale <= 0)
        {
            maxScale = 1f;
        }
        scaleFactor /= maxScale;

        return new Rect(
            sensorResolution.x * (1f - scaleFactor.x) * 0.5f,
            sensorResolution.y * (1f - scaleFactor.y) * 0.5f,
            sensorResolution.x * scaleFactor.x,
            sensorResolution.y * scaleFactor.y);
    }

    private static Vector3 ProjectOntoPlane(Plane plane, Ray ray, float fallbackDistance)
    {
        return plane.Raycast(ray, out var planeDistance)
            ? ray.GetPoint(planeDistance)
            : ray.GetPoint(fallbackDistance);
    }
}
