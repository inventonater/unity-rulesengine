using UnityEngine;
using System.Linq;

namespace Inventonater.Rules
{
    /// <summary>
    /// Bootstrap component that sets up and initializes the rules engine
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RulesEngineManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool autoLoadSamples = true;
        [SerializeField] private bool showDevPanel = true;
        
        private RuleRepository _repository;
        private EntityStore _store;
        private Services _services;
        private TimerService _timerService;
        private RuleEngine _engine;
        private DesktopInput _input;
        private DevPanel _devPanel;
        
        private static RulesEngineManager _instance;
        public static RulesEngineManager Instance => _instance;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeComponents();
        }
        
        private void InitializeComponents()
        {
            // Add core components
            _repository = gameObject.AddComponent<RuleRepository>();
            _store = gameObject.AddComponent<EntityStore>();
            _services = gameObject.AddComponent<Services>();
            _timerService = gameObject.AddComponent<TimerService>();
            _engine = gameObject.AddComponent<RuleEngine>();
            
            // Add input provider
            _input = gameObject.AddComponent<DesktopInput>();
            
            // Add dev panel if enabled
            if (showDevPanel)
            {
                _devPanel = gameObject.AddComponent<DevPanel>();
            }
            
            Debug.Log("[RulesEngineManager] Components initialized");
        }
        
        private void Start()
        {
            // Initialize the engine
            _engine.Initialize(_repository, _store, _services, _timerService);
            
            // Auto-load sample rules if enabled
            if (autoLoadSamples)
            {
                LoadSampleRules();
            }
            
            Debug.Log("[RulesEngineManager] Rules Engine started successfully!");
            Debug.Log("Controls:");
            Debug.Log("  • Click: Beep");
            Debug.Log("  • Hold mouse: Toast notification");
            Debug.Log("  • Double-click: Loud beep");
            Debug.Log("  • Space twice: Triple beep");
            Debug.Log("  • Konami code: ↑↑↓↓←→←→BA");
            Debug.Log("  • F1: Toggle debug mode");
            Debug.Log("  • F2: Toggle DevPanel");
        }
        
        private void LoadSampleRules()
        {
            if (_devPanel != null)
            {
                // Use DevPanel's LoadAllSampleRules through reflection to avoid duplication
                var method = _devPanel.GetType().GetMethod("LoadAllSampleRules", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(_devPanel, null);
                    Debug.Log("[RulesEngineManager] Sample rules loaded via DevPanel");
                    return;
                }
            }
            
            Debug.LogWarning("[RulesEngineManager] Could not auto-load sample rules. Use DevPanel to load them manually.");
        }
        
        public void LoadRuleJson(string json)
        {
            _repository.LoadSingleRuleFromJson(json);
            _engine.ReloadRules();
        }
        
        public void LoadRulesJson(string json)
        {
            _repository.LoadFromJson(json);
            _engine.ReloadRules();
        }
        
        public void ReloadRules()
        {
            _engine.ReloadRules();
        }
        
        public void EmitEvent(string eventName)
        {
            EventBus.Publish(eventName);
        }
        
        public void SetState(string key, string value)
        {
            _store.SetState(key, value);
        }
        
        public void SetNumeric(string key, double value)
        {
            _store.SetNumeric(key, value);
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
