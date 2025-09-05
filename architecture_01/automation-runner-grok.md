## Automation Runner Implementation

This implementation provides a basic automation runner in C# for Unity, inspired by Home Assistant's event-trigger-condition-action model. It parses JSON files into automation rules, handles event-based triggers (with optional data matching), evaluates conditions (supporting nested `and`/`or` and simple data matching), and executes actions via a registry system. Events can be triggered externally (e.g., from Quest VR inputs using Oculus Integration, by calling `EventDispatcher.TriggerEvent`).

Key design choices:
- **JSON Structure**: Automations are lists with triggers (event-based), conditions (nested/polymorphic), and actions (type-based with data).
- **Async Handling**: Uses UniTask for action execution.
- **Event System**: Simple dictionary-based dispatcher.
- **Condition Polymorphism**: Uses Newtonsoft.Json converter for deserialization based on `type`.
- **Action Registry**: Register custom action handlers by type (e.g., for Unity-specific actions like scene changes or VR interactions).
- **No Unity Components**: Plain C# classes; assumes external update loop if needed, and external event triggering (e.g., from a MonoBehaviour wrapper).
- **Limitations**: Basic condition types (extendable); assumes synchronous condition evaluation; only event triggers (no time-based); no built-in state management (use Context for extensions).

Assume packages are added to Unity:
- Newtonsoft.Json (via Package Manager or asset).
- UniTask (via Git: https://github.com/Cysharp/UniTask).

### Supporting Classes

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class EventData
{
    public Dictionary<string, object> Data { get; set; } = new();
}

public class Context
{
    public EventData TriggerData { get; set; }
    // Extend with states, variables, etc., as needed
}

public class Trigger
{
    public string Type { get; set; } // e.g., "event"
    public string EventName { get; set; }
    public Dictionary<string, object> MatchData { get; set; }
}

public abstract class Condition
{
    public abstract bool Evaluate(Context context);
}

public class AndCondition : Condition
{
    public List<Condition> Conditions { get; set; }
    public override bool Evaluate(Context context) => Conditions.All(c => c.Evaluate(context));
}

public class OrCondition : Condition
{
    public List<Condition> Conditions { get; set; }
    public override bool Evaluate(Context context) => Conditions.Any(c => c.Evaluate(context));
}

public class MatchDataCondition : Condition
{
    public string Key { get; set; }
    public object Value { get; set; }
    public override bool Evaluate(Context context)
    {
        return context.TriggerData.Data.TryGetValue(Key, out var actual) && actual.Equals(Value);
    }
}

public class ConditionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(Condition);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);
        string type = obj["type"]?.Value<string>();
        Condition condition = type switch
        {
            "and" => new AndCondition(),
            "or" => new OrCondition(),
            "match_data" => new MatchDataCondition(),
            _ => throw new NotImplementedException($"Unknown condition type: {type}")
        };
        serializer.Populate(obj.CreateReader(), condition);
        return condition;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class Action
{
    public string Type { get; set; }
    public Dictionary<string, object> Data { get; set; }
}

public class Automation
{
    public string Id { get; set; }
    public List<Trigger> Triggers { get; set; }
    public List<Condition> Conditions { get; set; }
    public List<Action> Actions { get; set; }
}

public class AutomationConfig
{
    public List<Automation> Automations { get; set; }
}
```

### Event Dispatcher

```csharp
public class EventDispatcher
{
    private readonly Dictionary<string, List<Action<EventData>>> listeners = new();

    public void Subscribe(string eventName, Action<EventData> handler)
    {
        if (!listeners.TryGetValue(eventName, out var list))
        {
            list = new List<Action<EventData>>();
            listeners[eventName] = list;
        }
        list.Add(handler);
    }

    public void Unsubscribe(string eventName, Action<EventData> handler)
    {
        if (listeners.TryGetValue(eventName, out var list))
        {
            list.Remove(handler);
        }
    }

    public void TriggerEvent(string eventName, EventData data)
    {
        if (listeners.TryGetValue(eventName, out var list))
        {
            foreach (var handler in list.ToArray())
            {
                handler(data);
            }
        }
    }
}
```

### Automation Runner

```csharp
public class AutomationRunner
{
    private readonly List<Automation> automations = new();
    private readonly EventDispatcher eventDispatcher = new();
    private readonly Dictionary<string, List<Action<EventData>>> subscribedHandlers = new(); // For cleanup
    private readonly Dictionary<string, Func<Dictionary<string, object>, Context, UniTask>> actionHandlers = new();

