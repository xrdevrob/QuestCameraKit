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

using System.Linq;

using TMPro;

using UnityEngine;


namespace Meta.XR.MRUtilityKitSamples.QRCodeDetection
{
    [MetaCodeSample("MRUKSample-QRCodeDetection")]
    public sealed class QRCode : MonoBehaviour
    {
        public string PayloadText => _text.text;

        [SerializeField]
        TMP_Text _text;

        [SerializeField]
        RectTransform _background;

        public void Initialize(MRUKTrackable trackable)
        {
            if (trackable.MarkerPayloadString is { } str)
            {
                _text.text = $"\"{str}\"";
            }
            else if (trackable.MarkerPayloadBytes is { } bytes)
            {
                _text.text = $"Binary(data=[{string.Join(" ", bytes.Take(16).Select(b => $"{b:x02}"))}{(bytes.Length > 16 ? " ..." : "")}], length={bytes.Length})";
            }
            else
            {
                _text.text = "(no payload)";
            }

            if (!_background)
            {
                return;
            }

            _text.ForceMeshUpdate();

            var bounds = _text.textBounds;
            _background.position = _text.transform.TransformPoint(bounds.center);

            var size = bounds.size;
            size.x += 16f;
            size.y += 16f;
            _background.sizeDelta = size;
        }
    }
}
