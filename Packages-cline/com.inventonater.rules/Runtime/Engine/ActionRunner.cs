using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public interface IServices
    {
        UniTask ExecuteServiceAsync(string service, Dictionary<string, object> data, CancellationToken ct);
    }
    
    /// <summary>
    /// Executes rule actions sequentially
    /// </summary>
    public class ActionRunner
    {
        private readonly IServices _services;
        
        public ActionRunner(IServices services)
        {
            _services = services;
        }
        
        public async UniTask RunActionsAsync(List<ActionDto> actions, CancellationToken ct)
        {
            if (actions == null || actions.Count == 0)
                return;
            
            foreach (var action in actions)
            {
                if (ct.IsCancellationRequested)
                    break;
                    
                await RunActionAsync(action, ct);
            }
        }
        
        private async UniTask RunActionAsync(ActionDto action, CancellationToken ct)
        {
            if (action == null)
                return;
            
            switch (action.type)
            {
                case "service_call":
                    await ExecuteServiceCall(action, ct);
                    break;
                    
                case "wait_duration":
                    await ExecuteWait(action, ct);
                    break;
                    
                case "repeat_count":
                    await ExecuteRepeat(action, ct);
                    break;
                    
                case "stop":
                    // Stop execution by throwing cancellation
                    ct.ThrowIfCancellationRequested();
                    throw new System.OperationCanceledException("Stop action executed");
                    
                default:
                    Debug.LogWarning($"Unknown action type: {action.type}");
                    break;
            }
        }
        
        private async UniTask ExecuteServiceCall(ActionDto action, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(action.service))
            {
                Debug.LogWarning("Service call action missing service name");
                return;
            }
            
            await _services.ExecuteServiceAsync(action.service, action.data ?? new Dictionary<string, object>(), ct);
        }
        
        private async UniTask ExecuteWait(ActionDto action, CancellationToken ct)
        {
            var durationMs = Mathf.Clamp(action.duration_ms_0_to_60000, 0, 60000);
            if (durationMs > 0)
            {
                await UniTask.Delay(durationMs, cancellationToken: ct);
            }
        }
        
        private async UniTask ExecuteRepeat(ActionDto action, CancellationToken ct)
        {
            var count = Mathf.Clamp(action.count_1_to_20, 1, 20);
            
            if (action.actions == null || action.actions.Count == 0)
            {
                Debug.LogWarning("Repeat action has no nested actions");
                return;
            }
            
            for (int i = 0; i < count; i++)
            {
                if (ct.IsCancellationRequested)
                    break;
                    
                await RunActionsAsync(action.actions, ct);
            }
        }
    }
}
