# Unity Automation Runner System Implementation Guide

## Building a Home Assistant-style automation framework in Unity

This comprehensive guide synthesizes research from Home Assistant's architecture, Unity event systems, rule engines, and async patterns to create a robust automation runner system using pure C#, Newtonsoft.Json, and UniTask—without relying on MonoBehaviours or ScriptableObjects.

## Architecture overview

The proposed system combines Home Assistant's **trigger-condition-action pattern** with Unity's event-driven capabilities, creating a flexible automation framework. At its core, the system uses an **event bus architecture** for publish-subscribe communication, a **state registry** for entity tracking, and an **automation engine** that evaluates rules and executes actions using UniTask for async operations.

The architecture separates concerns into distinct layers: the event system handles all communication, the state management layer tracks entity states, and the automation layer processes rules. This design enables complex automation scenarios while maintaining clean code boundaries and testability through dependency injection patterns.

## Core event system implementation

The foundation begins with a thread-safe event bus that avoids Unity's MonoBehaviour dependencies:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

public interface IAutomationEvent 
{
    string EventType { get; }
    DateTime Timestamp { get; }
    Dictionary<string, object> Data { get; }
    AutomationContext Context { get; }
}

public class EventBus
{
    private readonly Dictionary<Type, List<Func<IAutomationEvent, CancellationToken, UniTask>>> _handlers = new();
    private readonly object _lock = new();
    private readonly Queue<Action> _mainThreadQueue = new();
    
    public IDisposable Subscribe<T>(Func<T, CancellationToken, UniTask> handler) where T : IAutomationEvent
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Func<IAutomationEvent, CancellationToken, UniTask>>();
                _handlers[typeof(T)] = list;
            }
            
            Func<IAutomationEvent, CancellationToken, UniTask> wrapper = 
                (evt, ct) => handler((T)evt, ct);
            
            list.Add(wrapper);
            
            return new Subscription(() =>
            {
                lock (_lock)
                {
                    list.Remove(wrapper);
                }
            });
        }
    }
    
    public async UniTask PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) 
        where T : IAutomationEvent
    {
        List<Func<IAutomationEvent, CancellationToken, UniTask>> handlers;
        
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                return;
                
            handlers = new List<Func<IAutomationEvent, CancellationToken, UniTask>>(list);
        }
        
        var tasks = handlers.Select(h => h(eventData, cancellationToken));
        await UniTask.WhenAll(tasks);
    }
    
    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe?.Invoke();
    }
}
```

## State management architecture

The state system maintains entity states with immutability and change tracking, following Home Assistant's pattern:

```csharp
[JsonObject(MemberSerialization.OptIn)]
public class EntityState
{
    [JsonProperty("entity_id")]
    public string EntityId { get; }
    
    [JsonProperty("state")]
    public string State { get; }
    
    [JsonProperty("attributes")]
    public Dictionary<string, object> Attributes { get; }
    
    [JsonProperty("last_changed")]
    public DateTime LastChanged { get; }
    
    [JsonProperty("context")]
    public AutomationContext Context { get; }
    
    public EntityState(string entityId, string state, 
        Dictionary<string, object> attributes = null,
        AutomationContext context = null)
    {
        EntityId = entityId;
        State = state;
        Attributes = attributes ?? new Dictionary<string, object>();
        LastChanged = DateTime.UtcNow;
        Context = context ?? new AutomationContext();
    }
    
    public EntityState WithState(string newState, AutomationContext context = null)
    {
        return new EntityState(EntityId, newState, 
            new Dictionary<string, object>(Attributes), 
            context ?? Context);
    }
}

public class StateRegistry
{
    private readonly ConcurrentDictionary<string, EntityState> _states = new();
    private readonly EventBus _eventBus;
    
    public StateRegistry(EventBus eventBus)
    {
        _eventBus = eventBus;
    }
    
