using UnityEngine;

namespace QuestCameraKit.OpenAI
{
    public class TtsManager : MonoBehaviour
    {
        [SerializeField] private AudioPlayer audioPlayer;
        [SerializeField] private TtsModel model = TtsModel.Tts1;
        [SerializeField] private TtsVoice voice = TtsVoice.Alloy;
        [SerializeField, Range(0.25f, 4.0f)] private float speed = 1f;

        private ImageOpenAIConnector _imageOpenAIConnector;

        private void Awake()
        {
            _imageOpenAIConnector = FindAnyObjectByType<ImageOpenAIConnector>();
            if (!audioPlayer) audioPlayer = GetComponentInChildren<AudioPlayer>();
        }

        public void SynthesizeAndPlay(string text)
        {
            if (!_imageOpenAIConnector || !audioPlayer || string.IsNullOrWhiteSpace(text)) return;
            _imageOpenAIConnector.StartCoroutine(_imageOpenAIConnector.RequestTextToSpeech(
                text,
                audioData =>
                {
                    if (audioData == null || !this || !audioPlayer)
                    {
                        return;
                    }

                    Debug.Log("Playing audio.");
                    audioPlayer.ProcessAudioBytes(audioData);
                },
                error => { Debug.LogError("Failed to get audio data from OpenAI: " + error); },
                model, voice, speed
            ));
        }
    }
}