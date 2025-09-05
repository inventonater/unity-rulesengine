using System.Collections.Generic;
using System.Linq;

namespace Inventonater.Rules
{
    /// <summary>
    /// Tracks ordered event sequences within a time window
    /// </summary>
    public sealed class PatternSequenceWatcher
    {
        private readonly string[] _sequence;
        private readonly float _withinSeconds;
        private int _currentIndex = 0;
        private float _windowStart = -1f;
        
        public PatternSequenceWatcher(IEnumerable<string> names, int withinMs)
        {
            _sequence = names?.ToArray() ?? new string[0];
            _withinSeconds = withinMs / 1000f;
        }
        
        /// <summary>
        /// Process an event and return true if the sequence completes
        /// </summary>
        public bool OnEvent(string name, float now)
        {
            if (_sequence.Length == 0) return false;
            
            // Starting fresh
            if (_currentIndex == 0)
            {
                if (name == _sequence[0])
                {
                    _currentIndex = 1;
                    _windowStart = now;
                }
                return false;
            }
            
            // Check if we're still within the time window
            if (now - _windowStart > _withinSeconds)
            {
                // Window expired, reset
                _currentIndex = 0;
                _windowStart = -1f;
                
                // Check if this event starts a new sequence
                if (name == _sequence[0])
                {
                    _currentIndex = 1;
                    _windowStart = now;
                }
                return false;
            }
            
            // Check if this is the next expected event
            if (name == _sequence[_currentIndex])
            {
                _currentIndex++;
                
                // Check if sequence is complete
                if (_currentIndex >= _sequence.Length)
                {
                    // Reset for next sequence
                    _currentIndex = 0;
                    _windowStart = -1f;
                    return true;
                }
                return false;
            }
            
            // Wrong event - check if it's a restart
            _currentIndex = (name == _sequence[0]) ? 1 : 0;
            if (_currentIndex == 1 && _windowStart < 0)
            {
                _windowStart = now;
            }
            
            return false;
        }
        
        public void Reset()
        {
            _currentIndex = 0;
            _windowStart = -1f;
        }
    }
}