    public async UniTask SetStateAsync(string entityId, string state, 
        Dictionary<string, object> attributes = null,
        AutomationContext context = null,
        CancellationToken cancellationToken = default)
    {
        var oldState = _states.TryGetValue(entityId, out var existing) ? existing : null;
        var newState = new EntityState(entityId, state, attributes, context);
        
        _states[entityId] = newState;
        
        await _eventBus.PublishAsync(new StateChangedEvent
        {
            EntityId = entityId,
            OldState = oldState,
            NewState = newState,
            Context = context ?? new AutomationContext()
        }, cancellationToken);
    }
    
    public EntityState GetState(string entityId)
    {
        return _states.TryGetValue(entityId, out var state) ? state : null;
    }
    
    public bool IsState(string entityId, string expectedState)
    {
        var state = GetState(entityId);
        return state != null && state.State == expectedState;
    }
}
```

## Trigger implementation patterns

Triggers monitor system events and activate automations when conditions are met. The system supports multiple trigger types through a flexible base class:

```csharp
public abstract class AutomationTrigger
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;
    
    protected EventBus EventBus { get; private set; }
    protected StateRegistry StateRegistry { get; private set; }
    
    private IDisposable _subscription;
    
    public event Func<TriggerContext, CancellationToken, UniTask> Triggered;
    
    public virtual void Initialize(EventBus eventBus, StateRegistry stateRegistry)
    {
        EventBus = eventBus;
        StateRegistry = stateRegistry;
        _subscription = SetupEventSubscriptions();
    }
    
    protected abstract IDisposable SetupEventSubscriptions();
    
    protected async UniTask FireTriggerAsync(TriggerContext context, CancellationToken cancellationToken)
    {
        if (Enabled && Triggered != null)
        {
            await Triggered(context, cancellationToken);
        }
    }
    
    public virtual void Dispose()
    {
        _subscription?.Dispose();
    }
}

[JsonConverter(typeof(TriggerJsonConverter))]
public class StateTrigger : AutomationTrigger
{
    [JsonProperty("entity_id")]
    public string EntityId { get; set; }
    
    [JsonProperty("from")]
    public string FromState { get; set; }
    
    [JsonProperty("to")]
    public string ToState { get; set; }
    
    [JsonProperty("for")]
    public TimeSpan? Duration { get; set; }
    
    private CancellationTokenSource _durationCts;
    
    protected override IDisposable SetupEventSubscriptions()
    {
        return EventBus.Subscribe<StateChangedEvent>(HandleStateChange);
    }
    
