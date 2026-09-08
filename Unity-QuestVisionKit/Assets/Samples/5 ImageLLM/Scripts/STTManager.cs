using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace QuestCameraKit.OpenAI
{
    public class SttManager : MonoBehaviour
    {
        public event Action<string> OnTranscriptionComplete;
        [Header("STT Settings")]
        [SerializeField, Min(1)] private int recordingMaximum = 5;
        [Header("Canvas Settings")]
        [SerializeField] private bool useCanvas = true;
        [Header("UI Components (Optional)")]
        [SerializeField] private Button recordButton;
        [SerializeField] private Text transcriptionText;
        [SerializeField] private Dropdown microphoneDropdown;
        [Header("Events")] public UnityEvent onRequestStarted;
        public UnityEvent onRequestSent;

        private ImageOpenAIConnector _imageOpenAIConnector;
        private AudioClip _clip;
        private string _selectedMic;
        private bool _isRecording;
        private bool _isSending;
        private float _time;
        private UnityWebRequest _request;

        private void Start()
        {
            _imageOpenAIConnector = FindAnyObjectByType<ImageOpenAIConnector>();
            if (!_imageOpenAIConnector)
            {
                Debug.LogError("SttManager requires an ImageOpenAIConnector.");
                enabled = false;
                return;
            }
            if (useCanvas && microphoneDropdown)
            {
                microphoneDropdown.ClearOptions();
                microphoneDropdown.AddOptions(new List<string>(Microphone.devices));
                microphoneDropdown.SetValueWithoutNotify(Mathf.Clamp(
                    PlayerPrefs.GetInt("user-mic-device-index", 0), 0, Mathf.Max(0, Microphone.devices.Length - 1)));
                microphoneDropdown.onValueChanged.AddListener(ChangeMicrophone);
            }
            if (useCanvas && recordButton) recordButton.onClick.AddListener(ToggleRecording);
        }

        private void ChangeMicrophone(int index) => PlayerPrefs.SetInt("user-mic-device-index", index);

        private void ToggleRecording()
        {
            if (!isActiveAndEnabled || _isSending) return;
            if (_isRecording) EndRecording();
            else StartRecording();
        }

        private void StartRecording()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => { if (this && isActiveAndEnabled) StartRecording(); };
                callbacks.PermissionDenied += _ => { if (this) SetStatus("Microphone permission denied."); };
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
                return;
            }
#endif
            if (_isRecording || _isSending) return;
            var devices = Microphone.devices;
            if (devices.Length == 0)
            {
                SetStatus("No microphone found.");
                return;
            }
            var index = useCanvas && microphoneDropdown
                ? microphoneDropdown.value : PlayerPrefs.GetInt("user-mic-device-index", 0);
            _selectedMic = devices[Mathf.Clamp(index, 0, devices.Length - 1)];
            _clip = Microphone.Start(_selectedMic, false, Mathf.Max(1, recordingMaximum), 44100);
            if (!_clip)
            {
                SetStatus("Could not start microphone.");
                return;
            }
            _time = 0f;
            _isRecording = true;
            SetStatus("Recording...");
            onRequestStarted?.Invoke();
        }

        private async void EndRecording()
        {
            if (!_isRecording) return;
            var frames = Microphone.GetPosition(_selectedMic);
            // A non-looping recording can report zero once its full buffer has stopped.
            if (frames <= 0 && _clip && _time >= Mathf.Max(1, recordingMaximum)) frames = _clip.samples;
            Microphone.End(_selectedMic);
            _isRecording = false;
            _isSending = true;
            if (useCanvas && recordButton) recordButton.interactable = false;
            try
            {
                if (!_clip || frames <= 0) throw new InvalidOperationException("No audio was recorded.");
                var audio = SaveWav.Save("output.wav", _clip, frames);
                Destroy(_clip);
                _clip = null;
                SetStatus("Processing...");
                onRequestSent?.Invoke();
                var transcription = await SendToOpenAI(audio);
                if (!this || !isActiveAndEnabled) return;
                _imageOpenAIConnector.StopProcessingSound("");
                SetStatus(transcription);
                if (!string.IsNullOrWhiteSpace(transcription)) OnTranscriptionComplete?.Invoke(transcription);
            }
            catch (Exception e)
            {
                if (this && isActiveAndEnabled)
                {
                    if (_imageOpenAIConnector) _imageOpenAIConnector.StopProcessingSound("");
                    SetStatus("Transcription failed: " + e.Message);
                    Debug.LogWarning("Transcription failed: " + e.Message);
                }
            }
            finally
            {
                _isSending = false;
                if (this)
                {
                    if (_clip) Destroy(_clip);
                    _clip = null;
                    if (useCanvas && recordButton) recordButton.interactable = true;
                }
            }
        }

        private async Task<string> SendToOpenAI(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0 || audioData.Length > 25 * 1024 * 1024)
                throw new InvalidOperationException("Recording must contain between 1 byte and 25 MB.");
            if (!_imageOpenAIConnector || !_imageOpenAIConnector.HasApiKey)
                throw new InvalidOperationException("Configure an API key before transcribing audio.");
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", audioData, "output.wav", "audio/wav"),
                new MultipartFormDataSection("model", "whisper-1"),
                new MultipartFormDataSection("response_format", "text")
            };
            using var request = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form);
            request.timeout = 60;
            request.SetRequestHeader("Authorization", "Bearer " + _imageOpenAIConnector.apiKey);
            _request = request;
            try
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException($"HTTP {request.responseCode}: {request.error}");
                return request.downloadHandler.text.Trim();
            }
            finally { _request = null; }
        }

        private void SetStatus(string text)
        {
            if (useCanvas && transcriptionText) transcriptionText.text = text;
        }

        private void OnDisable()
        {
            if (_isRecording) Microphone.End(_selectedMic);
            _isRecording = false;
            _request?.Abort();
            if (_clip) Destroy(_clip);
            _clip = null;
        }

        private void OnDestroy()
        {
            if (recordButton) recordButton.onClick.RemoveListener(ToggleRecording);
            if (microphoneDropdown) microphoneDropdown.onValueChanged.RemoveListener(ChangeMicrophone);
        }

        private void Update()
        {
            if (_isRecording)
            {
                _time += Time.deltaTime;
                if (_time >= Mathf.Max(1, recordingMaximum)) EndRecording();
            }
            if (OVRInput.GetDown(OVRInput.Button.Start)) ToggleRecording();
        }
    }
}
