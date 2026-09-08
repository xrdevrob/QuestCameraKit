/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Meta.XR.MRUtilityKit;
using Meta.XR.Samples;

using UnityEngine;


namespace Meta.XR.MRUtilityKitSamples.QRCodeDetection
{
    [MetaCodeSample("MRUKSample-QRCodeDetection")]
    public class Bounded2DVisualizer : MonoBehaviour
    {
        [SerializeField]
        LineRenderer _lineRenderer;
        [SerializeField]
        RectTransform _canvasRect;
        [SerializeField, Tooltip("(in Canvas-local units)")]
        Vector3 _canvasOffset = new(0f, -15f, 0f);

        MRUKTrackable _trackable;

        public void Initialize(MRUKTrackable trackable)
        {
            _trackable = trackable;
            Update();
        }

        void Update() => SetBounds(_trackable ? _trackable.PlaneRect : null);

        internal void SetBounds(Rect? bounds)
        {
            var visible = bounds.HasValue;
            if (_lineRenderer) _lineRenderer.enabled = visible;
            if (_canvasRect) _canvasRect.gameObject.SetActive(visible);
            if (!visible || !_lineRenderer) return;

            var box = bounds.Value;
            _lineRenderer.positionCount = 4;
            _lineRenderer.SetPosition(0, new Vector3(box.xMin, box.yMin, 0));
            _lineRenderer.SetPosition(1, new Vector3(box.xMax, box.yMin, 0));
            _lineRenderer.SetPosition(2, new Vector3(box.xMax, box.yMax, 0));
            _lineRenderer.SetPosition(3, new Vector3(box.xMin, box.yMax, 0));
            if (!_canvasRect) return;
            _canvasRect.localPosition = new Vector3(
                box.center.x + _canvasOffset.x * _canvasRect.localScale.x,
                box.yMin + _canvasOffset.y * _canvasRect.localScale.y,
                _canvasOffset.z * _canvasRect.localScale.z);
        }
    }
}
