using System.Collections.Generic;
using System.Linq;

namespace Inventonater.Rules
{
    public sealed class PatternSequenceWatcher
    {
        private readonly string[] seq;
        private readonly float within;
        private int i = 0;
        private float windowStart = -1f;

        public PatternSequenceWatcher(IEnumerable<string> names, int withinMs)
        {
            seq = names.ToArray();
            within = withinMs / 1000f;
        }

        // Return true when sequence completes
        public bool OnEvent(string name, float now)
        {
            // If we haven't started the sequence
            if (i == 0)
            {
                if (name == seq[0])
                {
                    i = 1;
                    windowStart = now;
                }
                return false;
            }
            
            // Check if we've exceeded the time window
            if (now - windowStart > within)
            {
                // Reset and check if this event starts a new sequence
                i = 0;
                windowStart = -1f;
                
                if (name == seq[0])
                {
                    i = 1;
                    windowStart = now;
                }
                return false;
            }
            
            // Check if this is the next expected event
            if (name == seq[i])
            {
                i++;
                if (i >= seq.Length)
                {
                    // Sequence complete!
                    i = 0;
                    windowStart = -1f;
                    return true;
                }
                return false;
            }
            
            // Wrong event - check if it's the start of a new sequence
            if (name == seq[0])
            {
                i = 1;
                windowStart = now;
            }
            else
            {
                i = 0;
                windowStart = -1f;
            }
            
            return false;
        }

        public void Reset()
        {
            i = 0;
            windowStart = -1f;
        }
    }
}