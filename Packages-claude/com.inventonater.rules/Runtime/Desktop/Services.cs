using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Inventonater.Rules
{
    public class Services : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _beepClip;
        
        [Header("UI")]
        [SerializeField] private GameObject _toastPrefab;
        [SerializeField] private Transform _toastContainer;
        
        private void Awake()
        {
            // Create audio source if not assigned
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }
            
            // Try to load beep sound from Resources
            if (_beepClip == null)
            {
                _beepClip = Resources.Load<AudioClip>("beep");
                if (_beepClip == null)
                {
                    Debug.LogWarning("[Services] Beep audio clip not found in Resources/beep.wav");
                }
            }
        }

        public async UniTask CallServiceAsync(string service, Dictionary<string, object> data, CancellationToken ct = default)
        {
            switch (service)
            {
                case "audio.play":
                    PlayAudio(data);
                    break;
                    
                case "debug.log":
                    LogDebug(data);
                    break;
                    
                case "ui.toast":
                    ShowToast(data);
                    break;
                    
                case "state.set":
                    SetState(data);
                    break;
                    
                default:
                    Debug.LogWarning($"[Services] Unknown service: {service}");
                    break;
            }
            
            await UniTask.Yield(ct);
        }

        private void PlayAudio(Dictionary<string, object> data)
        {
            string clipName = GetStringParam(data, "clip", "beep");
            float volume = GetFloatParam(data, "volume_0_to_1", 1.0f);
            volume = Mathf.Clamp01(volume);
            
            if (clipName == "beep" && _beepClip != null)
            {
                _audioSource.PlayOneShot(_beepClip, volume);
                Debug.Log($"[Services] Playing beep at volume {volume:F2}");
            }
            else
            {
                // Try to load clip from Resources
                var clip = Resources.Load<AudioClip>(clipName);
                if (clip != null)
                {
                    _audioSource.PlayOneShot(clip, volume);
                    Debug.Log($"[Services] Playing audio '{clipName}' at volume {volume:F2}");
                }
                else
                {
                    Debug.LogWarning($"[Services] Audio clip '{clipName}' not found");
                }
            }
        }

        private void LogDebug(Dictionary<string, object> data)
        {
            string message = GetStringParam(data, "message", "Debug message");
            string level = GetStringParam(data, "level", "log");
            
            switch (level.ToLower())
            {
                case "warning":
                    Debug.LogWarning($"[RULE] {message}");
                    break;
                case "error":
                    Debug.LogError($"[RULE] {message}");
                    break;
                default:
                    Debug.Log($"[RULE] {message}");
                    break;
            }
        }

        private void ShowToast(Dictionary<string, object> data)
        {
            string text = GetStringParam(data, "text", "Toast message");
            int durationMs = GetIntParam(data, "duration_ms_0_to_10000", 2000);
            durationMs = Mathf.Clamp(durationMs, 0, 10000);
            
            Debug.Log($"[Services] Toast: '{text}' for {durationMs}ms");
            
            // If we have a UI setup, show actual toast
            if (_toastPrefab != null && _toastContainer != null)
            {
                ShowToastUI(text, durationMs).Forget();
            }
            else
            {
                // Fallback to console
                Debug.Log($"[TOAST] {text}");
            }
        }

        private async UniTaskVoid ShowToastUI(string text, int durationMs)
        {
            var toast = Instantiate(_toastPrefab, _toastContainer);
            var textComponent = toast.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = text;
            }
            
            await UniTask.Delay(durationMs);
            
            if (toast != null)
            {
                Destroy(toast);
            }
        }

        private void SetState(Dictionary<string, object> data)
        {
            string key = GetStringParam(data, "key", null);
            object value = data.TryGetValue("value", out var v) ? v : null;
            
            if (string.IsNullOrEmpty(key) || value == null)
            {
                Debug.LogWarning("[Services] state.set requires both 'key' and 'value' parameters");
                return;
            }
            
            var store = FindObjectOfType<EntityStore>();
            if (store == null)
            {
                Debug.LogWarning("[Services] EntityStore not found for state.set");
                return;
            }
            
            string valueStr = value.ToString();
            
            // Try to parse as number first
            if (double.TryParse(valueStr, out var numValue))
            {
                store.SetNumeric(key, numValue);
                Debug.Log($"[Services] Set numeric state '{key}' = {numValue}");
            }
            else
            {
                store.SetState(key, valueStr);
                Debug.Log($"[Services] Set string state '{key}' = '{valueStr}'");
            }
        }

        private string GetStringParam(Dictionary<string, object> data, string key, string defaultValue)
        {
            if (data.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }
            return defaultValue;
        }

        private float GetFloatParam(Dictionary<string, object> data, string key, float defaultValue)
        {
            if (data.TryGetValue(key, out var value))
            {
                if (value is float f) return f;
                if (value is double d) return (float)d;
                if (value is int i) return i;
                if (value is long l) return l;
                if (float.TryParse(value?.ToString(), out var parsed))
                {
                    return parsed;
                }
            }
            return defaultValue;
        }

        private int GetIntParam(Dictionary<string, object> data, string key, int defaultValue)
        {
            if (data.TryGetValue(key, out var value))
            {
                if (value is int i) return i;
                if (value is long l) return (int)l;
                if (value is float f) return (int)f;
                if (value is double d) return (int)d;
                if (int.TryParse(value?.ToString(), out var parsed))
                {
                    return parsed;
                }
            }
            return defaultValue;
        }
    }
}