using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public class Services : MonoBehaviour, IServices
    {
        private AudioSource _audioSource;
        private readonly Dictionary<string, AudioClip> _audioCache = new();

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        public async UniTask PlaySound(string sound, float volume)
        {
            if (string.IsNullOrEmpty(sound)) return;
            
            AudioClip clip = await LoadAudioClip(sound);
            
            if (clip != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }

        private async UniTask<AudioClip> LoadAudioClip(string soundName)
        {
            if (_audioCache.TryGetValue(soundName, out var cached))
            {
                return cached;
            }
            
            var clip = Resources.Load<AudioClip>($"Audio/{soundName}");
            
            if (clip == null)
            {
                clip = await Resources.LoadAsync<AudioClip>($"Audio/{soundName}") as AudioClip;
            }
            
            if (clip != null)
            {
                _audioCache[soundName] = clip;
            }
            else
            {
                Debug.LogWarning($"Audio clip not found: {soundName}");
            }
            
            return clip;
        }

        public void Log(string message, string severity)
        {
            switch (severity?.ToLower())
            {
                case "error":
                    Debug.LogError($"[Rule] {message}");
                    break;
                case "warning":
                case "warn":
                    Debug.LogWarning($"[Rule] {message}");
                    break;
                default:
                    Debug.Log($"[Rule] {message}");
                    break;
            }
        }
    }
}