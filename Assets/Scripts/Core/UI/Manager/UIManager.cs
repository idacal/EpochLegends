using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

namespace EpochLegends.Core.UI.Manager
{
    public enum UIPanel
    {
        None,
        MainMenu,
        ServerBrowser,
        Lobby,
        HeroSelection,
        Loading,
        HUD,
        Scoreboard,
        Pause,
        EndGame,
        Options
    }
    
    [System.Serializable]
    public class UIPanelInfo
    {
        public UIPanel panelType;
        public GameObject panelObject;
        [Tooltip("If true, this panel will remain active when switching to another panel")]
        public bool persistent = false;
        [Tooltip("If true, this panel can be shown alongside other non-exclusive panels")]
        public bool exclusive = true;
    }
    
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        
        [Header("Panel Configuration")]
        [SerializeField] private List<UIPanelInfo> panels = new List<UIPanelInfo>();
        [SerializeField] private UIPanel initialPanel = UIPanel.MainMenu;
        
        [Header("Transition Settings")]
        [SerializeField] private float transitionTime = 0.3f;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        
        // State tracking
        private UIPanel currentPanel = UIPanel.None;
        private Stack<UIPanel> panelHistory = new Stack<UIPanel>();
        private List<UIPanel> activePanels = new List<UIPanel>();
        
