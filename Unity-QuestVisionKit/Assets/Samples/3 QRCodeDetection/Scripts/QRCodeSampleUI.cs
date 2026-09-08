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

using Meta.XR.Samples;

using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.UI;


namespace Meta.XR.MRUtilityKitSamples.QRCodeDetection
{
    [MetaCodeSample("MRUKSample-QRCodeDetection")]
    class QRCodeSampleUI : MonoBehaviour
    {
        //
        // Public interface

        public void Log(object log, LogType type = LogType.Log)
        {
            if (_logLines++ > 0)
            {
                _logBuilder.Append('\n');
            }

            _logBuilder.Append(_logLines);

            string c = null;
            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    c = "<color=red>";
                    break;
                case LogType.Warning:
                    c = "<color=yellow>";
                    break;
            }

            _logBuilder.Append($"{c} | {log}");

            if (c is not null)
            {
                _logBuilder.Append("</color>");
            }

            Debug.LogFormat(
                logType: type,
                logOptions: LogOption.None,
                context: this,
                format: "{0}: {1}", nameof(QRCodeSampleUI), log
            );
        }


        //
        // Serialized fields

        [Header("Config")]
        [SerializeField, Range(0f, 7f)]
        float _updateIntervalSeconds = 0.5f;

        [Header("Scene References")]
        [SerializeField]
        Image _icoTrackingSupported;
        [SerializeField]
        Image _icoTrackingNotSupported;

        [SerializeField]
        Image _icoHasPermissions;
        [SerializeField]
        Image _icoNoPermissions;
        [SerializeField]
        Button _btnRequestPermissions;

        [SerializeField]
        Toggle _togTrackingEnabled;

        [SerializeField]
        TMP_Text _txtNumActive;

        [SerializeField]
        TMP_Text _txtLogs;

        // non-serialized fields

        readonly System.Text.StringBuilder _logBuilder = new();
        int _logLines;


        //
        // UnityEvent listeners

        public void Tog_TrackingEnabled(bool value)
        {
            Log($"{nameof(Tog_TrackingEnabled)}({value})");

            QRCodeManager.TrackingEnabled = value;
        }

        public void Btn_RequestPermissions()
        {
            Log(nameof(Btn_RequestPermissions));

            QRCodeManager.RequestRequiredPermissions(hasPerms =>
            {
                _icoHasPermissions.enabled = hasPerms;
                _icoNoPermissions.enabled = !hasPerms;
            });
        }


        //
        // private impl.

        void OnValidate()
        {
            var raycaster = GetComponentInChildren<OVRRaycaster>();
            if (raycaster && !raycaster.pointer)
            {
                raycaster.pointer = GameObject.Find("RightHandAnchor");
            }
        }

        void UpdateUI()
        {
            // QRCodeTrackingSupported?
            bool isSupported = QRCodeManager.IsSupported;
            _icoTrackingSupported.enabled = isSupported;
            _icoTrackingNotSupported.enabled = !isSupported;

            // HasPermissions?
            bool hasPerms = QRCodeManager.HasPermissions;
            _icoHasPermissions.enabled = hasPerms;
            _icoNoPermissions.enabled = !hasPerms;

            // QRCodeTrackingEnabled?
            bool trackingEnabled = QRCodeManager.TrackingEnabled;
            _togTrackingEnabled.SetIsOnWithoutNotify(trackingEnabled);

            // # Active MRUKTrackers:
            int nActive = QRCodeManager.ActiveTrackedCount;
            _txtNumActive.text = $"{nActive}";

            // Info Panel - Logs
            _txtLogs.SetText(_logBuilder);
            _txtLogs.pageToDisplay = _txtLogs.textInfo?.pageCount ?? 1;
        }

        void OnEnable()
        {
            UpdateUI();

            _ = StartCoroutine(coroutine());

            return;

            IEnumerator coroutine()
            {
                var interval = new WaitForSecondsRealtime(_updateIntervalSeconds);
                var lateUpdate = new WaitForEndOfFrame();
                while (this)
                {
                    yield return interval;
                    yield return lateUpdate;
                    UpdateUI();
                }
            }
        }

    } // end class QRCodeSampleUI
}
