namespace Inventonater.Rules.Authoring
{
    /// <summary>
    /// Normalizes legacy/alias trigger names into the canonical schema
    /// so the engine only deals with one set of names.
    /// </summary>
    public static class RuleCoercion
    {
        public static void Normalize(RuleDto rule)
        {
            if (rule?.triggers == null) return;
            foreach (var t in rule.triggers)
            {
                if (t.type == null) continue;
                switch (t.type)
                {
                    case "timer":
                        t.type = "time_schedule";
                        t.every_ms_10_to_600000 = t.every_ms_10_to_600000 ?? t.for_ms;
                        break;
                    case "value":
                        t.type = "numeric_threshold";
                        break;
                    case "pattern":
                        t.type = "pattern_sequence";
                        break;
                }
            }
        }
    }
}
