using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventonater.Rules
{
    public class DesktopInput : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private IEventBus _eventBus;
        private IEntityStore _store;
        
        private float _lastClickTime;
        private int _clickCount;
        private const float DoubleClickThreshold = 0.3f;
        
        private bool _isDragging;
        private float _dragStartTime;
        private Vector2 _dragStartPosition;
        private float _dragDistance;

        public void Initialize(IEventBus eventBus, IEntityStore store)
        {
            _eventBus = eventBus;
            _store = store;
        }

        private void Update()
        {
            HandleKeyboardInput();
            UpdateDragState();
        }

        private void HandleKeyboardInput()
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(key))
                    {
                        string keyName = key.ToString().ToLower();
                        _eventBus.Fire($"key_{keyName}");
                        _store.Set($"input.key.{keyName}", true);
                        
                        if (key == KeyCode.UpArrow) _eventBus.Fire("arrow_up");
                        else if (key == KeyCode.DownArrow) _eventBus.Fire("arrow_down");
                        else if (key == KeyCode.LeftArrow) _eventBus.Fire("arrow_left");
                        else if (key == KeyCode.RightArrow) _eventBus.Fire("arrow_right");
                    }
                }
            }
            
            if (Input.GetKeyUp(KeyCode.Space))
            {
                _store.Set("input.key.space", false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _eventBus.Fire("click");
                _store.Set("input.click_count", _clickCount + 1);
                
                float currentTime = Time.time;
                if (currentTime - _lastClickTime < DoubleClickThreshold)
                {
                    _clickCount++;
                    if (_clickCount == 2)
                    {
                        _eventBus.Fire("double_click");
                        _clickCount = 0;
                    }
                }
                else
                {
                    _clickCount = 1;
                }
                _lastClickTime = currentTime;
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                _eventBus.Fire("right_click");
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _eventBus.Fire("mouse_down");
                _store.Set("input.mouse.down", true);
                _store.Set("input.mouse.position", eventData.position);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _eventBus.Fire("mouse_up");
                _store.Set("input.mouse.down", false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragStartTime = Time.time;
            _dragStartPosition = eventData.position;
            _dragDistance = 0f;
            
            _eventBus.Fire("drag_start");
            _store.Set("input.drag.active", true);
            _store.Set("input.drag.start_position", _dragStartPosition);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            
            _dragDistance = Vector2.Distance(_dragStartPosition, eventData.position);
            float dragDuration = Time.time - _dragStartTime;
            
            _store.Set("input.drag.distance", _dragDistance);
            _store.Set("input.drag.duration", dragDuration);
            _store.Set("input.drag.position", eventData.position);
            
            _eventBus.Fire("drag");
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            
            _isDragging = false;
            float dragDuration = Time.time - _dragStartTime;
            
            _eventBus.Fire("drag_end");
            _store.Set("input.drag.active", false);
            _store.Set("input.drag.final_distance", _dragDistance);
            _store.Set("input.drag.final_duration", dragDuration);
            
            if (dragDuration > 1.0f)
            {
                _eventBus.Fire("long_drag");
            }
        }

        private void UpdateDragState()
        {
            if (_isDragging)
            {
                float dragDuration = Time.time - _dragStartTime;
                
                if (dragDuration >= 1.0f && dragDuration < 1.1f)
                {
                    _eventBus.Fire("drag_held_1s");
                }
                else if (dragDuration >= 2.0f && dragDuration < 2.1f)
                {
                    _eventBus.Fire("drag_held_2s");
                }
            }
        }
    }
}