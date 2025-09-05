using UnityEngine;

namespace Inventonater.Rules
{
    public class DesktopInput : MonoBehaviour
    {
        private EntityStore _store;
        private Vector3 _lastMousePosition;
        private float _mouseButtonDownTime;
        private bool _mouseButtonDown;

        private void Start()
        {
            _store = FindObjectOfType<EntityStore>();
            if (_store == null)
            {
                Debug.LogError("[DesktopInput] EntityStore not found!");
            }
            
            _lastMousePosition = Input.mousePosition;
        }

        private void Update()
        {
            // Mouse button events
            if (Input.GetMouseButtonDown(0))
            {
                EventBus.Publish("mouse.left.down");
                _mouseButtonDown = true;
                _mouseButtonDownTime = Time.time;
                
                if (_store != null)
                {
                    _store.SetNumeric("sensor.mouse_button_left", 1.0);
                }
            }
            
            if (Input.GetMouseButtonUp(0))
            {
                EventBus.Publish("mouse.left.up");
                _mouseButtonDown = false;
                
                if (_store != null)
                {
                    _store.SetNumeric("sensor.mouse_button_left", 0.0);
                }
            }
            
            // Mouse speed calculation
            Vector3 currentMousePos = Input.mousePosition;
            float mouseSpeed = (currentMousePos - _lastMousePosition).magnitude / Time.deltaTime;
            _lastMousePosition = currentMousePos;
            
            if (_store != null)
            {
                _store.SetNumeric("sensor.mouse_speed", mouseSpeed);
            }
            
            // Keyboard events
            if (Input.GetKeyDown(KeyCode.Space))
            {
                EventBus.Publish("key.space.down");
            }
            
            if (Input.GetKeyUp(KeyCode.Space))
            {
                EventBus.Publish("key.space.up");
            }
            
            // Arrow keys for Konami code
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                EventBus.Publish("key.arrow_up.down");
            }
            
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                EventBus.Publish("key.arrow_down.down");
            }
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                EventBus.Publish("key.arrow_left.down");
            }
            
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                EventBus.Publish("key.arrow_right.down");
            }
            
            // A and B keys for Konami code
            if (Input.GetKeyDown(KeyCode.A))
            {
                EventBus.Publish("key.a.down");
            }
            
            if (Input.GetKeyDown(KeyCode.B))
            {
                EventBus.Publish("key.b.down");
            }
            
            // F1 key for debug mode toggle
            if (Input.GetKeyDown(KeyCode.F1))
            {
                EventBus.Publish("key.f1.down");
                
                if (_store != null)
                {
                    string currentMode = _store.GetState("ui.mode");
                    string newMode = currentMode == "debug" ? "normal" : "debug";
                    _store.SetState("ui.mode", newMode);
                    Debug.Log($"[DesktopInput] UI mode toggled to: {newMode}");
                }
            }
            
            // Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EventBus.Publish("key.escape.down");
            }
            
            // Number keys
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    EventBus.Publish($"key.{i}.down");
                }
            }
        }
    }
}