using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Network.Manager;
using EpochLegends;

namespace EpochLegends.Core.UI.Lobby
{
    public class LobbyUI : NetworkBehaviour
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
            RefreshUI();
        }
        
        public void RefreshUI()
        {
            UpdateServerInfo();
            RefreshPlayerList();
        }
        
        public void RefreshPlayerList()
        {
            // Clear existing entries
            ClearPlayerList();
            
            // Get player list from GameManager
            if (GameManager.Instance != null && GameManager.Instance.ConnectedPlayers != null)
            {
                // Solo usamos el jugador local para esta demostración
                if (NetworkClient.connection != null && NetworkClient.localPlayer != null)
                {
                    var conn = NetworkClient.connection;
                    if (conn != null && conn.identity != null)
                    {
                        // Create player entry
                        CreatePlayerEntry(conn.identity.netId, "Player " + conn.identity.netId, 
                                        1, // Equipo 1 para demostración
                                        true); // Es el jugador local
                    }
                }
            }
        }
        
        private void UpdateServerInfo()
        {
            if (EpochNetworkManager.Instance != null)
            {
                if (serverNameText != null)
                    serverNameText.text = EpochNetworkManager.Instance.ServerName;
            }
            
            if (playerCountText != null && GameManager.Instance != null)
            {
                playerCountText.text = $"Players: {GameManager.Instance.ConnectedPlayerCount} / {EpochNetworkManager.Instance.MaxPlayers}";
            }
        }
        
        private void ClearPlayerList()
        {
            foreach (var entry in playerEntries.Values)
            {
                Destroy(entry);
            }
            
            playerEntries.Clear();
        }
        
        private void CreatePlayerEntry(uint netId, string playerName, int teamId, bool isLocalPlayer)
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
                    readyIndicator.gameObject.SetActive(isLocalPlayer ? isReady : false);
            }
        }
        
        private void OnReadyClicked()
        {
            isReady = !isReady;
            
            // Update UI
            if (readyButton != null)
                readyButton.GetComponentInChildren<Text>().text = isReady ? "Not Ready" : "Ready";
                
            // Update ready status for local player
            if (GameManager.Instance != null && NetworkClient.localPlayer != null)
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
        }
        
        [Command]
        private void CmdSetPlayerReady(bool ready)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayerReady(NetworkClient.connection, ready);
            }
        }
        
        private void OnSwitchTeamClicked()
        {
            // Request team switch
            // En una implementación real, usarías TeamAssignment para cambiar equipos
            // Por ahora, solo actualizamos la UI
            RefreshPlayerList();
        }
        
        private void OnLeaveClicked()
        {
            // Leave the game
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.DisconnectGame();
            }
        }
    }
}