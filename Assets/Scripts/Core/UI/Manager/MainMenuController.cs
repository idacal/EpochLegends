using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.UI.Manager;

namespace EpochLegends.Core.UI.Menu
{
    public class MainMenuController : MonoBehaviour, IUIPanelController
    {
        [Header("Main Menu Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private GameObject serverBrowserPanel;
        
        [Header("Host Game Settings")]
        [SerializeField] private TMP_InputField hostGameNameInput;
        [SerializeField] private TMP_InputField hostPasswordInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private Button createServerButton;
        
        [Header("Join Game Settings")]
        [SerializeField] private TMP_InputField joinIPInput;
        [SerializeField] private TMP_InputField joinPasswordInput;
        [SerializeField] private Button joinServerButton;
        
        private EpochNetworkManager networkManager;
        
        private void Awake()
        {
            // Get the network manager
            networkManager = EpochNetworkManager.Instance;
            
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager instance not found!");
            }
            
            // Set default values
            if (maxPlayersInput != null)
                maxPlayersInput.text = "10";
                
            if (hostGameNameInput != null)
                hostGameNameInput.text = "Epoch Legends Server";
                
            if (joinIPInput != null)
                joinIPInput.text = "localhost";
                
            // Set up button listeners
            SetupButtonListeners();
        }
        
        private void SetupButtonListeners()
        {
            // Find buttons if not assigned in inspector
            if (createServerButton == null)
                createServerButton = hostPanel.GetComponentInChildren<Button>();
                
            if (joinServerButton == null)
                joinServerButton = joinPanel.GetComponentInChildren<Button>();
            
            // Add listeners
            if (createServerButton != null)
                createServerButton.onClick.AddListener(CreateServer);
                
            if (joinServerButton != null)
                joinServerButton.onClick.AddListener(JoinServer);
                
            // Find additional buttons in the main panel
            if (mainPanel != null)
            {
                Button[] buttons = mainPanel.GetComponentsInChildren<Button>();
                foreach (Button button in buttons)
                {
                    // Add listeners based on button name
                    switch (button.name)
                    {
                        case "HostButton":
                            button.onClick.AddListener(() => ShowPanel(hostPanel));
                            break;
                            
                        case "JoinButton":
                            button.onClick.AddListener(() => ShowPanel(joinPanel));
                            break;
                            
                        case "BrowserButton":
                            button.onClick.AddListener(() => ShowPanel(serverBrowserPanel));
                            break;
                            
                        case "OptionsButton":
                            button.onClick.AddListener(ShowOptions);
                            break;
                            
                        case "QuitButton":
                            button.onClick.AddListener(QuitGame);
                            break;
                    }
                }
            }
            
            // Add back buttons to sub-panels
            AddBackButtonListeners(hostPanel);
            AddBackButtonListeners(joinPanel);
            AddBackButtonListeners(serverBrowserPanel);
        }
        
        private void AddBackButtonListeners(GameObject panel)
        {
            if (panel == null) return;
            
            Button[] buttons = panel.GetComponentsInChildren<Button>();
            foreach (Button button in buttons)
            {
                if (button.name.Contains("Back"))
                {
                    button.onClick.AddListener(() => ShowPanel(mainPanel));
                }
            }
        }
        
        private void ShowPanel(GameObject targetPanel)
        {
            // Hide all panels
            if (mainPanel != null) mainPanel.SetActive(false);
            if (hostPanel != null) hostPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(false);
            if (serverBrowserPanel != null) serverBrowserPanel.SetActive(false);
            
            // Show target panel
            if (targetPanel != null)
            {
                targetPanel.SetActive(true);
            }
        }
        
        private void CreateServer()
        {
            if (networkManager == null) return;
            
            // Get server settings
            string serverName = hostGameNameInput != null ? hostGameNameInput.text : "Epoch Legends Server";
            string password = hostPasswordInput != null ? hostPasswordInput.text : "";
            int maxPlayers = 10;
            
            if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int parsedMaxPlayers))
            {
                maxPlayers = Mathf.Clamp(parsedMaxPlayers, 2, 20);
            }
            
            // Start hosting
            networkManager.StartHost(serverName, password, maxPlayers);
            
            // Switch to lobby UI
            UIManager.Instance?.ShowPanel(UIPanel.Lobby);
        }
        
        private void JoinServer()
        {
            if (networkManager == null) return;
            
            // Get connection settings
            string address = joinIPInput != null ? joinIPInput.text : "localhost";
            string password = joinPasswordInput != null ? joinPasswordInput.text : "";
            
            // Join game
            networkManager.JoinGame(address, password);
            
            // Show connecting UI
            UIManager.Instance?.ShowPanel(UIPanel.Loading);
        }
        
        private void ShowOptions()
        {
            // Show options menu
            UIManager.Instance?.ShowPanel(UIPanel.Options);
        }
        
        public void QuitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        #region IUIPanelController Implementation
        
        public void OnPanelShown()
        {
            // Ensure main panel is shown first
            ShowPanel(mainPanel);
        }
        
        public void OnPanelHidden()
        {
            // Clean up or cancel any ongoing processes
        }
        
        #endregion
    }
}