using UnityEngine;
using Inventonater.Rules.Engine;

namespace Inventonater.Rules.Desktop
{
    public class DesktopInput : MonoBehaviour
    {
        public EventBus Bus;
        public EntityStore Store;
        private Vector3 _lastMouse;
        private float _lastTime;

        void Start()
        {
            _lastMouse = Input.mousePosition;
            _lastTime = Time.time;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Bus.Emit(new EventData("mouse.left.down"));
                Store.Set("mouse.left.button", "down");
            }
            if (Input.GetMouseButtonUp(0))
            {
                Bus.Emit(new EventData("mouse.left.up"));
                Store.Set("mouse.left.button", "up");
            }
            if (Input.GetKeyDown(KeyCode.Space))
                Bus.Emit(new EventData("key.space.down"));
            if (Input.GetKeyDown(KeyCode.F1))
                Bus.Emit(new EventData("key.f1.down"));

            // mouse speed entity (pixels per second)
            var now = Time.time;
            var delta = (Input.mousePosition - _lastMouse).magnitude;
            var dt = now - _lastTime;
            if (dt > 0)
            {
                var speed = delta / dt;
                Store.Set("mouse.speed", speed);
            }
            _lastMouse = Input.mousePosition;
            _lastTime = now;
        }
    }
}