    private async UniTask HandleStateChange(StateChangedEvent evt, CancellationToken cancellationToken)
    {
        if (evt.EntityId != EntityId)
            return;
            
        bool fromMatches = string.IsNullOrEmpty(FromState) || 
                          evt.OldState?.State == FromState;
        bool toMatches = string.IsNullOrEmpty(ToState) || 
                        evt.NewState?.State == ToState;
        
        if (!fromMatches || !toMatches)
            return;
        
        if (Duration.HasValue)
        {
            _durationCts?.Cancel();
            _durationCts = new CancellationTokenSource();
            
            try
            {
                await UniTask.Delay(Duration.Value, cancellationToken: _durationCts.Token);
                
                // Verify state hasn't changed
                if (StateRegistry.IsState(EntityId, evt.NewState.State))
                {
                    await FireTriggerAsync(new TriggerContext
                    {
                        TriggerId = Id,
                        TriggerData = evt,
                        Variables = new Dictionary<string, object>
                        {
                            ["trigger.entity_id"] = EntityId,
                            ["trigger.from_state"] = evt.OldState,
                            ["trigger.to_state"] = evt.NewState
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
        }
        else
        {
            await FireTriggerAsync(new TriggerContext
            {
                TriggerId = Id,
                TriggerData = evt
            }, cancellationToken);
        }
    }
}

public class TimeTrigger : AutomationTrigger
{
    [JsonProperty("at")]
    public TimeSpan? At { get; set; }
    
    [JsonProperty("interval")]
    public TimeSpan? Interval { get; set; }
    
    private CancellationTokenSource _timerCts;
    
    protected override IDisposable SetupEventSubscriptions()
    {
        _timerCts = new CancellationTokenSource();
        StartTimer(_timerCts.Token).Forget();
        
        return new DisposableAction(() => _timerCts?.Cancel());
    }
    
    private async UniTaskVoid StartTimer(CancellationToken cancellationToken)
    {
        if (At.HasValue)
        {
            await WaitForTimeOfDay(At.Value, cancellationToken);
            await FireTriggerAsync(new TriggerContext { TriggerId = Id }, cancellationToken);
        }
        else if (Interval.HasValue)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Delay(Interval.Value, cancellationToken: cancellationToken);
                await FireTriggerAsync(new TriggerContext { TriggerId = Id }, cancellationToken);
            }
        }
    }
}
```

## Conditions framework

Conditions evaluate whether automations should proceed, supporting complex logical operations:

```csharp
public abstract class AutomationCondition
{
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;
    
    public abstract UniTask<bool> EvaluateAsync(
        AutomationContext context, 
        CancellationToken cancellationToken = default);
}

public class StateCondition : AutomationCondition
{
    [JsonProperty("entity_id")]
    public string EntityId { get; set; }
    
    [JsonProperty("state")]
    public string[] States { get; set; }
    
    public override UniTask<bool> EvaluateAsync(
        AutomationContext context, 
        CancellationToken cancellationToken = default)
    {
        if (!Enabled) return UniTask.FromResult(true);
        
        var currentState = context.StateRegistry.GetState(EntityId);
        if (currentState == null) return UniTask.FromResult(false);
        
        return UniTask.FromResult(States.Contains(currentState.State));
    }
}

public class OrCondition : AutomationCondition
{
    [JsonProperty("conditions")]
    public List<AutomationCondition> Conditions { get; set; }
    
    public override async UniTask<bool> EvaluateAsync(
        AutomationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled || Conditions == null || Conditions.Count == 0)
            return true;
        
        var tasks = Conditions.Select(c => c.EvaluateAsync(context, cancellationToken));
        var results = await UniTask.WhenAll(tasks);
        
        return results.Any(r => r);
    }
}

public class TemplateCondition : AutomationCondition
{
    [JsonProperty("value_template")]
    public string Template { get; set; }
    
    private readonly ITemplateEngine _templateEngine;
    
    public TemplateCondition(ITemplateEngine templateEngine)
    {
        _templateEngine = templateEngine;
    }
    
    public override async UniTask<bool> EvaluateAsync(
        AutomationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _templateEngine.EvaluateAsync(Template, context, cancellationToken);
        return bool.Parse(result.ToString());
    }
}
```

## Actions execution system

Actions perform the actual work when automations trigger and conditions pass. The system supports sequential and parallel execution patterns:

```csharp
public abstract class AutomationAction
{
    [JsonProperty("continue_on_error")]
    public bool ContinueOnError { get; set; } = false;
    
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;
    
    public abstract UniTask ExecuteAsync(
        AutomationContext context,
        CancellationToken cancellationToken = default);
}

public class ServiceCallAction : AutomationAction
{
    [JsonProperty("service")]
    public string Service { get; set; }
    
    [JsonProperty("target")]
    public ActionTarget Target { get; set; }
    
    [JsonProperty("data")]
    public Dictionary<string, object> Data { get; set; }
    
    public override async UniTask ExecuteAsync(
        AutomationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        
        try
        {
            var service = context.ServiceRegistry.GetService(Service);
            await service.CallAsync(Target, Data, context, cancellationToken);
        }
        catch (Exception ex) when (ContinueOnError)
        {
            UnityEngine.Debug.LogError($"Action error (continuing): {ex}");
        }
    }
}

public class ParallelAction : AutomationAction
{
    [JsonProperty("actions")]
    public List<AutomationAction> Actions { get; set; }
    
    public override async UniTask ExecuteAsync(
        AutomationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled || Actions == null) return;
        
        var tasks = Actions
            .Where(a => a.Enabled)
            .Select(a => ExecuteWithErrorHandling(a, context, cancellationToken));
            
        await UniTask.WhenAll(tasks);
    }
    
    private async UniTask ExecuteWithErrorHandling(
        AutomationAction action, 
        AutomationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await action.ExecuteAsync(context, cancellationToken);
        }
        catch (Exception ex) when (action.ContinueOnError)
        {
            UnityEngine.Debug.LogError($"Parallel action error: {ex}");
        }
    }
}

