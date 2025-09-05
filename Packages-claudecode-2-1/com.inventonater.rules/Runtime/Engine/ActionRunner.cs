using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public class ActionRunner : IActionRunner
    {
        private readonly IServices _services;
        private readonly IEntityStore _store;

        public ActionRunner(IServices services, IEntityStore store)
        {
            _services = services;
            _store = store;
        }

        public async UniTask RunAsync(List<ActionDto> actions, CancellationToken ct)
        {
            if (actions == null || actions.Count == 0) return;
            
            foreach (var action in actions)
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    await ExecuteAction(action, ct);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Action execution error ({action.type}): {e}");
                }
            }
        }

        private async UniTask ExecuteAction(ActionDto action, CancellationToken ct)
        {
            if (action == null) return;
            
            if (action.delay_ms > 0)
            {
                await UniTask.Delay(action.delay_ms, cancellationToken: ct);
            }
            
            switch (action.type)
            {
                case "play_sound":
                    await PlaySound(action);
                    break;
                
                case "set_value":
                    SetValue(action);
                    break;
                
                case "increment_value":
                    IncrementValue(action);
                    break;
                
                case "log":
                    LogMessage(action);
                    break;
                
                case "wait":
                    await Wait(action, ct);
                    break;
            }
        }

        private async UniTask PlaySound(ActionDto action)
        {
            if (string.IsNullOrEmpty(action.sound)) return;
            
            float volume = Mathf.Clamp01(action.volume_0_to_1);
            await _services.PlaySound(action.sound, volume);
        }

        private void SetValue(ActionDto action)
        {
            if (string.IsNullOrEmpty(action.entity)) return;
            
            object value = ParseValue(action.value);
            _store.Set(action.entity, value);
        }

        private void IncrementValue(ActionDto action)
        {
            if (string.IsNullOrEmpty(action.entity)) return;
            
            var current = _store.Get<double>(action.entity);
            double increment = 1.0;
            
            if (!string.IsNullOrEmpty(action.value) && double.TryParse(action.value, out double parsed))
            {
                increment = parsed;
            }
            
            _store.Set(action.entity, current + increment);
        }

        private void LogMessage(ActionDto action)
        {
            string message = action.message ?? "Rule action executed";
            string severity = action.severity ?? "info";
            
            _services.Log(message, severity);
        }

        private async UniTask Wait(ActionDto action, CancellationToken ct)
        {
            int waitMs = action.delay_ms;
            if (waitMs <= 0) return;
            
            await UniTask.Delay(waitMs, cancellationToken: ct);
        }

        private object ParseValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            
            if (bool.TryParse(value, out bool boolValue))
                return boolValue;
            
            if (int.TryParse(value, out int intValue))
                return intValue;
            
            if (double.TryParse(value, out double doubleValue))
                return doubleValue;
            
            return value;
        }
    }
}