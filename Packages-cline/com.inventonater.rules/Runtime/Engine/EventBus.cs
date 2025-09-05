using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public struct EventData
    {
        public string Name { get; }
        public float Timestamp { get; }
        public Dictionary<string, object> Payload { get; }
        
        public EventData(string name, float timestamp = -1f, Dictionary<string, object> payload = null)
        {
            Name = name;
            Timestamp = timestamp >= 0 ? timestamp : Time.time;
            Payload = payload;
        }
    }
    
    /// <summary>
    /// Simple event bus using channels for async event streaming
    /// </summary>
    public static class EventBus
    {
        private static Channel<EventData> _channel;
        private static ChannelWriter<EventData> _writer;
        private static ChannelReader<EventData> _reader;
        private static bool _initialized;
        
        static EventBus()
        {
            Initialize();
        }
        
        private static void Initialize()
        {
            if (_initialized) return;
            
            _channel = Channel.CreateUnbounded<EventData>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
            
            _writer = _channel.Writer;
            _reader = _channel.Reader;
            _initialized = true;
        }
        
        public static void Publish(string eventName, Dictionary<string, object> payload = null)
        {
            if (!_initialized) Initialize();
            
            var data = new EventData(eventName, Time.time, payload);
            if (!_writer.TryWrite(data))
            {
                Debug.LogWarning($"Failed to publish event: {eventName}");
            }
            else
            {
                Debug.Log($"[EventBus] Published: {eventName} at {data.Timestamp:F2}");
            }
        }
        
        public static async IUniTaskAsyncEnumerable<EventData> GetStream(CancellationToken cancellationToken)
        {
            if (!_initialized) Initialize();
            
            await foreach (var data in _reader.ReadAllAsync(cancellationToken))
            {
                yield return data;
            }
        }
        
        public static void Reset()
        {
            if (_initialized)
            {
                _writer?.TryComplete();
                _initialized = false;
                Initialize();
            }
        }
    }
}