public class ChooseAction : AutomationAction
{
    [JsonProperty("choices")]
    public List<Choice> Choices { get; set; }
    
    [JsonProperty("default")]
    public List<AutomationAction> DefaultActions { get; set; }
    
    public class Choice
    {
        [JsonProperty("conditions")]
        public List<AutomationCondition> Conditions { get; set; }
        
        [JsonProperty("sequence")]
        public List<AutomationAction> Actions { get; set; }
    }
    
    public override async UniTask ExecuteAsync(
        AutomationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        
        foreach (var choice in Choices)
        {
            bool allConditionsMet = true;
            
            foreach (var condition in choice.Conditions)
            {
                if (!await condition.EvaluateAsync(context, cancellationToken))
                {
                    allConditionsMet = false;
                    break;
                }
            }
            
            if (allConditionsMet)
            {
                foreach (var action in choice.Actions)
                {
                    await action.ExecuteAsync(context, cancellationToken);
                }
                return;
            }
        }
        
        // Execute default if no choices matched
        if (DefaultActions != null)
        {
            foreach (var action in DefaultActions)
            {
                await action.ExecuteAsync(context, cancellationToken);
            }
        }
    }
}
```

## Automation engine and lifecycle management

The automation engine orchestrates trigger evaluation, condition checking, and action execution with proper lifecycle management:

```csharp
public class Automation
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("alias")]
    public string Name { get; set; }
    
    [JsonProperty("mode")]
    public AutomationMode Mode { get; set; } = AutomationMode.Single;
    
    [JsonProperty("max")]
    public int MaxRuns { get; set; } = 10;
    
    [JsonProperty("triggers")]
    public List<AutomationTrigger> Triggers { get; set; }
    
    [JsonProperty("conditions")]
    public List<AutomationCondition> Conditions { get; set; }
    
    [JsonProperty("actions")]
    public List<AutomationAction> Actions { get; set; }
    
    private readonly SemaphoreSlim _executionSemaphore;
    private readonly Queue<TriggerContext> _executionQueue;
    private CancellationTokenSource _currentExecutionCts;
    private int _currentRunCount = 0;
    
    public Automation()
    {
        _executionSemaphore = new SemaphoreSlim(1, 1);
        _executionQueue = new Queue<TriggerContext>();
    }
    
    public async UniTask ExecuteAsync(
        TriggerContext triggerContext,
        AutomationContext automationContext,
        CancellationToken cancellationToken = default)
    {
        switch (Mode)
        {
            case AutomationMode.Single:
                await ExecuteSingleMode(triggerContext, automationContext, cancellationToken);
                break;
                
            case AutomationMode.Restart:
                await ExecuteRestartMode(triggerContext, automationContext, cancellationToken);
                break;
                
            case AutomationMode.Queued:
                await ExecuteQueuedMode(triggerContext, automationContext, cancellationToken);
                break;
                
            case AutomationMode.Parallel:
                await ExecuteParallelMode(triggerContext, automationContext, cancellationToken);
                break;
        }
    }
    
    private async UniTask ExecuteSingleMode(
        TriggerContext triggerContext,
        AutomationContext context,
        CancellationToken cancellationToken)
    {
        if (_currentRunCount > 0)
            return;
            
        await _executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            _currentRunCount++;
            await RunAutomationAsync(triggerContext, context, cancellationToken);
        }
        finally
        {
            _currentRunCount--;
            _executionSemaphore.Release();
        }
    }
    
    private async UniTask ExecuteRestartMode(
        TriggerContext triggerContext,
        AutomationContext context,
        CancellationToken cancellationToken)
    {
        _currentExecutionCts?.Cancel();
        _currentExecutionCts = new CancellationTokenSource();
        
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _currentExecutionCts.Token);
            
        await _executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            await RunAutomationAsync(triggerContext, context, linkedCts.Token);
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }
    
    private async UniTask RunAutomationAsync(
        TriggerContext triggerContext,
        AutomationContext context,
        CancellationToken cancellationToken)
    {
        // Set trigger variables
        context.Variables = new Dictionary<string, object>(context.Variables);
        foreach (var kvp in triggerContext.Variables)
        {
            context.Variables[kvp.Key] = kvp.Value;
        }
        
        // Evaluate conditions
        foreach (var condition in Conditions)
        {
            if (!await condition.EvaluateAsync(context, cancellationToken))
            {
                return; // Condition not met, skip execution
            }
        }
        
        // Execute actions
        foreach (var action in Actions)
        {
            try
            {
                await action.ExecuteAsync(context, cancellationToken);
            }
            catch (Exception ex) when (!action.ContinueOnError)
            {
                UnityEngine.Debug.LogError($"Automation {Id} failed: {ex}");
                throw;
            }
        }
    }
}