    public EventDispatcher EventDispatcher => eventDispatcher; // Expose for external event triggering (e.g., from Quest inputs)

    public void RegisterAction(string type, Func<Dictionary<string, object>, Context, UniTask> handler)
    {
        actionHandlers[type] = handler;
    }

    public void LoadFromJson(string json)
    {
        var settings = new JsonSerializerSettings { Converters = { new ConditionConverter() } };
        var config = JsonConvert.DeserializeObject<AutomationConfig>(json, settings);
        automations.AddRange(config.Automations);

        foreach (var automation in config.Automations)
        {
            foreach (var trigger in automation.Triggers)
            {
                if (trigger.Type == "event")
                {
                    Action<EventData> handler = data => HandleTriggerAsync(automation, data).Forget();
                    eventDispatcher.Subscribe(trigger.EventName, handler);

                    if (!subscribedHandlers.TryGetValue(trigger.EventName, out var list))
                    {
                        list = new List<Action<EventData>>();
                        subscribedHandlers[trigger.EventName] = list;
                    }
                    list.Add(handler);
                }
            }
        }
    }

    private async UniTask HandleTriggerAsync(Automation automation, EventData triggerData)
    {
        if (automation.Triggers.Any(t => !MatchesTriggerData(t, triggerData))) return;

        var context = new Context { TriggerData = triggerData };
        bool conditionsMet = automation.Conditions == null || automation.Conditions.All(c => c.Evaluate(context));
        if (!conditionsMet) return;

        foreach (var action in automation.Actions)
        {
            if (actionHandlers.TryGetValue(action.Type, out var handler))
            {
                await handler(action.Data, context);
            }
        }
    }

    private bool MatchesTriggerData(Trigger trigger, EventData triggerData)
    {
        if (trigger.MatchData == null) return true;
        return trigger.MatchData.All(kv => triggerData.Data.TryGetValue(kv.Key, out var val) && val.Equals(kv.Value));
    }

    // Optional: Call this if unloading automations
    public void Unload()
    {
        foreach (var kv in subscribedHandlers)
        {
            foreach (var handler in kv.Value)
            {
                eventDispatcher.Unsubscribe(kv.Key, handler);
            }
        }
        automations.Clear();
        subscribedHandlers.Clear();
    }
}
```

### Example Usage

```csharp
// In your Unity entry point (e.g., a MonoBehaviour's Start, avoiding direct use in runner)
var runner = new AutomationRunner();

// Register custom actions (e.g., Unity/VR specific)
runner.RegisterAction("log", (data, context) =>
{
    UnityEngine.Debug.Log(data["message"]);
    return UniTask.CompletedTask;
});

runner.RegisterAction("vr_action", (data, context) =>
{
    // Example: Trigger VR haptic or something; use Oculus API here
    return UniTask.CompletedTask;
});

// Load JSON (e.g., from file or LLM output)
string json = @"
{
    ""automations"": [
        {
            ""id"": ""auto1"",
            ""triggers"": [
                {
                    ""type"": ""event"",
                    ""event_name"": ""button_pressed"",
                    ""match_data"": { ""button"": ""A"" }
                }
            ],
            ""conditions"": [
                {
                    ""type"": ""and"",
                    ""conditions"": [
                        { ""type"": ""match_data"", ""key"": ""state"", ""value"": ""active"" }
                    ]
                }
            ],
            ""actions"": [
                { ""type"": ""log"", ""data"": { ""message"": ""Button A pressed!"" } }
            ]
        }
    ]
}";
runner.LoadFromJson(json);

// Trigger event from Quest input (e.g., in Update or input handler)
var data = new EventData { Data = { { "button", "A" }, { "state", "active" } } };
runner.EventDispatcher.TriggerEvent("button_pressed", data);
```

### Example JSON Structure

```json
{
  "automations": [
    {
      "id": "auto1",
      "triggers": [
        {"type": "event", "event_name": "button_pressed", "match_data": {"button": "A"}}
      ],
      "conditions": [
        {"type": "or", "conditions": [
          {"type": "match_data", "key": "state", "value": "active"},
          {"type": "match_data", "key": "state", "value": "idle"}
        ]}
      ],
      "actions": [
        {"type": "log", "data": {"message": "Triggered!"}},
        {"type": "vr_action", "data": {"intensity": 0.5}}
      ]
    }
  ]
}
```

Extend by adding more condition types to `ConditionConverter`, registering more actions, or adding state to `Context` for complex evaluations. For Quest inputs, integrate with `OVRInput` in a separate class to call `TriggerEvent`.