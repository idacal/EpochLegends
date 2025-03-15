using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Systems.Team.Manager;
using EpochLegends.Systems.Team.Assignment;

namespace EpochLegends.UI.Lobby
{
    public class LobbyController : MonoBehaviour // Cambiado de NetworkBehaviour a MonoBehaviour
    {
        [Header("Server Info")]
        [SerializeField] private TextMeshProUGUI serverNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private GameObject waitingForPlayersText;

        [Header("Team Panels")]
        [SerializeField] private Transform[] teamContainers;
        [SerializeField] private TextMeshProUGUI[] teamNameTexts;
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Player Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button teamSwitchButton;
        [SerializeField] private Button disconnectButton;

        // References
        private NetworkManager networkManager;
        private TeamManager teamManager;
        private TeamAssignment teamAssignment;
        private GameManager gameManager;

        // State tracking
        private bool isPlayerReady = false;
        private bool isLocalPlayerHost = false;

        // Player entries cache
        private Dictionary<uint, PlayerEntry> playerEntries = new Dictionary<uint, PlayerEntry>();

        private void Awake()
        {
            // Find managers
            networkManager = NetworkManager.singleton;
            teamManager = FindObjectOfType<TeamManager>();
            teamAssignment = FindObjectOfType<TeamAssignment>();
            gameManager = FindObjectOfType<GameManager>();

            if (networkManager == null || teamManager == null || gameManager == null)
            {
                Debug.LogError("Required managers not found in scene!");
            }

            // Setup button listeners
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyButtonClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);

            if (teamSwitchButton != null)
                teamSwitchButton.onClick.AddListener(OnTeamSwitchClicked);

            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        private void Start()
        {
            // Determine if we're the host
            isLocalPlayerHost = NetworkServer.active;
            
            // Setup UI based on role
            if (isLocalPlayerHost)
            {
                // Show start game button for host
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(true);
            }
            else
            {
                // Hide start game button for clients
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(false);
            }

            // Setup server info
            if (serverNameText != null)
            {
                serverNameText.text = $"Server: {GetServerName()}";
            }

            // Initialize team names
            UpdateTeamNames();

            // Initial UI state
            UpdateReadyButtonText();
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
        }

        private string GetServerName()
        {
            // Simplify for now - you can customize based on your implementation
            return "Epoch Legends Server";
        }

        private void Update()
        {
            // Update player count
            UpdatePlayerCount();
            
            // Update UI states
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
        }

        #region UI Updates

        private void UpdateTeamNames()
        {
            if (teamManager == null) return;

            // Set team names in UI
            for (int i = 0; i < teamContainers.Length && i < teamNameTexts.Length; i++)
            {
                TeamConfig config = teamManager.GetTeamConfig(i + 1); // Assuming team IDs start at 1
                if (config != null)
                {
                    teamNameTexts[i].text = config.teamName;
                    // Could also set team colors here
                    teamNameTexts[i].color = config.teamColor;
                }
            }
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && gameManager != null)
            {
                int playerCount = gameManager.ConnectedPlayerCount;
                playerCountText.text = $"Players: {playerCount}";
            }
        }