public enum AutomationMode
{
    Single,   // Only one instance can run at a time
    Restart,  // Cancel current run and start new one
    Queued,   // Queue new runs to execute sequentially
    Parallel  // Allow multiple instances to run simultaneously
}
```

## JSON serialization with Newtonsoft.Json

The system uses custom converters for polymorphic serialization of triggers, conditions, and actions:

```csharp
public class TriggerJsonConverter : JsonConverter<AutomationTrigger>
{
    public override void WriteJson(JsonWriter writer, AutomationTrigger value, JsonSerializer serializer)
    {
        var jo = JObject.FromObject(value);
        jo.AddFirst(new JProperty("trigger", value.GetType().Name.Replace("Trigger", "").ToLower()));
        jo.WriteTo(writer);
    }
    
    public override AutomationTrigger ReadJson(
        JsonReader reader,
        Type objectType,
        AutomationTrigger existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var jo = JObject.Load(reader);
        var triggerType = jo["trigger"]?.Value<string>();
        
        AutomationTrigger trigger = triggerType switch
        {
            "state" => new StateTrigger(),
            "time" => new TimeTrigger(),
            "event" => new EventTrigger(),
            "template" => new TemplateTrigger(),
            _ => throw new JsonException($"Unknown trigger type: {triggerType}")
        };
        
        serializer.Populate(jo.CreateReader(), trigger);
        return trigger;
    }
}

// Example JSON configuration
public class AutomationConfiguration
{
    public static Automation LoadFromJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new TriggerJsonConverter(),
                new ConditionJsonConverter(),
                new ActionJsonConverter()
            },
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include
        };
        
        return JsonConvert.DeserializeObject<Automation>(json, settings);
    }
    
    public static string SaveToJson(Automation automation)
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new TriggerJsonConverter(),
                new ConditionJsonConverter(),
                new ActionJsonConverter()
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        
        return JsonConvert.SerializeObject(automation, settings);
    }
}
```

## Dependency injection with VContainer

The system uses dependency injection for clean service architecture without MonoBehaviours:

```csharp
public class AutomationLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Core services
        builder.Register<EventBus>(Lifetime.Singleton);
        builder.Register<StateRegistry>(Lifetime.Singleton);
        builder.Register<AutomationEngine>(Lifetime.Singleton);
        builder.Register<ServiceRegistry>(Lifetime.Singleton);
        
        // Template engine
        builder.Register<ITemplateEngine, ScribanTemplateEngine>(Lifetime.Singleton);
        
        // Entry points
        builder.RegisterEntryPoint<AutomationController>();
        builder.RegisterEntryPoint<EventForwarder>();
    }
}

public class AutomationController : IAsyncStartable, IDisposable
{
    private readonly EventBus _eventBus;
    private readonly StateRegistry _stateRegistry;
    private readonly AutomationEngine _engine;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    public AutomationController(
        EventBus eventBus,
        StateRegistry stateRegistry,
        AutomationEngine engine)
    {
        _eventBus = eventBus;
        _stateRegistry = stateRegistry;
        _engine = engine;
    }
    
