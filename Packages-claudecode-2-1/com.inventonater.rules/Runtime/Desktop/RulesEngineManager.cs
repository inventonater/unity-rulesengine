using UnityEngine;

namespace Inventonater.Rules
{
    public class RulesEngineManager : MonoBehaviour
    {
        [SerializeField] private string rulesDirectory = "StreamingAssets/Rules";
        
        private RuleEngine _engine;
        private IRuleRepository _repository;
        private IEntityStore _store;
        private IEventBus _eventBus;
        private IServices _services;
        private DesktopInput _input;

        private void Awake()
        {
            _repository = new RuleRepository();
            _store = new EntityStore();
            _eventBus = new EventBus();
            
            GameObject servicesObject = new GameObject("RuleServices");
            servicesObject.transform.SetParent(transform);
            _services = servicesObject.AddComponent<Services>();
            
            GameObject inputObject = new GameObject("DesktopInput");
            inputObject.transform.SetParent(transform);
            _input = inputObject.AddComponent<DesktopInput>();
            _input.Initialize(_eventBus, _store);
            
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            GameObject inputPanel = new GameObject("InputPanel");
            inputPanel.transform.SetParent(canvas.transform, false);
            var rect = inputPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            var image = inputPanel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 0.01f);
            image.raycastTarget = true;
            
            inputPanel.AddComponent<DesktopInput>().Initialize(_eventBus, _store);
        }

        private void Start()
        {
            LoadRules();
            InitializeEngine();
            InitializeGameState();
        }

        private void LoadRules()
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, rulesDirectory);
            
            if (System.IO.Directory.Exists(fullPath))
            {
                (_repository as RuleRepository)?.LoadFromDirectory(fullPath);
                Debug.Log($"Loaded {_repository.GetAllRules().Count()} rules from {fullPath}");
            }
            else
            {
                Debug.LogWarning($"Rules directory not found: {fullPath}");
                LoadDefaultRules();
            }
        }

        private void LoadDefaultRules()
        {
            var defaultRule = new RuleDto
            {
                id = "default_click_sound",
                mode = "single",
                triggers = new System.Collections.Generic.List<TriggerDto>
                {
                    new TriggerDto { type = "event", name = "click" }
                },
                actions = new System.Collections.Generic.List<ActionDto>
                {
                    new ActionDto 
                    { 
                        type = "play_sound", 
                        sound = "beep", 
                        volume_0_to_1 = 0.5f 
                    },
                    new ActionDto
                    {
                        type = "log",
                        message = "Click detected!",
                        severity = "info"
                    }
                }
            };
            
            (_repository as RuleRepository)?.AddRule(defaultRule);
        }

        private void InitializeEngine()
        {
            _engine = new RuleEngine(_repository, _store, _eventBus, _services);
            _engine.Initialize();
            
            Debug.Log("Rules Engine initialized");
        }

        private void InitializeGameState()
        {
            _store.Set("game.health", 100);
            _store.Set("game.score", 0);
            _store.Set("game.level", 1);
            _store.Set("game.combo", 0);
            _store.Set("game.time", 0f);
        }

        private void Update()
        {
            float gameTime = _store.Get<float>("game.time");
            _store.Set("game.time", gameTime + Time.deltaTime);
            
            if (Time.frameCount % 60 == 0)
            {
                _eventBus.Fire("tick");
            }
        }

        private void OnDestroy()
        {
            _engine?.Dispose();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _eventBus.Fire("game_paused");
            }
            else
            {
                _eventBus.Fire("game_resumed");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                _eventBus.Fire("game_focused");
            }
            else
            {
                _eventBus.Fire("game_unfocused");
            }
        }
    }
}