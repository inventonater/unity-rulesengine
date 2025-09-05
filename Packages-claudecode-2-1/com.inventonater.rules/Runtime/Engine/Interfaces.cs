using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Inventonater.Rules
{
    public interface IRuleRepository
    {
        IEnumerable<RuleDto> GetAllRules();
        RuleDto GetRule(string id);
        void LoadFromJson(string json);
    }

    public interface IEntityStore
    {
        T Get<T>(string key);
        void Set(string key, object value);
        bool TryGet<T>(string key, out T value);
        event Action<string, object> OnChanged;
    }

    public interface IEventBus
    {
        void Fire(string eventName);
        IDisposable Subscribe(string eventName, Action handler);
    }

    public interface IServices
    {
        UniTask PlaySound(string sound, float volume);
        void Log(string message, string severity);
    }

    public interface IActionRunner
    {
        UniTask RunAsync(List<ActionDto> actions, CancellationToken ct);
    }

    public interface IConditionEvaluator
    {
        bool Evaluate(List<ConditionDto> conditions);
    }
}