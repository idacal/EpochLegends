using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror.Discovery;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.UI.Manager; // Para acceder a UIManager y UIPanel

namespace EpochLegends.Core.UI.Menu
{
    public class ServerBrowserController : MonoBehaviour
    {
        [SerializeField] private Transform serverListContent;
        [SerializeField] private GameObject serverItemPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button backButton;
        [SerializeField] private NetworkDiscovery networkDiscovery;
        
        private Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();
        private float refreshCooldown = 0f;
        
        private void Start()
        {
            if (networkDiscovery == null)
                networkDiscovery = FindObjectOfType<NetworkDiscovery>();
                
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshServerList);
                
            if (backButton != null)
                backButton.onClick.AddListener(BackToMainMenu);
                
            // Start discovery on launch
            StartDiscovery();
        }
        
        private void Update()
        {
            // Manage refresh cooldown
            if (refreshCooldown > 0)
            {
                refreshCooldown -= Time.deltaTime;
                if (refreshButton != null)
                    refreshButton.interactable = refreshCooldown <= 0;
            }
        }
        
        public void StartDiscovery()
        {
            discoveredServers.Clear();
            ClearServerList();
            
            if (networkDiscovery != null)
            {
                networkDiscovery.OnServerFound.AddListener(OnDiscoveredServer);
                networkDiscovery.StartDiscovery();
            }
            
            // Set cooldown to prevent spam
            refreshCooldown = 3f;
            if (refreshButton != null)
                refreshButton.interactable = false;
        }
        
        public void RefreshServerList()
        {
            if (refreshCooldown <= 0)
            {
                StartDiscovery();
            }
        }
        
        private void ClearServerList()
        {
            if (serverListContent != null)
            {
                foreach (Transform child in serverListContent)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        public void OnDiscoveredServer(ServerResponse info)
        {
            // Add to dictionary
            discoveredServers[info.serverId] = info;
            
            // Update UI
            DisplayServer(info);
        }
        
        private void DisplayServer(ServerResponse info)
        {
            if (serverListContent != null && serverItemPrefab != null)
            {
                GameObject serverItem = Instantiate(serverItemPrefab, serverListContent);
                ServerItemUI itemUI = serverItem.GetComponent<ServerItemUI>();
                
                if (itemUI != null)
                {
                    itemUI.SetupServer(info);
                    itemUI.OnJoinClicked += () => JoinServer(info);
                }
            }
        }
        
        private void JoinServer(ServerResponse info)
        {
            Debug.Log($"Joining server: {info.uri}");
            
            // Connect to the server
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.networkAddress = info.uri.Host;
                EpochNetworkManager.Instance.JoinGame(info.uri.Host, ""); // Assuming no password from discovery
            }
            else
            {
                Debug.LogError("EpochNetworkManager instance not found!");
            }
        }
        
        private void BackToMainMenu()
        {
            // Return to main menu
            gameObject.SetActive(false);
            
            // Find main menu and show it
            UIManager manager = FindObjectOfType<UIManager>();
            if (manager != null)
            {
                // Especificar explícitamente el enum UIPanel con el namespace completo
                manager.ShowPanel(EpochLegends.Core.UI.Manager.UIPanel.MainMenu);
            }
        }
        
        private void OnDestroy()
        {
            if (networkDiscovery != null)
            {
                networkDiscovery.OnServerFound.RemoveListener(OnDiscoveredServer);
                networkDiscovery.StopDiscovery();
            }
        }
    }
    
    // Helper class for server list items
    public class ServerItemUI : MonoBehaviour
    {
        [SerializeField] private Text serverNameText;
        [SerializeField] private Text playerCountText;
        [SerializeField] private Text pingText;
        [SerializeField] private Button joinButton;
        
        public System.Action OnJoinClicked;
        
        private void Start()
        {
            if (joinButton != null)
                joinButton.onClick.AddListener(() => OnJoinClicked?.Invoke());
        }
        
        public void SetupServer(ServerResponse info)
        {
            if (serverNameText != null)
                serverNameText.text = info.uri.ToString(); // Usa la URI como nombre
                
            if (playerCountText != null)
                playerCountText.text = "Players: ?/?"; // No hay datos de jugadores en ServerResponse
                
            if (pingText != null)
                pingText.text = "Ping: ?ms"; // No hay datos de ping en ServerResponse
        }
    }
}