        private void UpdateReadyButtonText()
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = isPlayerReady ? "NOT READY" : "READY";
            }
        }

        private void UpdateStartButtonState()
        {
            if (startGameButton != null && isLocalPlayerHost && gameManager != null)
            {
                bool canStart = gameManager.ConnectedPlayerCount >= 2 && CheckAllPlayersReady();
                startGameButton.interactable = canStart;
            }
        }

        private void UpdateWaitingForPlayersText()
        {
            if (waitingForPlayersText != null && gameManager != null)
            {
                bool needMorePlayers = gameManager.ConnectedPlayerCount < 2;
                waitingForPlayersText.SetActive(needMorePlayers);
            }
        }

        #endregion

        #region Player Management

        public void UpdatePlayerList(PlayerInfo[] players)
        {
            // Clear current entries if needed
            if (playerEntries.Count > 0)
            {
                HashSet<uint> currentPlayers = new HashSet<uint>();
                
                // Update existing entries and add new ones
                foreach (var playerInfo in players)
                {
                    uint netId = playerInfo.NetId;
                    currentPlayers.Add(netId);
                    
                    if (playerEntries.TryGetValue(netId, out PlayerEntry entry))
                    {
                        // Update existing entry
                        entry.UpdateInfo(playerInfo);
                    }
                    else
                    {
                        // Create new entry
                        CreatePlayerEntry(playerInfo);
                    }
                }
                
                // Remove entries for players who left
                List<uint> toRemove = new List<uint>();
                foreach (var entry in playerEntries)
                {
                    if (!currentPlayers.Contains(entry.Key))
                    {
                        toRemove.Add(entry.Key);
                    }
                }
                
                foreach (var netId in toRemove)
                {
                    RemovePlayerEntry(netId);
                }
            }
            else
            {
                // Initial population
                foreach (var playerInfo in players)
                {
                    CreatePlayerEntry(playerInfo);
                }
            }

            // Actualizar UI
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
        }

        private void CreatePlayerEntry(PlayerInfo playerInfo)
        {
            if (teamContainers == null || teamContainers.Length < 2) return;

            // Determine which team container to use (0-based array index)
            int teamIndex = playerInfo.TeamId - 1;
            
            // Validate team index
            if (teamIndex < 0 || teamIndex >= teamContainers.Length)
            {
                teamIndex = 0;
            }

            // Create the entry
            GameObject entryObj = Instantiate(playerEntryPrefab, teamContainers[teamIndex]);
            PlayerEntry entry = entryObj.GetComponent<PlayerEntry>();
            
            if (entry != null)
            {
                entry.Initialize(playerInfo);
                playerEntries[playerInfo.NetId] = entry;
            }
        }

        private void RemovePlayerEntry(uint netId)
        {
            if (playerEntries.TryGetValue(netId, out PlayerEntry entry))
            {
                if (entry != null)
                {
                    Destroy(entry.gameObject);
                }
                playerEntries.Remove(netId);
            }
        }

        // Actualiza el estado de un jugador específico
        public void UpdatePlayerReadyState(uint playerNetId, bool isReady)
        {
            // Actualizar la entrada del jugador si existe
            if (playerEntries.TryGetValue(playerNetId, out PlayerEntry entry))
            {
                PlayerInfo updatedInfo = new PlayerInfo
                {
                    NetId = playerNetId,
                    IsReady = isReady,
                    // Mantener valores existentes
                    PlayerName = entry.GetPlayerName(),
                    TeamId = entry.GetTeamId(),
                    IsHost = entry.IsHost()
                };
                
                entry.UpdateInfo(updatedInfo);
            }
            
            // Si este es el jugador local, actualiza el estado local
            if (IsLocalPlayer(playerNetId))
            {
                isPlayerReady = isReady;
                UpdateReadyButtonText();
            }
            
            // Actualiza UI
            UpdateStartButtonState();
        }

        #endregion

        #region Button Handlers

        private void OnReadyButtonClicked()
        {
            // Toggle ready state
            isPlayerReady = !isPlayerReady;
            
            // Update button text
            UpdateReadyButtonText();
            
            // Get local player NetId
            uint localPlayerNetId = GetLocalPlayerNetId();
            
            // Send ready status to server if we have a valid NetworkConnection
            if (NetworkClient.active && NetworkClient.connection != null)
            {
                NetworkClient.connection.Send(new ReadyStateMessage
                {
                    isReady = isPlayerReady
                });
                
                // Update locally immediately for responsiveness
                UpdatePlayerReadyState(localPlayerNetId, isPlayerReady);
            }
        }

        private void OnStartGameClicked()
        {
            if (!isLocalPlayerHost) return;

            // Check if at least 2 players are connected
            if (gameManager != null && gameManager.ConnectedPlayerCount >= 2)
            {
                // Check if all players are ready
                bool allReady = CheckAllPlayersReady();
                
                if (allReady)
                {
                    // Start hero selection
                    gameManager.StartHeroSelection();
                }
                else
                {
                    // Show message that not all players are ready
                    Debug.Log("Cannot start game: Not all players are ready");
                }
            }
            else
            {
                // Show message that more players are needed
                Debug.Log("Cannot start game: Need at least 2 players");
            }
        }
        
        private bool CheckAllPlayersReady()
        {
            if (gameManager == null) return false;

            foreach (var playerInfo in gameManager.ConnectedPlayers.Values)
            {
                if (!playerInfo.IsReady)
                    return false;
            }
            
            return true;
        }

        private void OnTeamSwitchClicked()
        {
            if (!NetworkClient.active) return;

            // Request team switch to opposite team
            int currentTeam = GetLocalPlayerTeam();
            int newTeam = (currentTeam == 1) ? 2 : 1;
            
            if (teamAssignment != null)
            {
                teamAssignment.RequestTeamChange(newTeam);
            }
            else
            {
                // Fallback - send team change request directly if possible
                if (NetworkClient.connection != null)
                {
                    NetworkClient.connection.Send(new TeamChangeMessage
                    {
                        teamId = newTeam
                    });
                }
            }
        }

        private void OnDisconnectClicked()
        {
            if (NetworkClient.active)
            {
                // Si somos host
                if (NetworkServer.active)
                {
                    NetworkManager.singleton.StopHost();
                }
                // Si somos cliente
                else
                {
                    NetworkManager.singleton.StopClient();
                }
            }
        }

        #endregion

        #region Helper Methods

        private int GetLocalPlayerTeam()
        {
            if (gameManager == null) return 1;

            // Get local player's team
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                uint localPlayerNetId = NetworkClient.localPlayer.netId;
                
                foreach (var playerEntry in gameManager.ConnectedPlayers)
                {
                    if (playerEntry.Key.identity != null && 
                        playerEntry.Key.identity.netId == localPlayerNetId)
                    {
                        return playerEntry.Value.TeamId;
                    }
                }
            }

            return 1; // Default to team 1
        }
        
        private uint GetLocalPlayerNetId()
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                return NetworkClient.localPlayer.netId;
            }
            return 0;
        }
        
        private bool IsLocalPlayer(uint netId)
        {
            return NetworkClient.active && 
                   NetworkClient.localPlayer != null && 
                   NetworkClient.localPlayer.netId == netId;
        }

        #endregion
    }

    // Mensaje para comunicar el estado "ready"
    public struct ReadyStateMessage : NetworkMessage
    {
        public bool isReady;
    }
    
    // Mensaje para solicitar cambio de equipo
    public struct TeamChangeMessage : NetworkMessage
    {
        public int teamId;
    }

    // Helper struct to be used with player list updates
    public struct PlayerInfo
    {
        public uint NetId;
        public string PlayerName;
        public int TeamId;
        public bool IsReady;
        public bool IsHost;
    }

    // Component for individual player entries in the lobby
    public class PlayerEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image readyIndicator;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite notReadySprite;
        [SerializeField] private GameObject hostIndicator;

        private uint playerNetId;
        private string playerName;
        private int teamId;
        private bool isHost;

        public void Initialize(PlayerInfo info)
        {
            playerNetId = info.NetId;
            playerName = info.PlayerName;
            teamId = info.TeamId;
            isHost = info.IsHost;
            UpdateInfo(info);
        }

        public void UpdateInfo(PlayerInfo info)
        {
            // Actualizar datos internos
            playerName = info.PlayerName;
            teamId = info.TeamId;
            isHost = info.IsHost;

            // Update player name
            if (playerNameText != null)
            {
                playerNameText.text = info.PlayerName;
            }

            // Update ready status
            if (readyIndicator != null)
            {
                readyIndicator.sprite = info.IsReady ? readySprite : notReadySprite;
                readyIndicator.gameObject.SetActive(true);
            }

            // Update host indicator
            if (hostIndicator != null)
            {
                hostIndicator.SetActive(info.IsHost);
            }
        }

        // Getters para acceder a los datos internos
        public string GetPlayerName() => playerName;
        public int GetTeamId() => teamId;
        public bool IsHost() => isHost;
    }
}