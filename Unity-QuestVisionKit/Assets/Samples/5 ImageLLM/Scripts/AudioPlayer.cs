using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace QuestCameraKit.OpenAI
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        private AudioSource _audioSource;
        private AudioClip _ownedClip;
        private int _requestId;
        private readonly HashSet<string> _pendingFiles = new();

        private void Awake() => _audioSource = GetComponent<AudioSource>();

        public void ProcessAudioBytes(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0 || !isActiveAndEnabled) return;
            var filePath = Path.Combine(Application.temporaryCachePath, $"tts-{Guid.NewGuid():N}.mp3");
            File.WriteAllBytes(filePath, audioData);
            _pendingFiles.Add(filePath);
            StartCoroutine(LoadAndPlayAudio(filePath, ++_requestId));
        }

        private IEnumerator LoadAndPlayAudio(string filePath, int requestId)
        {
            try
            {
                using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(filePath).AbsoluteUri, AudioType.MPEG);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Audio file loading error: " + request.error);
                    yield break;
                }
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (requestId != _requestId || !isActiveAndEnabled)
                {
                    Destroy(clip);
                    yield break;
                }
                _audioSource.Stop();
                if (_ownedClip) Destroy(_ownedClip);
                _ownedClip = clip;
                _audioSource.clip = clip;
                _audioSource.Play();
            }
            finally
            {
                File.Delete(filePath);
                _pendingFiles.Remove(filePath);
            }
        }

        private void OnDisable()
        {
            ++_requestId;
            if (_audioSource) _audioSource.Stop();
        }

        private void OnDestroy()
        {
            if (_ownedClip) Destroy(_ownedClip);
            foreach (var path in _pendingFiles) File.Delete(path);
            _pendingFiles.Clear();
        }
    }
}
