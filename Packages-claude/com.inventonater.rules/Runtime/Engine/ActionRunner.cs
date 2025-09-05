using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public class ActionRunner
    {
        private readonly Services _services;
        private bool _shouldStop;

        public ActionRunner(Services services)
        {
            _services = services;
        }

        public async UniTask RunActionsAsync(IEnumerable<ActionDto> actions, CancellationToken ct = default)
        {
            if (actions == null) return;
            
            _shouldStop = false;
            
            foreach (var action in actions)
            {
                if (_shouldStop || ct.IsCancellationRequested)
                {
                    Debug.Log("[ActionRunner] Stopping action sequence");
                    break;
                }
                
                await RunSingleActionAsync(action, ct);
            }
        }

        private async UniTask RunSingleActionAsync(ActionDto action, CancellationToken ct)
        {
            if (action == null || string.IsNullOrEmpty(action.type))
            {
                Debug.LogWarning("[ActionRunner] Invalid action (null or missing type)");
                return;
            }

            switch (action.type)
            {
                case "service_call":
                    await RunServiceCall(action, ct);
                    break;
                    
                case "wait_duration":
                    await RunWaitDuration(action, ct);
                    break;
                    
                case "repeat_count":
                    await RunRepeatCount(action, ct);
                    break;
                    
                case "stop":
                    _shouldStop = true;
                    Debug.Log("[ActionRunner] Stop action encountered");
                    break;
                    
                default:
                    Debug.LogWarning($"[ActionRunner] Unknown action type: {action.type}");
                    break;
            }
        }

        private async UniTask RunServiceCall(ActionDto action, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(action.service))
            {
                Debug.LogWarning("[ActionRunner] service_call missing service name");
                return;
            }
            
            if (_services == null)
            {
                Debug.LogWarning("[ActionRunner] Services component not found");
                return;
            }
            
            Debug.Log($"[ActionRunner] Calling service: {action.service}");
            await _services.CallServiceAsync(action.service, action.data ?? new Dictionary<string, object>(), ct);
        }

        private async UniTask RunWaitDuration(ActionDto action, CancellationToken ct)
        {
            int durationMs = Mathf.Clamp(action.duration_ms_0_to_60000, 0, 60000);
            if (durationMs <= 0)
            {
                Debug.LogWarning("[ActionRunner] wait_duration has invalid duration");
                return;
            }
            
            Debug.Log($"[ActionRunner] Waiting for {durationMs}ms");
            await UniTask.Delay(durationMs, cancellationToken: ct);
        }

        private async UniTask RunRepeatCount(ActionDto action, CancellationToken ct)
        {
            int count = Mathf.Clamp(action.count_1_to_20, 1, 20);
            if (action.actions == null || action.actions.Count == 0)
            {
                Debug.LogWarning("[ActionRunner] repeat_count has no actions to repeat");
                return;
            }
            
            Debug.Log($"[ActionRunner] Repeating {action.actions.Count} actions {count} times");
            
            for (int i = 0; i < count; i++)
            {
                if (_shouldStop || ct.IsCancellationRequested)
                {
                    break;
                }
                
                foreach (var nestedAction in action.actions)
                {
                    if (_shouldStop || ct.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    await RunSingleActionAsync(nestedAction, ct);
                }
            }
        }
    }
}