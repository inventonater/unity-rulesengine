using Inventonater.RulesEngine.Engine;

namespace Inventonater.RulesEngine.Desktop
{
    /// <summary>
    /// Placeholder desktop input adapter. In a real Unity project this would listen
    /// to mouse/keyboard and publish events to the EventBus.
    /// </summary>
    public class DesktopInput
    {
        private readonly EventBus _bus;
        public DesktopInput(EventBus bus) { _bus = bus; }
        public void Simulate(string eventName) => _bus.Publish(eventName);
    }
}
