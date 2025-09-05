using UnityEngine;

namespace Inventonater.Rules.Demo
{
    /// <summary>
    /// Simple setup script for the Rules Engine demo.
    /// Attach this to an empty GameObject to quickly set up all required components.
    /// </summary>
    public class DemoSetup : MonoBehaviour
    {
        private void Awake()
        {
            // Create the Rules Manager GameObject if not present
            GameObject rulesManager = GameObject.Find("RulesManager");
            if (rulesManager == null)
            {
                rulesManager = new GameObject("RulesManager");
            }
            
            // Add all required components
            if (!rulesManager.GetComponent<RuleRepository>())
                rulesManager.AddComponent<RuleRepository>();
            
            if (!rulesManager.GetComponent<EntityStore>())
                rulesManager.AddComponent<EntityStore>();
            
            if (!rulesManager.GetComponent<Services>())
                rulesManager.AddComponent<Services>();
            
            if (!rulesManager.GetComponent<TimerService>())
                rulesManager.AddComponent<TimerService>();
            
            if (!rulesManager.GetComponent<RuleEngine>())
                rulesManager.AddComponent<RuleEngine>();
            
            if (!rulesManager.GetComponent<DesktopInput>())
                rulesManager.AddComponent<DesktopInput>();
            
            if (!rulesManager.GetComponent<DevPanel>())
                rulesManager.AddComponent<DevPanel>();
            
            // Create a simple UI Canvas for toasts
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            Debug.Log("[DemoSetup] Rules Engine demo components initialized!");
            Debug.Log("[DemoSetup] Controls:");
            Debug.Log("  - Click: Beep sound");
            Debug.Log("  - Double-click: Louder beep");
            Debug.Log("  - Hold mouse button: Toast message");
            Debug.Log("  - Move mouse fast: Triple beep");
            Debug.Log("  - Space x2: Triple beep combo");
            Debug.Log("  - Arrow keys + B + A: Konami code");
            Debug.Log("  - F1: Toggle debug mode");
            Debug.Log("  - F2: Toggle DevPanel");
        }
    }
}