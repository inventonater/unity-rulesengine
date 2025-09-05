using UnityEngine;

namespace Inventonater.Rules
{
    /// <summary>
    /// Desktop input provider - publishes mouse/keyboard events and tracks entities
    /// </summary>
    public class DesktopInput : MonoBehaviour
    {
        private EntityStore _store;
        
        private bool _leftMouseDown;
        private float _leftMouseDownTime;
        private Vector3 _lastMousePosition;
        private float _mouseSpeed;
        
        private void Awake()
        {
            _store = GetComponent<EntityStore>();
            if (_store == null)
            {
                _store = gameObject.AddComponent<EntityStore>();
            }
        }
        
        private void Start()
        {
            // Initialize entity states
            _store.SetNumeric("sensor.mouse_button_left", 0);
            _store.SetNumeric("sensor.mouse_speed", 0);
            _store.SetState("ui.mode", "normal");
        }
        
        private void Update()
        {
            ProcessMouseInput();
            ProcessKeyboardInput();
            UpdateMouseSpeed();
        }
        
        private void ProcessMouseInput()
        {
            // Left mouse button down
            if (Input.GetMouseButtonDown(0))
            {
                _leftMouseDown = true;
                _leftMouseDownTime = Time.time;
                _store.SetNumeric("sensor.mouse_button_left", 1);
                EventBus.Publish("mouse.left.down");
                Debug.Log("[DesktopInput] mouse.left.down");
            }
            
            // Left mouse button up
            if (Input.GetMouseButtonUp(0))
            {
                _leftMouseDown = false;
                _store.SetNumeric("sensor.mouse_button_left", 0);
                EventBus.Publish("mouse.left.up");
                Debug.Log("[DesktopInput] mouse.left.up");
            }
            
            // Right mouse button
            if (Input.GetMouseButtonDown(1))
            {
                EventBus.Publish("mouse.right.down");
                Debug.Log("[DesktopInput] mouse.right.down");
            }
            
            if (Input.GetMouseButtonUp(1))
            {
                EventBus.Publish("mouse.right.up");
                Debug.Log("[DesktopInput] mouse.right.up");
            }
        }
        
        private void ProcessKeyboardInput()
        {
            // Space key
            if (Input.GetKeyDown(KeyCode.Space))
            {
                EventBus.Publish("key.space.down");
                Debug.Log("[DesktopInput] key.space.down");
            }
            
            if (Input.GetKeyUp(KeyCode.Space))
            {
                EventBus.Publish("key.space.up");
            }
            
            // Arrow keys for Konami code
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                EventBus.Publish("key.arrow_up.down");
                Debug.Log("[DesktopInput] key.arrow_up.down");
            }
            
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                EventBus.Publish("key.arrow_down.down");
                Debug.Log("[DesktopInput] key.arrow_down.down");
            }
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                EventBus.Publish("key.arrow_left.down");
                Debug.Log("[DesktopInput] key.arrow_left.down");
            }
            
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                EventBus.Publish("key.arrow_right.down");
                Debug.Log("[DesktopInput] key.arrow_right.down");
            }
            
            // A and B keys for Konami code
            if (Input.GetKeyDown(KeyCode.A))
            {
                EventBus.Publish("key.a.down");
                Debug.Log("[DesktopInput] key.a.down");
            }
            
            if (Input.GetKeyDown(KeyCode.B))
            {
                EventBus.Publish("key.b.down");
                Debug.Log("[DesktopInput] key.b.down");
            }
            
            // F1 key for debug mode toggle
            if (Input.GetKeyDown(KeyCode.F1))
            {
                var currentMode = _store.GetState("ui.mode", "normal");
                var newMode = currentMode == "debug" ? "normal" : "debug";
                _store.SetState("ui.mode", newMode);
                EventBus.Publish("key.f1.down");
                Debug.Log($"[DesktopInput] F1 pressed, ui.mode = {newMode}");
            }
        }
        
        private void UpdateMouseSpeed()
        {
            var currentMousePosition = Input.mousePosition;
            var deltaPosition = currentMousePosition - _lastMousePosition;
            
            // Calculate speed in pixels per second
            _mouseSpeed = deltaPosition.magnitude / Time.deltaTime;
            
            // Update entity store
            _store.SetNumeric("sensor.mouse_speed", _mouseSpeed);
            
            _lastMousePosition = currentMousePosition;
        }
    }
}