    public async UniTask StartAsync(CancellationToken cancellationToken)
    {
        // Load automations from configuration
        var automations = await LoadAutomationsAsync(cancellationToken);
        
        foreach (var automation in automations)
        {
            _engine.RegisterAutomation(automation);
        }
        
        // Start the engine
        await _engine.StartAsync(cancellationToken);
        
        // Start update loop
        await RunUpdateLoopAsync(CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token);
    }
    
    private async UniTask RunUpdateLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in UniTaskAsyncEnumerable.EveryUpdate()
            .WithCancellation(cancellationToken))
        {
            // Process any pending events or state updates
            await _engine.ProcessFrameAsync(cancellationToken);
        }
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _engine.Dispose();
    }
}
```

## Unity integration without MonoBehaviours

The system integrates with Unity's lifecycle using the PlayerLoop API and static initialization:

```csharp
public static class AutomationBootstrapper
{
    private static AutomationEngine _engine;
    private static EventBus _eventBus;
    private static StateRegistry _stateRegistry;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _engine?.Dispose();
        _engine = null;
        _eventBus = null;
        _stateRegistry = null;
    }
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        // Create core services
        _eventBus = new EventBus();
        _stateRegistry = new StateRegistry(_eventBus);
        _engine = new AutomationEngine(_eventBus, _stateRegistry);
        
        // Hook into Unity events
        Application.quitting += OnApplicationQuitting;
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Insert into PlayerLoop
        var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
        var updateSystem = new PlayerLoopSystem
        {
            type = typeof(AutomationBootstrapper),
            updateDelegate = UpdateAutomations
        };
        
        playerLoop = InsertSystem<Update>(playerLoop, updateSystem);
        PlayerLoop.SetPlayerLoop(playerLoop);
        
        // Start the engine
        _engine.StartAsync(CancellationToken.None).Forget();
    }
    
    static void UpdateAutomations()
    {
        _engine?.ProcessFrameAsync(CancellationToken.None).Forget();
    }
    
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Publish scene change event
        _eventBus?.PublishAsync(new SceneChangedEvent 
        { 
            SceneName = scene.name,
            LoadMode = mode 
        }, CancellationToken.None).Forget();
    }
    
    static void OnApplicationQuitting()
    {
        _engine?.Dispose();
    }
}
```

## Best practices and performance considerations

**Memory management** requires careful attention in Unity's real-time environment. The system uses object pooling for frequently allocated objects like `CancellationTokenSource` and event data structures. UniTask's zero-allocation async operations eliminate GC pressure from async code. All event subscriptions must be properly disposed to prevent memory leaks, using weak references where appropriate.

**Thread safety** is crucial since Unity APIs must run on the main thread. The event bus uses locks for subscription management but executes handlers without locks to avoid deadlocks. Background operations use `UniTask.SwitchToThreadPool()` for CPU-intensive work, always returning to the main thread for Unity API calls.

**Performance optimization** includes batching state changes to reduce event frequency, using struct-based event data to minimize heap allocations, and implementing lazy evaluation for expensive condition checks. The automation engine processes events in chunks to maintain consistent frame rates.

**Error handling** follows a defensive programming approach with try-catch blocks around all event handlers and actions. The system implements configurable retry policies with exponential backoff for network operations and comprehensive logging for debugging automation execution flows.

## Conclusion

This automation runner system brings Home Assistant's powerful automation patterns to Unity while respecting the engine's unique constraints. By leveraging UniTask for efficient async operations, Newtonsoft.Json for flexible configuration, and pure C# architecture with dependency injection, the system provides a robust foundation for complex automation scenarios without the limitations of MonoBehaviours.

The event-driven architecture ensures loose coupling between components, while the trigger-condition-action pattern enables expressive automation rules. With proper implementation of the patterns outlined in this guide, developers can create sophisticated automation systems that are maintainable, testable, and performant within Unity's real-time environment.