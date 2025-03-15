using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Network.Manager;
using EpochLegends;

namespace EpochLegends.Core.UI.Lobby
{
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }
        
        [Header("Server Info")]
        [SerializeField] private Text serverNameText;
        [SerializeField] private Text playerCountText;
        
        [Header("Player List")]
        [SerializeField] private Transform team1Container;
        [SerializeField] private Transform team2Container;
        [SerializeField] private GameObject playerEntryPrefab;
        
        [Header("Buttons")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button switchTeamButton;
        [SerializeField] private Button leaveButton;
        
        [Header("Debug")]
        [SerializeField] private bool debugUI = true;
        
        private Dictionary<uint, GameObject> playerEntries = new Dictionary<uint, GameObject>();
        private bool isReady = false;
        
        // Last update time to prevent excessive refreshes
        private float lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 0.5f;
        
        // Flag to force a full refresh
        private bool forceRefresh = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            if (debugUI)
                Debug.Log("[LobbyUI] Initialized");
        }
        
        private void Start()
        {
            // Setup button listeners
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyClicked);
                
            if (switchTeamButton != null)
                switchTeamButton.onClick.AddListener(OnSwitchTeamClicked);
                
            if (leaveButton != null)
                leaveButton.onClick.AddListener(OnLeaveClicked);
                
            // Initial UI update
            if (debugUI)
                Debug.Log("[LobbyUI] Starting initial UI refresh");
                
            RefreshUI();
            
            // Check if we're the host
            bool isHost = NetworkServer.active;
            if (debugUI)
                Debug.Log($"[LobbyUI] Started. Is host: {isHost}");
                
            // Request server for player list if we are a client
            if (NetworkClient.active && !NetworkServer.active)
            {
                if (debugUI)
                    Debug.Log("[LobbyUI] Requesting player list from server");
                
                // Allow time for connections to stabilize
                Invoke(nameof(RequestPlayerList), 1.0f);
            }
        }
        
        private void Update()
        {
            // Only refresh periodically or when forced to avoid excessive UI updates
            if (forceRefresh || Time.time - lastRefreshTime > REFRESH_INTERVAL)
            {
                if (debugUI && forceRefresh)
                    Debug.Log("[LobbyUI] Forced UI refresh");
                    
                RefreshUI();
                forceRefresh = false;
                lastRefreshTime = Time.time;
            }
        }
        
        private void RequestPlayerList()
        {
            if (NetworkClient.active)
            {
                if (debugUI)
                    Debug.Log("[LobbyUI] Sending game state request");
                    
                // Send a request for game state update
                NetworkClient.Send(new EpochLegends.Core.Network.GameStateRequestMessage());
            }
        }
        
        public void RefreshUI()
        {
            if (debugUI)
                Debug.Log("[LobbyUI] Refreshing UI");
                
            UpdateServerInfo();
            RefreshPlayerList();
        }
        
        private void UpdateServerInfo()
        {
            if (EpochNetworkManager.Instance != null)
            {
                if (serverNameText != null)
                {
                    serverNameText.text = EpochNetworkManager.Instance.ServerName;
                    
                    if (debugUI)
                        Debug.Log($"[LobbyUI] Set server name to: {serverNameText.text}");
                }
            }
            
            if (playerCountText != null && GameManager.Instance != null)
            {
                int playerCount = GameManager.Instance.ConnectedPlayerCount;
                if (EpochNetworkManager.Instance != null)
                {
                    playerCountText.text = $"Players: {playerCount} / {EpochNetworkManager.Instance.MaxPlayers}";
                }
                else
                {
                    playerCountText.text = $"Players: {playerCount}";
                }
                
                if (debugUI)
                    Debug.Log($"[LobbyUI] Updated player count: {playerCount}");
            }
        }
        
        private void RefreshPlayerList()
        {
            // Start by clearing all existing entries
            ClearPlayerList();
            
            // Get player list from GameManager
            if (GameManager.Instance != null)
            {
                var players = GameManager.Instance.ConnectedPlayers;
                
                if (debugUI)
                    Debug.Log($"[LobbyUI] Refreshing player list. Connected players: {players.Count}");
                
                foreach (var playerEntry in players)
                {
                    uint netId = playerEntry.Key;
                    var playerInfo = playerEntry.Value;
                    
                    // Get player name - either from NetworkIdentity or fallback to "Player X"
                    string playerName = "Player " + netId;
                    
                    if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
                    {
                        playerName = identity.gameObject.name;
                    }
                    
                    // Determine if this is the local player
                    bool isLocalPlayer = NetworkClient.localPlayer != null && 
                                         NetworkClient.localPlayer.netId == netId;
                    
                    // Update my ready status if this is me
                    if (isLocalPlayer)
                    {
                        bool wasReady = isReady;
                        isReady = playerInfo.IsReady;
                        
                        if (wasReady != isReady && readyButton != null)
                        {
                            readyButton.GetComponentInChildren<Text>().text = isReady ? "Not Ready" : "Ready";
                            
                            if (debugUI)
                                Debug.Log($"[LobbyUI] Updated local player ready button: {readyButton.GetComponentInChildren<Text>().text}");
                        }
                    }
                    
                    // Create player entry in UI
                    CreatePlayerEntry(
                        netId, 
                        playerName, 
                        playerInfo.TeamId, 
                        isLocalPlayer, 
                        playerInfo.IsReady
                    );
                }
            }
            else if (debugUI)
            {
                Debug.LogWarning("[LobbyUI] GameManager is null");
            }
        }
        
        private void ClearPlayerList()
        {
            foreach (var entry in playerEntries.Values)
            {
                Destroy(entry);
            }
            
            playerEntries.Clear();
            
            // Also clear any leftover children in the containers
            if (team1Container != null)
                ClearContainer(team1Container);
                
            if (team2Container != null)
                ClearContainer(team2Container);
                
            if (debugUI)
                Debug.Log("[LobbyUI] Cleared player list");
        }
        
        private void ClearContainer(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
        
        private void CreatePlayerEntry(uint netId, string playerName, int teamId, bool isLocalPlayer, bool isPlayerReady)
        {
            Transform container = teamId == 1 ? team1Container : team2Container;
            
            if (container != null && playerEntryPrefab != null)
            {
                GameObject entry = Instantiate(playerEntryPrefab, container);
                playerEntries[netId] = entry;
                
                // Setup entry UI
                Text nameText = entry.GetComponentInChildren<Text>();
                if (nameText != null)
                    nameText.text = playerName + (isLocalPlayer ? " (You)" : "");
                    
                // Setup ready indicator
                Transform readyIndicator = entry.transform.Find("ReadyIndicator");
                if (readyIndicator != null)
                    readyIndicator.gameObject.SetActive(isPlayerReady);
                    
                if (debugUI)
                    Debug.Log($"[LobbyUI] Created player entry for {playerName} (NetID: {netId}, Team: {teamId}, Ready: {isPlayerReady})");
            }
            else if (debugUI)
            {
                Debug.LogWarning($"[LobbyUI] Cannot create player entry - container or prefab missing for team {teamId}");
            }
        }
        
        private void OnReadyClicked()
        {
            isReady = !isReady;
            
            // Update UI
            if (readyButton != null)
                readyButton.GetComponentInChildren<Text>().text = isReady ? "Not Ready" : "Ready";
                
            // Update ready status for local player
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                // Send ready status to server
                CmdSetPlayerReady(isReady);
            }
            
            // Update player entry
            if (NetworkClient.localPlayer != null)
            {
                uint netId = NetworkClient.localPlayer.netId;
                if (playerEntries.TryGetValue(netId, out GameObject entry))
                {
                    Transform readyIndicator = entry.transform.Find("ReadyIndicator");
                    if (readyIndicator != null)
                        readyIndicator.gameObject.SetActive(isReady);
                }
            }
            
            if (debugUI)
                Debug.Log($"[LobbyUI] Ready button clicked. New state: {isReady}");
        }
        
        private void CmdSetPlayerReady(bool ready)
        {
            // Send ready message to server
            NetworkClient.Send(new ReadyStateMessage { isReady = ready });
            
            if (debugUI)
                Debug.Log($"[LobbyUI] Sent ready state to server: {ready}");
        }
        
        private void OnSwitchTeamClicked()
        {
            if (debugUI)
                Debug.Log("[LobbyUI] Team switch requested");
                
            // Check if TeamAssignment exists in the scene
            var teamAssignment = FindObjectOfType<Systems.Team.Assignment.TeamAssignment>();
            if (teamAssignment != null)
            {
                // Get current team
                int currentTeam = 1; // Default
                
                if (NetworkClient.localPlayer != null)
                {
                    uint netId = NetworkClient.localPlayer.netId;
                    if (GameManager.Instance != null && 
                        GameManager.Instance.ConnectedPlayers.TryGetValue(netId, out PlayerInfo playerInfo))
                    {
                        currentTeam = playerInfo.TeamId;
                    }
                }
                
                // Switch to other team
                int newTeam = (currentTeam == 1) ? 2 : 1;
                teamAssignment.RequestTeamChange(newTeam);
                
                // Force refresh after a short delay to give time for server to process
                Invoke(nameof(ForceUIRefresh), 0.5f);
            }
            else
            {
                Debug.LogWarning("[LobbyUI] TeamAssignment not found in scene");
            }
        }
        
        private void ForceUIRefresh()
        {
            forceRefresh = true;
        }
        
        private void OnLeaveClicked()
        {
            if (debugUI)
                Debug.Log("[LobbyUI] Leave button clicked");
                
            // Leave the game
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.DisconnectGame();
            }
            else if (NetworkManager.singleton != null)
            {
                if (NetworkServer.active)
                    NetworkManager.singleton.StopHost();
                else
                    NetworkManager.singleton.StopClient();
            }
        }
    }
}