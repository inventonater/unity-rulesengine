using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Desktop services implementation - audio, log, toast, state
    /// </summary>
    public class Services : MonoBehaviour, IServices
    {
        private EntityStore _store;
        private AudioSource _audioSource;
        private AudioClip _beepClip;
        
        // Toast UI
        private bool _showingToast;
        private string _toastMessage = "";
        private float _toastEndTime;
        private Rect _toastRect;
        
        private void Awake()
        {
            _store = GetComponent<EntityStore>();
            if (_store == null)
            {
                _store = gameObject.AddComponent<EntityStore>();
            }
            
            // Set up audio
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // Load beep sound
            _beepClip = Resources.Load<AudioClip>("beep");
            if (_beepClip == null)
            {
                Debug.LogWarning("[Services] beep.wav not found in Resources folder");
            }
        }
        
        private void OnGUI()
        {
            if (_showingToast && Time.time < _toastEndTime)
            {
                // Draw toast notification
                GUI.skin.box.fontSize = 18;
                GUI.skin.box.alignment = TextAnchor.MiddleCenter;
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.Box(_toastRect, "");
                GUI.color = Color.white;
                GUI.Box(_toastRect, _toastMessage);
            }
            else if (_showingToast)
            {
                _showingToast = false;
            }
        }
        
        public async UniTask ExecuteServiceAsync(string service, Dictionary<string, object> data, CancellationToken ct)
        {
            Debug.Log($"[Services] Executing service: {service}");
            
            switch (service)
            {
                case "audio.play":
                    await PlayAudio(data, ct);
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
            
            await UniTask.CompletedTask;
        }
        
        private async UniTask PlayAudio(Dictionary<string, object> data, CancellationToken ct)
        {
            if (_beepClip == null)
            {
                Debug.LogWarning("[Services] No audio clip available");
                return;
            }
            
            var clipName = GetStringParam(data, "clip", "beep");
            var volume = GetFloatParam(data, "volume_0_to_1", 0.5f);
            
            if (clipName == "beep")
            {
                _audioSource.PlayOneShot(_beepClip, Mathf.Clamp01(volume));
                Debug.Log($"[Services] Played beep at volume {volume}");
                
                // Wait for clip to finish if needed
                await UniTask.Delay((int)(_beepClip.length * 1000), cancellationToken: ct);
            }
            else
            {
                Debug.LogWarning($"[Services] Unknown audio clip: {clipName}");
            }
        }
        
        private void LogDebug(Dictionary<string, object> data)
        {
            var message = GetStringParam(data, "message", "Debug message");
            var level = GetStringParam(data, "level", "info");
            
            switch (level.ToLower())
            {
                case "error":
                    Debug.LogError($"[Rule] {message}");
                    break;
                case "warning":
                    Debug.LogWarning($"[Rule] {message}");
                    break;
                default:
                    Debug.Log($"[Rule] {message}");
                    break;
            }
        }
        
        private void ShowToast(Dictionary<string, object> data)
        {
            _toastMessage = GetStringParam(data, "text", "Toast notification");
            var duration = GetIntParam(data, "duration_ms_0_to_10000", 2000);
            
            duration = Mathf.Clamp(duration, 0, 10000);
            
            _showingToast = true;
            _toastEndTime = Time.time + (duration / 1000f);
            
            // Calculate toast rect (centered at top of screen)
            var width = 400f;
            var height = 60f;
            _toastRect = new Rect(
                (Screen.width - width) / 2f,
                50f,
                width,
                height
            );
            
            Debug.Log($"[Services] Showing toast: {_toastMessage} for {duration}ms");
        }
        
        private void SetState(Dictionary<string, object> data)
        {
            var key = GetStringParam(data, "key", null);
            var value = data.TryGetValue("value", out var val) ? val?.ToString() : null;
            
            if (!string.IsNullOrEmpty(key) && value != null)
            {
                if (double.TryParse(value, out var numericValue))
                {
                    _store.SetNumeric(key, numericValue);
                    Debug.Log($"[Services] Set numeric state {key} = {numericValue}");
                }
                else
                {
                    _store.SetState(key, value);
                    Debug.Log($"[Services] Set string state {key} = {value}");
                }
            }
            else
            {
                Debug.LogWarning("[Services] state.set requires {key, value}");
            }
        }
        
        // Helper methods for parameter extraction
        private string GetStringParam(Dictionary<string, object> data, string key, string defaultValue)
        {
            if (data == null || !data.TryGetValue(key, out var value))
                return defaultValue;
            
            return value?.ToString() ?? defaultValue;
        }
        
        private float GetFloatParam(Dictionary<string, object> data, string key, float defaultValue)
        {
            if (data == null || !data.TryGetValue(key, out var value))
                return defaultValue;
            
            if (value is float f)
                return f;
            if (value is double d)
                return (float)d;
            if (value is int i)
                return i;
            if (value is long l)
                return l;
            
            if (float.TryParse(value?.ToString(), out var parsed))
                return parsed;
            
            return defaultValue;
        }
        
        private int GetIntParam(Dictionary<string, object> data, string key, int defaultValue)
        {
            if (data == null || !data.TryGetValue(key, out var value))
                return defaultValue;
            
            if (value is int i)
                return i;
            if (value is long l)
                return (int)l;
            if (value is float f)
                return (int)f;
            if (value is double d)
                return (int)d;
            
            if (int.TryParse(value?.ToString(), out var parsed))
                return parsed;
            
            return defaultValue;
        }
    }
}
