using System.Collections.Generic;
using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Simple key-value store for entity states and numeric values
    /// </summary>
    public class EntityStore : MonoBehaviour
    {
        private readonly Dictionary<string, string> _stringValues = new Dictionary<string, string>();
        private readonly Dictionary<string, double> _numericValues = new Dictionary<string, double>();
        private readonly Dictionary<string, float> _lastChangeTime = new Dictionary<string, float>();
        
        public void SetState(string key, string value)
        {
            _stringValues[key] = value;
            _lastChangeTime[key] = Time.time;
            
            // Publish state change event
            EventBus.Publish($"state_changed:{key}", new Dictionary<string, object>
            {
                { "key", key },
                { "value", value },
                { "type", "string" }
            });
            
            Debug.Log($"[EntityStore] Set state {key} = {value}");
        }
        
        public string GetState(string key, string defaultValue = "")
        {
            return _stringValues.TryGetValue(key, out var value) ? value : defaultValue;
        }
        
        public void SetNumeric(string key, double value)
        {
            var oldValue = GetNumeric(key, double.MinValue);
            _numericValues[key] = value;
            _lastChangeTime[key] = Time.time;
            
            // Publish numeric change event
            EventBus.Publish($"numeric_changed:{key}", new Dictionary<string, object>
            {
                { "key", key },
                { "value", value },
                { "oldValue", oldValue },
                { "type", "numeric" }
            });
            
            // Check thresholds
            if (oldValue != double.MinValue)
            {
                // Crossing above threshold
                if (oldValue <= 0 && value > 0)
                {
                    EventBus.Publish($"threshold_crossed:{key}:above:0");
                }
                // Crossing below threshold
                if (oldValue > 0 && value <= 0)
                {
                    EventBus.Publish($"threshold_crossed:{key}:below:0");
                }
            }
            
            Debug.Log($"[EntityStore] Set numeric {key} = {value}");
        }
        
        public double GetNumeric(string key, double defaultValue = 0)
        {
            return _numericValues.TryGetValue(key, out var value) ? value : defaultValue;
        }
        
        public float GetLastChangeTime(string key)
        {
            return _lastChangeTime.TryGetValue(key, out var time) ? time : 0f;
        }
        
        public bool HasChanged(string key, float sinceTime)
        {
            return GetLastChangeTime(key) > sinceTime;
        }
        
        public void Clear()
        {
            _stringValues.Clear();
            _numericValues.Clear();
            _lastChangeTime.Clear();
        }
        
        /// <summary>
        /// Check if a numeric value has been above/below a threshold for a duration
        /// </summary>
        public bool IsThresholdMet(string key, double threshold, bool checkAbove, float forSeconds)
        {
            var currentValue = GetNumeric(key);
            var meetsThreshold = checkAbove ? currentValue > threshold : currentValue < threshold;
            
            if (!meetsThreshold) return false;
            
            if (forSeconds <= 0) return true;
            
            var lastChange = GetLastChangeTime(key);
            return Time.time - lastChange >= forSeconds;
        }
    }
}
