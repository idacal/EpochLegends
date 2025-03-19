using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Network.Manager;
using EpochLegends;
using EpochLegends.Core.Network;

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
            
            // Subscribirse a eventos de cambios de datos
            GameManager.OnPlayerDataChanged += OnPlayerDataChanged;
            
            // Initial UI update
            if (debugUI)
                Debug.Log("[LobbyUI] Starting initial UI refresh");
                
            RefreshUI();
            
            // Request refresh from synchronizer
            Invoke(nameof(RequestSyncData), 0.5f);
        }
        
        private void RequestSyncData()
        {
            if (debugUI)
                Debug.Log("[LobbyUI] Requesting sync data");
                
            // Solicitar datos del sincronizador si existe
            var synchronizer = FindObjectOfType<LobbyDataSynchronizer>();
            if (synchronizer != null)
            {
                synchronizer.ForceRefresh();
            }
        }
        
        private void OnDestroy()
        {
            // Asegurarse de desuscribirse de eventos cuando el objeto se destruye
            GameManager.OnPlayerDataChanged -= OnPlayerDataChanged;
        }
        
        private void OnPlayerDataChanged()
        {
            if (debugUI)
                Debug.Log("[LobbyUI] Player data changed event received - refreshing UI");
                
            // Actualizar UI cuando los datos de jugadores cambian
            RefreshUI();
        }
        
        // Nuevo método para actualizar la UI desde los datos del sincronizador
        public void UpdateFromSyncData(string serverName, int playerCount, int maxPlayers, 
                                     List<LobbyDataSynchronizer.SyncPlayerData> players)
        {
            if (debugUI)
                Debug.Log($"[LobbyUI] Updating from sync data: {serverName}, Players: {playerCount}/{maxPlayers}, Player count: {players.Count}");
                
            // Actualizar información del servidor
            if (serverNameText != null)
                serverNameText.text = serverName;
                
            if (playerCountText != null)
                playerCountText.text = $"Players: {playerCount}/{maxPlayers}";
                
            // Limpiar entradas actuales
            ClearPlayerList();
            
            // Crear nuevas entradas con los datos sincronizados
            foreach (var playerData in players)
            {
                bool isLocalPlayer = NetworkClient.localPlayer != null && 
                                     NetworkClient.localPlayer.netId == playerData.netId;
                                     
                // Si este es el jugador local, actualizar estado ready
                if (isLocalPlayer)
                {
                    bool wasReady = isReady;
                    isReady = playerData.isReady;
                    
                    if (wasReady != isReady && readyButton != null)
                    {
                        Text buttonText = readyButton.GetComponentInChildren<Text>();
                        if (buttonText != null)
                            buttonText.text = isReady ? "Not Ready" : "Ready";
                            
                        if (debugUI)
                            Debug.Log($"[LobbyUI] Updated local player ready button: {buttonText.text}");
                    }
                }
                
                // Crear entrada en la UI
                CreatePlayerEntry(
                    playerData.netId,
                    playerData.playerName,
                    playerData.teamId,
                    isLocalPlayer,
                    playerData.isReady
                );
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
                    
                    // Usar el nombre del jugador de PlayerInfo
                    string playerName = playerInfo.PlayerName;
                    
                    // Si no hay nombre, utilizar un respaldo
                    if (string.IsNullOrEmpty(playerName))
                    {
                        playerName = "Player " + netId;
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
                            Text buttonText = readyButton.GetComponentInChildren<Text>();
                            if (buttonText != null)
                                buttonText.text = isReady ? "Not Ready" : "Ready";
                            
                            if (debugUI)
                                Debug.Log($"[LobbyUI] Updated local player ready button: {buttonText.text}");
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
            {
                Text buttonText = readyButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                    buttonText.text = isReady ? "Not Ready" : "Ready";
            }
                
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
                
                // Request refresh after a short delay
                Invoke(nameof(RequestSyncData), 0.5f);
            }
            else
            {
                Debug.LogWarning("[LobbyUI] TeamAssignment not found in scene");
            }
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