using System;
using System.Text;
using UnityEngine;
using OVRSimpleJSON;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace QuestCameraKit.OpenAI
{
    #region Enums and Extensions

    public enum TtsModel
    {
        Tts1,
        Tts1Hd,
    }

    public static class TtsModelExtensions
    {
        public static string EnumToString(this TtsModel model)
        {
            switch (model)
            {
                case TtsModel.Tts1:
                    return "tts-1";
                case TtsModel.Tts1Hd:
                    return "tts-1-hd";
                default:
                    Debug.Log(model + " is not a valid TTSModel.");
                    return "tts-1";
            }
        }
    }

    public enum TtsVoice
    {
        Alloy,
        Echo,
        Fable,
        Onyx,
        Nova,
        Shimmer
    }

    public enum OpenAIVisionModel
    {
        O1,
        Gpt4O,
        Gpt4OMini,
        Gpt4Turbo
    }

    public static class OpenAIVisionModelExtensions
    {
        public static string ToModelString(this OpenAIVisionModel model)
        {
            return model switch
            {
                OpenAIVisionModel.O1 => "o1",
                OpenAIVisionModel.Gpt4O => "gpt-4o",
                OpenAIVisionModel.Gpt4OMini => "gpt-4o-mini",
                OpenAIVisionModel.Gpt4Turbo => "gpt-4-turbo",
                _ => "gpt-4o"
            };
        }
    }

    public enum OpenAICommandMode
    {
        ImageAndText,
        ImageOnly,
        TextOnly
    }

    #endregion

    public class ImageOpenAIConnector : MonoBehaviour
    {
        [Header("API & Model Settings")] [Tooltip("Your OpenAI API key.")]
        public string apiKey = "YOUR_API_KEY";

        [SerializeField] private OpenAIVisionModel selectedModel = OpenAIVisionModel.Gpt4O;

        [Header("Command Settings")] public OpenAICommandMode commandMode = OpenAICommandMode.ImageAndText;
        [SerializeField] private string instructions = "";
        [SerializeField] private TtsManager ttsManager;
        [SerializeField] private SttManager sttManager;

        [Header("Events")] public UnityEvent<string> onResponseReceived;

        [Header("Processing Sound")] [Tooltip("Assign an AudioSource that plays a waiting sound.")] [SerializeField]
        private AudioSource processingAudioSource;

        private const string OutputFormat = "mp3";
        public bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey) &&
                                 apiKey != "YOUR_API_KEY" && apiKey != "YOUR_OPENAI_API_KEY";

        [Serializable]
        private class TtsPayload
        {
            public string model;
            public string input;
            public string voice;
            public string response_format;
            public float speed;
        }

        #region Audio Feedback

        private void Awake()
        {
            if (sttManager) sttManager.onRequestSent.AddListener(StartProcessingSound);
            onResponseReceived.AddListener(StopProcessingSound);
        }

        private void OnDestroy()
        {
            if (sttManager) sttManager.onRequestSent.RemoveListener(StartProcessingSound);
            onResponseReceived.RemoveListener(StopProcessingSound);
        }

        private void StartProcessingSound()
        {
            if (!processingAudioSource)
            {
                return;
            }

            processingAudioSource.loop = true;
            processingAudioSource.Play();
        }

        public void StopProcessingSound(string response)
        {
            if (processingAudioSource)
            {
                processingAudioSource.Stop();
            }
        }

        #endregion

        #region Text-To-Speech Functionality

        /// <summary>
        /// Requests text-to-speech audio from OpenAI.
        /// </summary>
        /// <param name="text">Text to be synthesized.</param>
        /// <param name="onSuccess">Callback with the resulting audio data.</param>
        /// <param name="onError">Callback with error message.</param>
        /// <param name="model">TTS model to use.</param>
        /// <param name="voice">Voice to use.</param>
        /// <param name="speed">Speed factor for the speech.</param>
        public IEnumerator RequestTextToSpeech(string text, Action<byte[]> onSuccess, Action<string> onError,
            TtsModel model = TtsModel.Tts1, TtsVoice voice = TtsVoice.Alloy, float speed = 1f)
        {
            if (!HasApiKey)
            {
                onError?.Invoke("Configure an API key before requesting speech.");
                yield break;
            }

            var payload = new TtsPayload
            {
                model = model.EnumToString(),
                input = text,
                voice = voice.ToString().ToLowerInvariant(),
                response_format = OutputFormat,
                speed = speed
            };

            var jsonPayload = JsonUtility.ToJson(payload);

            using var request = new UnityWebRequest("https://api.openai.com/v1/audio/speech", "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            request.timeout = 60;
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();
            StopProcessingSound("");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS Request Error: " + request.error);
                onError?.Invoke(request.error);
            }
            else
            {
                var audioData = request.downloadHandler.data;
                onSuccess?.Invoke(audioData);
            }
        }

        #endregion

        #region Image and Chat Functionality

        /// <summary>
        /// Sends an image (and optionally text) to OpenAI.
        /// </summary>
        /// <param name="image">The image to send.</param>
        /// <param name="command">The command text.</param>
        public void SendImage(Texture2D image, string command)
        {
            if (!HasApiKey)
            {
                StopProcessingSound("");
                Debug.LogWarning("Configure an API key before sending an image or command.");
                return;
            }
            StartCoroutine(SendImageRequest(image, command));
        }

        private IEnumerator SendImageRequest(Texture2D image, string command)
        {
            var base64Image = "";
            if (commandMode == OpenAICommandMode.ImageAndText || commandMode == OpenAICommandMode.ImageOnly)
            {
                if (!image)
                {
                    StopProcessingSound("");
                    Debug.LogError("An image is required for the selected command mode.");
                    yield break;
                }
                var processedImage = (image.width == 512 && image.height == 512 && image.format == TextureFormat.RGBA32)
                    ? image
                    : ResizeTexture(image, 512, 512);
                var imageBytes = processedImage.EncodeToJPG();
                if (processedImage != image) Destroy(processedImage);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Debug.LogError(
                        "Failed to encode image to JPG. Check that the texture is readable and uncompressed.");
                    yield break;
                }

                base64Image = Convert.ToBase64String(imageBytes);
            }

            var contentElements = new List<string>();

            if (!string.IsNullOrEmpty(instructions))
            {
                contentElements.Add($"{{\"type\":\"text\",\"text\":\"{EscapeJson(instructions)}\"}}");
            }

            switch (commandMode)
            {
                case OpenAICommandMode.ImageAndText:
                    contentElements.Add($"{{\"type\":\"text\",\"text\":\"{EscapeJson(command)}\"}}");
                    contentElements.Add(
                        $"{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/jpeg;base64,{base64Image}\"}}}}");
                    break;
                case OpenAICommandMode.ImageOnly:
                    contentElements.Add(
                        $"{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/jpeg;base64,{base64Image}\"}}}}");
                    break;
                case OpenAICommandMode.TextOnly:
                    contentElements.Add($"{{\"type\":\"text\",\"text\":\"{EscapeJson(command)}\"}}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var contentJson = string.Join(",", contentElements);
            var payloadJson = "{" +
                              $"\"model\":\"{selectedModel.ToModelString()}\"," +
                              "\"messages\":[{" +
                              $"\"role\":\"user\",\"content\":[{contentJson}]" +
                              "}]," +
                              "\"max_completion_tokens\":300" +
                              "}";

            using var request = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(payloadJson);

            request.timeout = 60;
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            StartProcessingSound();
            yield return request.SendWebRequest();
            StopProcessingSound("");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error sending request: {request.error} (Response code: {request.responseCode})");
            }
            else
            {
                var jsonResponse = JSON.Parse(request.downloadHandler.text);
                var responseContent = jsonResponse["choices"][0]["message"]["content"].Value;
                onResponseReceived?.Invoke(responseContent);
                StopProcessingSound("");

                if (ttsManager)
                {
                    ttsManager.SynthesizeAndPlay(responseContent);
                }

                Debug.Log(responseContent);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Resizes or converts the given texture to a 512x512 uncompressed RGBA32 texture.
        /// </summary>
        private Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            rt.filterMode = FilterMode.Bilinear;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        /// <summary>
        /// Escapes quotes, backslashes and control characters using the SDK JSON serializer.
        /// </summary>
        private string EscapeJson(string input)
        {
            var json = new JSONString(input ?? string.Empty).ToString();
            return json.Substring(1, json.Length - 2);
        }

        #endregion
    }
}