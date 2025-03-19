using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror.Discovery;
using System.Collections.Generic;
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
        
        [Header("Player Settings")]
        [SerializeField] private TMP_InputField playerNameInput;
        
        [Header("Host Game Settings")]
        [SerializeField] private TMP_InputField hostGameNameInput;
        [SerializeField] private TMP_InputField hostPasswordInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private Button createServerButton;
        
        [Header("Join Game Settings")]
        [SerializeField] private TMP_InputField joinIPInput;
        [SerializeField] private TMP_InputField joinPasswordInput;
        [SerializeField] private Button joinServerButton;
        
        [Header("Server Browser Settings")]
        [SerializeField] private Transform serverListContent;
        [SerializeField] private GameObject serverItemPrefab;
        [SerializeField] private Button refreshServersButton;
        [SerializeField] private NetworkDiscovery networkDiscovery;
        
        [Header("Debug")]
        [SerializeField] private bool debugUI = false;
        
        private EpochNetworkManager networkManager;
        private Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();
        private float refreshCooldown = 0f;
        
        private void Awake()
        {
            // Get the network manager
            networkManager = EpochNetworkManager.Instance;
            
            if (networkManager == null)
            {
                Debug.LogWarning("NetworkManager instance not found! NetworkManager funcionality will be limited.");
            }
            
            // Find NetworkDiscovery if not assigned
            if (networkDiscovery == null)
            {
                networkDiscovery = FindObjectOfType<NetworkDiscovery>();
            }
            
            // Set default values
            if (playerNameInput != null)
            {
                string savedName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
                playerNameInput.text = savedName;
                
                if (debugUI)
                    Debug.Log($"[MainMenuController] Loaded player name: {savedName}");
            }
            
            if (maxPlayersInput != null)
                maxPlayersInput.text = "10";
                
            if (hostGameNameInput != null)
                hostGameNameInput.text = "Epoch Legends Server";
                
            if (joinIPInput != null)
                joinIPInput.text = "localhost";
                
            // Set up button listeners
            SetupButtonListeners();
        }
        
        private void Update()
        {
            // Update refresh cooldown for server browser
            if (refreshCooldown > 0)
            {
                refreshCooldown -= Time.deltaTime;
                if (refreshServersButton != null)
                {
                    refreshServersButton.interactable = refreshCooldown <= 0;
                }
            }
        }
        
        private void SetupButtonListeners()
        {
            // Find buttons if not assigned in inspector
            if (createServerButton == null && hostPanel != null)
                createServerButton = hostPanel.GetComponentInChildren<Button>();
                
            if (joinServerButton == null && joinPanel != null)
                joinServerButton = joinPanel.GetComponentInChildren<Button>();
                
            if (refreshServersButton == null && serverBrowserPanel != null)
            {
                // Buscar botón de refresh por nombre
                Button[] buttons = serverBrowserPanel.GetComponentsInChildren<Button>();
                foreach (Button button in buttons)
                {
                    if (button.name.Contains("Refresh"))
                    {
                        refreshServersButton = button;
                        break;
                    }
                }
            }
            
            // Add listeners
            if (createServerButton != null)
                createServerButton.onClick.AddListener(CreateServer);
                
            if (joinServerButton != null)
                joinServerButton.onClick.AddListener(JoinServer);
                
            if (refreshServersButton != null)
                refreshServersButton.onClick.AddListener(RefreshServerList);
                
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
                            button.onClick.AddListener(() => {
                                ShowPanel(serverBrowserPanel);
                                RefreshServerList(); // Auto-refresh when showing
                            });
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
            
            // Set up NetworkDiscovery callbacks
            if (networkDiscovery != null)
            {
                networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
            }
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
            
            // Get player name
            string playerName = playerNameInput != null ? playerNameInput.text : "Player" + Random.Range(1000, 9999);
            
            // Validar nombre del jugador
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player" + Random.Range(1000, 9999);
                
            // Limitar longitud
            if (playerName.Length > 20)
                playerName = playerName.Substring(0, 20);
            
            // Guardar nombre para futuras sesiones
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
            
            // Get server settings
            string serverName = hostGameNameInput != null ? hostGameNameInput.text : "Epoch Legends Server";
            string password = hostPasswordInput != null ? hostPasswordInput.text : "";
            int maxPlayers = 10;
            
            // Obtener max players del input field
            if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int parsedMaxPlayers))
            {
                maxPlayers = Mathf.Clamp(parsedMaxPlayers, 2, 20);
            }
            
            Debug.Log($"Creating server: {serverName}, Player: {playerName}, Password: {!string.IsNullOrEmpty(password)}, Max Players: {maxPlayers}");
            
            // Start hosting
            networkManager.StartHost(serverName, password, maxPlayers);
            
            // Switch to lobby UI
            UIManager.Instance?.ShowPanel(UIPanel.Lobby);
        }
        
        private void JoinServer()
        {
            if (networkManager == null) return;
            
            // Get player name
            string playerName = playerNameInput != null ? playerNameInput.text : "Player" + Random.Range(1000, 9999);
            
            // Validar nombre del jugador
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player" + Random.Range(1000, 9999);
                
            // Limitar longitud
            if (playerName.Length > 20)
                playerName = playerName.Substring(0, 20);
            
            // Guardar nombre para futuras sesiones
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
            
            // Get connection settings
            string address = joinIPInput != null ? joinIPInput.text : "localhost";
            string password = joinPasswordInput != null ? joinPasswordInput.text : "";
            
            Debug.Log($"Joining server at: {address}, Player: {playerName}");
            
            // Join game
            networkManager.JoinGame(address, password);
            
            // Show connecting UI
            UIManager.Instance?.ShowPanel(UIPanel.Loading);
        }
        
        // Server Browser Methods
        public void RefreshServerList()
        {
            if (networkDiscovery == null || refreshCooldown > 0) return;
            
            // Clear previous servers
            discoveredServers.Clear();
            ClearServerList();
            
            // Start discovery
            networkDiscovery.StartDiscovery();
            
            // Set cooldown
            refreshCooldown = 3f;
            if (refreshServersButton != null)
            {
                refreshServersButton.interactable = false;
            }
            
            Debug.Log("Refreshing server list...");
        }
        
        private void ClearServerList()
        {
            if (serverListContent == null) return;
            
            // Remove all items in the server list
            foreach (Transform child in serverListContent)
            {
                Destroy(child.gameObject);
            }
        }
        
        public void OnDiscoveredServer(ServerResponse info)
        {
            Debug.Log($"Discovered server: {info.uri}");
            
            // Add to dictionary
            discoveredServers[info.serverId] = info;
            
            // Update UI
            DisplayServer(info);
        }
        
        private void DisplayServer(ServerResponse info)
        {
            if (serverListContent == null || serverItemPrefab == null) return;
            
            // Create server item
            GameObject serverItem = Instantiate(serverItemPrefab, serverListContent);
            
            // Set up server item info
            SetupServerItem(serverItem, info);
        }
        
        private void SetupServerItem(GameObject serverItem, ServerResponse info)
        {
            // Find components in the server item
            TMP_Text serverNameText = serverItem.GetComponentInChildren<TMP_Text>(true);
            Button joinButton = serverItem.GetComponentInChildren<Button>(true);
            
            // Set server info
            if (serverNameText != null)
            {
                // Use uri as name if no custom name available
                serverNameText.text = info.uri.ToString();
            }
            
            // Setup join button
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(() => JoinServerFromBrowser(info));
            }
        }
        
        private void JoinServerFromBrowser(ServerResponse info)
        {
            if (networkManager == null) return;
            
            // Get player name first
            string playerName = playerNameInput != null ? playerNameInput.text : "Player" + Random.Range(1000, 9999);
            
            // Validar y guardar nombre del jugador
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player" + Random.Range(1000, 9999);
                
            if (playerName.Length > 20)
                playerName = playerName.Substring(0, 20);
                
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
            
            Debug.Log($"Joining server from browser: {info.uri}, Player: {playerName}");
            
            // Connect to the selected server
            networkManager.networkAddress = info.uri.Host;
            networkManager.JoinGame(info.uri.Host, ""); // Assuming no password from discovery
            
            // Show connecting UI
            UIManager.Instance?.ShowPanel(UIPanel.Loading);
        }
        
        private void ShowOptions()
        {
            // Show options menu
            UIManager.Instance?.ShowPanel(UIPanel.Options);
        }
        
        private void QuitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        private void OnDestroy()
        {
            // Clean up NetworkDiscovery callbacks
            if (networkDiscovery != null)
            {
                networkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
                
                // Stop discovery if it's running
                if (networkDiscovery.isActiveAndEnabled)
                {
                    networkDiscovery.StopDiscovery();
                }
            }
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
            // Por ejemplo, detener la búsqueda de servidores
            if (networkDiscovery != null && networkDiscovery.isActiveAndEnabled)
            {
                networkDiscovery.StopDiscovery();
            }
        }
        
        #endregion
    }
}