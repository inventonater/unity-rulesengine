using System.Threading;
using System.Threading.Channels;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Inventonater.Rules
{
    public struct Event
    {
        public string Name { get; }
        public float Timestamp { get; }
        public object Data { get; }

        public Event(string name, object data = null)
        {
            Name = name;
            Timestamp = Time.time;
            Data = data;
        }
    }

    public static class EventBus
    {
        private static readonly Channel<Event> _channel = Channel.CreateUnbounded<Event>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        public static void Publish(string name, object data = null)
        {
            var evt = new Event(name, data);
            if (!_channel.Writer.TryWrite(evt))
            {
                Debug.LogWarning($"Failed to publish event: {name}");
            }
            else
            {
                Debug.Log($"[EventBus] Published: {name} at {evt.Timestamp:F2}");
            }
        }

        public static async UniTask<Event> GetNextAsync(CancellationToken ct = default)
        {
            try
            {
                var evt = await _channel.Reader.ReadAsync(ct);
                return evt;
            }
            catch (ChannelClosedException)
            {
                return default;
            }
        }

        public static IUniTaskAsyncEnumerable<Event> GetStream(CancellationToken ct = default)
        {
            return _channel.Reader.ReadAllAsync(ct).ToUniTaskAsyncEnumerable();
        }

        public static void Clear()
        {
            while (_channel.Reader.TryRead(out _)) { }
        }
    }
}