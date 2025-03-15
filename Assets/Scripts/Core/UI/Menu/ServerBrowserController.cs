using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.UI.Manager;

namespace EpochLegends.Core.UI.Menu
{
    public class ServerBrowserController : MonoBehaviour, IUIPanelController
    {
        [Header("UI References")]
        [SerializeField] private Transform serverListContainer;
        [SerializeField] private GameObject serverListItemPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI statusText;

        // List of server entries
        private List<GameObject> serverEntries = new List<GameObject>();
        private EpochNetworkManager networkManager;

        private void Awake()
        {
            // Get the network manager
            networkManager = EpochNetworkManager.Instance;
            
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager instance not found!");
            }

            // Set up button listeners
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshServerList);
                
            if (backButton != null)
                backButton.onClick.AddListener(ReturnToMainMenu);
        }

        public void RefreshServerList()
        {
            ClearServerList();
            
            if (statusText != null)
                statusText.text = "Searching for servers...";
            
            // In a real implementation, this would query LAN or internet servers
            // For now, we'll just add some dummy servers for testing
            
            // Simulate network delay
            Invoke(nameof(PopulateWithDummyServers), 1.0f);
        }

        private void ClearServerList()
        {
            // Destroy all existing server entries
            foreach (var entry in serverEntries)
            {
                Destroy(entry);
            }
            
            serverEntries.Clear();
        }

        private void PopulateWithDummyServers()
        {
            // Clear status text
            if (statusText != null)
                statusText.text = "";
                
            // Add some dummy servers for testing
            AddServerToList("Local Test Server", "127.0.0.1", "3/10", 0);
            AddServerToList("Bob's Game", "192.168.1.105", "8/10", 30);
            AddServerToList("Competitive Match", "203.0.113.42", "10/10", 100);
            AddServerToList("Training Room", "198.51.100.73", "1/6", 15);
            
            // If no servers found
            if (serverEntries.Count == 0 && statusText != null)
            {
                statusText.text = "No servers found. Try again later.";
            }
        }

        private void AddServerToList(string serverName, string ipAddress, string players, int ping)
        {
            if (serverListItemPrefab == null || serverListContainer == null)
                return;
                
            // Instantiate the server list item
            GameObject serverEntry = Instantiate(serverListItemPrefab, serverListContainer);
            serverEntries.Add(serverEntry);
            
            // Set server info
            ServerListItem listItem = serverEntry.GetComponent<ServerListItem>();
            if (listItem != null)
            {
                listItem.SetServerInfo(serverName, ipAddress, players, ping);
                listItem.SetJoinAction(() => JoinServer(ipAddress, serverName));
            }
        }

        private void JoinServer(string ipAddress, string serverName)
        {
            Debug.Log($"Joining server: {serverName} at {ipAddress}");
            
            if (networkManager != null)
            {
                networkManager.JoinGame(ipAddress, "");
                UIManager.Instance?.ShowPanel(UIPanel.Loading);
            }
        }

        private void ReturnToMainMenu()
        {
            UIManager.Instance?.ShowPanel(UIPanel.MainMenu);
        }

        #region IUIPanelController Implementation
        
        public void OnPanelShown()
        {
            // Refresh the server list when panel is shown
            RefreshServerList();
        }
        
        public void OnPanelHidden()
        {
            // Cancel any ongoing server discovery
            CancelInvoke(nameof(PopulateWithDummyServers));
        }
        
        #endregion
    }

    // Class for individual server list items
    public class ServerListItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI serverNameText;
        [SerializeField] private TextMeshProUGUI ipAddressText;
        [SerializeField] private TextMeshProUGUI playersText;
        [SerializeField] private TextMeshProUGUI pingText;
        [SerializeField] private Button joinButton;

        private string ipAddress;

        private void Awake()
        {
            if (joinButton == null)
                joinButton = GetComponentInChildren<Button>();
        }

        public void SetServerInfo(string serverName, string ip, string players, int ping)
        {
            ipAddress = ip;
            
            if (serverNameText != null)
                serverNameText.text = serverName;
                
            if (ipAddressText != null)
                ipAddressText.text = ip;
                
            if (playersText != null)
                playersText.text = players;
                
            if (pingText != null)
                pingText.text = ping + " ms";
        }

        public void SetJoinAction(UnityEngine.Events.UnityAction action)
        {
            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(action);
            }
        }
    }
}