        // References to commonly used UI elements
        private Dictionary<UIPanel, GameObject> panelLookup = new Dictionary<UIPanel, GameObject>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize panel lookup dictionary
            InitializePanelLookup();
        }
        
        private void Start()
        {
            // Show initial panel
            ShowPanel(initialPanel, false);
        }
        
        private void InitializePanelLookup()
        {
            panelLookup.Clear();
            
            foreach (var panelInfo in panels)
            {
                if (panelInfo.panelObject != null)
                {
                    panelLookup[panelInfo.panelType] = panelInfo.panelObject;
                    
                    // Ensure all panels start hidden
                    panelInfo.panelObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"Panel object missing for {panelInfo.panelType}");
                }
            }
        }
        
        #region Panel Management
        
        public void ShowPanel(UIPanel panel, bool addToPanelHistory = true)
        {
            if (panel == currentPanel) return;
            
            // Find panel info
            UIPanelInfo panelInfo = GetPanelInfo(panel);
            
            if (panelInfo == null || panelInfo.panelObject == null)
            {
                Debug.LogWarning($"Cannot show panel {panel}: Panel not found");
                return;
            }
            
            // Add current panel to history if needed
            if (addToPanelHistory && currentPanel != UIPanel.None)
            {
                panelHistory.Push(currentPanel);
            }
            
            // Hide current panels if exclusive
            if (panelInfo.exclusive)
            {
                HideAllPanels(true);
            }
            
            // Show the new panel
            panelInfo.panelObject.SetActive(true);
            
            // Update state
            currentPanel = panel;
            if (!activePanels.Contains(panel))
            {
                activePanels.Add(panel);
            }
            
            // Notify panel controllers
            NotifyPanelShown(panelInfo.panelObject);
            
            Debug.Log($"Showing panel: {panel}");
        }
        
        public void HidePanel(UIPanel panel)
        {
            UIPanelInfo panelInfo = GetPanelInfo(panel);
            
            if (panelInfo == null || panelInfo.panelObject == null)
            {
                return;
            }
            
            // Hide the panel
            panelInfo.panelObject.SetActive(false);
            
            // Update state
            activePanels.Remove(panel);
            
            // If this was the current panel, set current to None
            if (currentPanel == panel)
            {
                currentPanel = UIPanel.None;
            }
            
            // Notify panel controllers
            NotifyPanelHidden(panelInfo.panelObject);
            
            Debug.Log($"Hiding panel: {panel}");
        }
        
        public void ReturnToPreviousPanel()
        {
            if (panelHistory.Count > 0)
            {
                UIPanel previousPanel = panelHistory.Pop();
                ShowPanel(previousPanel, false);
            }
        }
        
        public void HideAllPanels(bool keepPersistent = true)
        {
            List<UIPanel> panelsToHide = new List<UIPanel>(activePanels);
            
            foreach (UIPanel panel in panelsToHide)
            {
                UIPanelInfo panelInfo = GetPanelInfo(panel);
                
                if (panelInfo != null && (!keepPersistent || !panelInfo.persistent))
                {
                    HidePanel(panel);
                }
            }
            
            if (!keepPersistent)
            {
                // Clear history if not keeping persistent panels
                panelHistory.Clear();
            }
        }
        
        public bool IsPanelActive(UIPanel panel)
        {
            return activePanels.Contains(panel);
        }
        
        private UIPanelInfo GetPanelInfo(UIPanel panelType)
        {
            foreach (var panelInfo in panels)
            {
                if (panelInfo.panelType == panelType)
                {
                    return panelInfo;
                }
            }
            
            return null;
        }
        
        private void NotifyPanelShown(GameObject panelObject)
        {
            // Notify UI panel controllers that they're being shown
            IUIPanelController[] controllers = panelObject.GetComponentsInChildren<IUIPanelController>();
            foreach (var controller in controllers)
            {
                controller.OnPanelShown();
            }
        }
        
        private void NotifyPanelHidden(GameObject panelObject)
        {
            // Notify UI panel controllers that they're being hidden
            IUIPanelController[] controllers = panelObject.GetComponentsInChildren<IUIPanelController>();
            foreach (var controller in controllers)
            {
                controller.OnPanelHidden();
            }
        }
        
        #endregion
        
        #region Transitions
        
        public void FadeIn(float duration = -1)
        {
            if (fadeCanvasGroup == null) return;
            
            float fadeDuration = duration > 0 ? duration : transitionTime;
            
            // Stop all running fade coroutines
            StopAllCoroutines();
            
            // Ensure the fade canvas is active
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 1;
            
            // Start fade in (from black)
            LeanTween.alphaCanvas(fadeCanvasGroup, 0, fadeDuration)
                .setOnComplete(() => {
                    fadeCanvasGroup.gameObject.SetActive(false);
                });
        }
        
        public void FadeOut(float duration = -1, System.Action onComplete = null)
        {
            if (fadeCanvasGroup == null) return;
            
            float fadeDuration = duration > 0 ? duration : transitionTime;
            
            // Stop all running fade coroutines
            StopAllCoroutines();
            
            // Ensure the fade canvas is active and transparent
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 0;
            
            // Start fade out (to black)
            LeanTween.alphaCanvas(fadeCanvasGroup, 1, fadeDuration)
                .setOnComplete(() => {
                    onComplete?.Invoke();
                });
        }
        
        public void TransitionBetweenPanels(UIPanel from, UIPanel to, float duration = -1)
        {
            float transitionDuration = duration > 0 ? duration : transitionTime;
            
            // Fade out, change panel, then fade in
            FadeOut(transitionDuration / 2, () => {
                HidePanel(from);
                ShowPanel(to);
                FadeIn(transitionDuration / 2);
            });
        }
        
        #endregion
        
        #region UI Element Access
        
        public GameObject GetPanelObject(UIPanel panel)
        {
            if (panelLookup.TryGetValue(panel, out GameObject panelObj))
            {
                return panelObj;
            }
            
            return null;
        }
        
        public T FindUIComponent<T>(UIPanel panel, string componentPath) where T : Component
        {
            GameObject panelObj = GetPanelObject(panel);
            if (panelObj == null) return null;
            
            if (string.IsNullOrEmpty(componentPath))
            {
                return panelObj.GetComponent<T>();
            }
            
            Transform child = panelObj.transform.Find(componentPath);
            if (child != null)
            {
                return child.GetComponent<T>();
            }
            
            return null;
        }
        
        public void SetText(UIPanel panel, string textPath, string message)
        {
            Text textComponent = FindUIComponent<Text>(panel, textPath);
            if (textComponent != null)
            {
                textComponent.text = message;
            }
        }
        
        public void SetButtonInteractable(UIPanel panel, string buttonPath, bool interactable)
        {
            Button button = FindUIComponent<Button>(panel, buttonPath);
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
        
        public void SetVisible(UIPanel panel, string objectPath, bool visible)
        {
            GameObject panelObj = GetPanelObject(panel);
            if (panelObj == null) return;
            
            Transform child = panelObj.transform.Find(objectPath);
            if (child != null)
            {
                child.gameObject.SetActive(visible);
            }
        }
        
        #endregion
    }
    
    // Interface for UI panels to implement to receive notifications
    public interface IUIPanelController
    {
        void OnPanelShown();
        void OnPanelHidden();
    }
}