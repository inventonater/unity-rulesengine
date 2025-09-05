using Inventonater.Rules.Engine;

namespace Inventonater.Rules.Desktop
{
    /// <summary>
    /// Placeholder desktop input adapter. In Unity this would hook into Input; here we expose a public
    /// method so tests or samples can emit input events manually.
    /// </summary>
    public class DesktopInput
    {
        private readonly EventBus _bus;
        public DesktopInput(EventBus bus) => _bus = bus;
        public void Emit(string name) => _bus.Emit(name);
    }
}
