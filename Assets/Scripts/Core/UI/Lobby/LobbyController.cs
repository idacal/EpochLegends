using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Systems.Team.Manager;
using EpochLegends.Systems.Team.Assignment;

namespace EpochLegends.UI.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        [Header("Server Info")]
        [SerializeField] private TextMeshProUGUI serverNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private GameObject waitingForPlayersText;

        [Header("Team Panels")]
        [SerializeField] private Transform[] teamContainers; // Asigna contenedores para cada equipo (por ejemplo, 2)
        [SerializeField] private TextMeshProUGUI[] teamNameTexts;
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Player Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button teamSwitchButton;
        [SerializeField] private Button disconnectButton;
        
        [Header("Debug")]
        [SerializeField] private bool debugUpdates = true;

        // Referencias a otros managers
        private TeamManager teamManager;
        private TeamAssignment teamAssignment;
        private EpochLegends.GameManager gameManager;

        // Estado local
        private bool isPlayerReady = false;
        private bool isLocalPlayerHost = false;

        // Cache de entradas de jugador en la UI
        private Dictionary<uint, PlayerUIInfo> currentPlayerInfos = new Dictionary<uint, PlayerUIInfo>();
        private Dictionary<uint, GameObject> playerEntries = new Dictionary<uint, GameObject>();
        
        // Tracking cuando se actualizó por última vez la UI
        private float lastUIUpdateTime = 0f;
        private float uiUpdateInterval = 0.5f; // Actualizar la UI cada medio segundo como máximo

        // Definición común de PlayerInfo para la UI
        public struct PlayerUIInfo
        {
            public uint NetId;
            public string PlayerName;
            public int TeamId;
            public bool IsReady;
            public bool IsHost;
        }

        private void Awake()
        {
            FindManagers();
            
            // Configurar listeners de botones
            SetupButtonListeners();
        }
        
        private void FindManagers()
        {
            teamManager = FindObjectOfType<TeamManager>();
            teamAssignment = FindObjectOfType<TeamAssignment>();
            gameManager = FindObjectOfType<EpochLegends.GameManager>();

            if (teamManager == null || gameManager == null)
            {
                Debug.LogError("No se encontraron los managers requeridos en la escena.");
            }
        }
        
        private void SetupButtonListeners()
        {
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
            // Determinar si somos host
            isLocalPlayerHost = NetworkServer.active;

            // Actualizar UI según el rol
            UpdateHostUI();
            
            // Configurar información del servidor
            UpdateServerInfo();
            
            // Inicializar nombres de equipos
            UpdateTeamNames();

            // Estado inicial de botones
            UpdateReadyButtonText();
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
            
            // Register network message handlers
            if (NetworkClient.active)
            {
                NetworkClient.RegisterHandler<ReadyStateMessage>(OnReadyStateMessage);
            }
            
            // Solicitar actualización de UI
            PerformFullUIRefresh();
        }

        private void Update()
        {
            // Limitar frecuencia de actualización de UI para no saturar
            if (Time.time - lastUIUpdateTime > uiUpdateInterval)
            {
                UpdatePlayerCount();
                UpdateStartButtonState();
                UpdateWaitingForPlayersText();
                RefreshPlayerList();
                
                lastUIUpdateTime = Time.time;
            }
        }
        
        // Método público para permitir que otros componentes soliciten una actualización de UI
        public void RefreshUI()
        {
            if (debugUpdates)
                Debug.Log("[LobbyController] RefreshUI called - performing full UI refresh");
                
            PerformFullUIRefresh();
        }
        
        private void PerformFullUIRefresh()
        {
            UpdateServerInfo();
            UpdateTeamNames();
            UpdatePlayerCount();
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
            RefreshPlayerList(true); // Force complete refresh
            
            lastUIUpdateTime = Time.time;
        }

        #region UI Updates

        private void UpdateHostUI()
        {
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(isLocalPlayerHost);
        }
        
        private void UpdateServerInfo()
        {
            if (serverNameText != null)
            {
                string serverName = GetServerName();
                serverNameText.text = $"Server: {serverName}";
                
                if (debugUpdates)
                    Debug.Log($"[LobbyController] Updated server name: {serverName}");
            }
        }
        
        private string GetServerName()
        {
            // Attempt to get server name from NetworkManager
            if (Mirror.NetworkManager.singleton is EpochLegends.Core.Network.Manager.EpochNetworkManager networkManager)
            {
                return networkManager.ServerName;
            }
            
            return "Epoch Legends Server";
        }

        private void UpdateTeamNames()
        {
            if (teamManager == null) return;
            
            for (int i = 0; i < teamContainers.Length && i < teamNameTexts.Length; i++)
            {
                var config = teamManager.GetTeamConfig(i + 1); // Suponiendo que los IDs de equipo inician en 1
                if (config != null)
                {
                    teamNameTexts[i].text = config.teamName;
                    teamNameTexts[i].color = config.teamColor;
                }
            }
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && gameManager != null)
            {
                int count = gameManager.ConnectedPlayerCount;
                playerCountText.text = $"Players: {count}";
                
                if (debugUpdates)
                    Debug.Log($"[LobbyController] Updated player count: {count}");
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
                bool needMore = gameManager.ConnectedPlayerCount < 2;
                waitingForPlayersText.SetActive(needMore);
            }
        }

        #endregion

        #region Actualización de la Lista de Jugadores

        // Este método consulta a GameManager la lista de conexiones y reconstruye la lista de UI.
        private void RefreshPlayerList(bool forceFullRefresh = false)
        {
            if (gameManager == null) return;

            // Construimos una lista de PlayerInfo "local" a partir de los jugadores conectados en GameManager
            Dictionary<uint, PlayerUIInfo> updatedInfos = new Dictionary<uint, PlayerUIInfo>();

            // Get the ConnectedPlayers dictionary from GameManager - this now uses uint (NetID) as keys
            var connectedPlayers = gameManager.ConnectedPlayers;
            
            if (debugUpdates && forceFullRefresh)
                Debug.Log($"[LobbyController] Refreshing player list. Connected players: {connectedPlayers.Count}");
            
            foreach (var kvp in connectedPlayers)
            {
                uint netId = kvp.Key;
                string playerName = "Player " + netId;
                
                // Try to get a better name from NetworkIdentity
                if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
                {
                    playerName = identity.gameObject.name;
                }
                
                bool isHost = NetworkServer.active && NetworkClient.localPlayer != null && 
                              NetworkClient.localPlayer.netId == netId;
                              
                int teamId = kvp.Value.TeamId;
                bool ready = kvp.Value.IsReady;

                updatedInfos[netId] = new PlayerUIInfo
                {
                    NetId = netId,
                    PlayerName = playerName,
                    TeamId = teamId,
                    IsReady = ready,
                    IsHost = isHost
                };
                
                // Update local player's ready state
                if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.netId == netId)
                {
                    bool oldReady = isPlayerReady;
                    isPlayerReady = ready;
                    
                    if (oldReady != isPlayerReady)
                    {
                        UpdateReadyButtonText();
                    }
                }
            }

            // Actualizamos la UI: agregamos nuevas entradas y removemos las que ya no están
            // Agregar o actualizar entradas
            foreach (var info in updatedInfos)
            {
                if (playerEntries.ContainsKey(info.Key))
                {
                    // Update existing entry
                    UpdatePlayerEntry(info.Key, info.Value);
                }
                else
                {
                    // Create new entry
                    CreatePlayerEntry(info.Value);
                    
                    if (debugUpdates)
                        Debug.Log($"[LobbyController] Created UI entry for player {info.Key}");
                }
            }

            // Remover entradas que ya no están
            List<uint> toRemove = playerEntries.Keys.Except(updatedInfos.Keys).ToList();
            foreach (var netId in toRemove)
            {
                if (playerEntries.TryGetValue(netId, out GameObject entry))
                {
                    Destroy(entry);
                    
                    if (debugUpdates)
                        Debug.Log($"[LobbyController] Removed UI entry for player {netId}");
                }
                playerEntries.Remove(netId);
            }
            
            // Update current player infos
            currentPlayerInfos = new Dictionary<uint, PlayerUIInfo>(updatedInfos);
        }

        private void UpdatePlayerEntry(uint netId, PlayerUIInfo info)
        {
            if (playerEntries.TryGetValue(netId, out GameObject entry))
            {
                // Update player entry UI based on your prefab structure
                // For example, update text components, ready indicator, etc.
                Text entryText = entry.GetComponentInChildren<Text>();
                if (entryText != null)
                {
                    entryText.text = info.PlayerName + (info.IsHost ? " (Host)" : "") + 
                                    (info.IsReady ? " - Ready" : " - Not Ready");
                }
                
                // If you have separate ready indicator, update it too
                Transform readyIndicator = entry.transform.Find("ReadyIndicator");
                if (readyIndicator != null)
                {
                    readyIndicator.gameObject.SetActive(info.IsReady);
                }
            }
        }

        private void CreatePlayerEntry(PlayerUIInfo info)
        {
            // Asigna el contenedor según el equipo (suponiendo que teamContainers[0] es equipo 1 y [1] equipo 2)
            int teamIndex = info.TeamId - 1;
            if (teamIndex < 0 || teamIndex >= teamContainers.Length)
                teamIndex = 0;

            if (playerEntryPrefab != null && teamContainers[teamIndex] != null)
            {
                GameObject entry = Instantiate(playerEntryPrefab, teamContainers[teamIndex]);
                
                // Update the entry based on your prefab structure
                Text entryText = entry.GetComponentInChildren<Text>();
                if (entryText != null)
                {
                    entryText.text = info.PlayerName + (info.IsHost ? " (Host)" : "") +
                                    (info.IsReady ? " - Ready" : " - Not Ready");
                }
                
                // If you have a ready indicator, update it
                Transform readyIndicator = entry.transform.Find("ReadyIndicator");
                if (readyIndicator != null)
                {
                    readyIndicator.gameObject.SetActive(info.IsReady);
                }
                
                playerEntries[info.NetId] = entry;
            }
        }

        private bool CheckAllPlayersReady()
        {
            if (gameManager == null) return false;
            
            // Now checking the updated ConnectedPlayers dictionary by NetId
            foreach (var playerInfo in gameManager.ConnectedPlayers.Values)
            {
                if (!playerInfo.IsReady)
                    return false;
            }
            return true;
        }

        #endregion

        #region Handlers de Botones

        private void OnReadyButtonClicked()
        {
            isPlayerReady = !isPlayerReady;
            UpdateReadyButtonText();

            // Enviar el estado "ready" al servidor
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                if (debugUpdates)
                    Debug.Log($"[LobbyController] Sending ready state: {isPlayerReady}");
                    
                // Sending ready state message
                NetworkClient.Send(new ReadyStateMessage
                {
                    isReady = isPlayerReady
                });
            }
        }

        private void OnStartGameClicked()
        {
            if (!isLocalPlayerHost) return;

            if (gameManager != null && gameManager.ConnectedPlayerCount >= 2)
            {
                if (CheckAllPlayersReady())
                {
                    if (debugUpdates)
                        Debug.Log("[LobbyController] Starting hero selection phase");
                        
                    gameManager.StartHeroSelection();
                }
                else
                {
                    Debug.Log("No se puede iniciar el juego: no todos los jugadores están listos.");
                }
            }
            else
            {
                Debug.Log("No se puede iniciar el juego: se necesitan al menos 2 jugadores.");
            }
        }

        private void OnTeamSwitchClicked()
        {
            // Aquí se podría solicitar un cambio de equipo mediante TeamAssignment
            if (teamAssignment != null && NetworkClient.localPlayer != null)
            {
                int currentTeam = GetLocalPlayerTeam();
                int newTeam = (currentTeam == 1) ? 2 : 1;
                
                if (debugUpdates)
                    Debug.Log($"[LobbyController] Requesting team change from {currentTeam} to {newTeam}");
                    
                teamAssignment.RequestTeamChange(newTeam);
            }
            else
            {
                Debug.Log("No se encontró el TeamAssignment o no hay jugador local.");
            }
        }

        private void OnDisconnectClicked()
        {
            if (NetworkClient.active)
            {
                if (debugUpdates)
                    Debug.Log("[LobbyController] Disconnecting from server");
                    
                if (NetworkServer.active)
                {
                    NetworkManager.singleton.StopHost();
                }
                else
                {
                    NetworkManager.singleton.StopClient();
                }
            }
        }

        private int GetLocalPlayerTeam()
        {
            if (gameManager == null || NetworkClient.localPlayer == null) return 1;
            
            uint localPlayerNetId = NetworkClient.localPlayer.netId;
            if (gameManager.ConnectedPlayers.TryGetValue(localPlayerNetId, out PlayerInfo playerInfo))
            {
                return playerInfo.TeamId;
            }
            
            return 1;
        }
        
        private void OnReadyStateMessage(ReadyStateMessage message)
        {
            // This is a client-side handler
            if (debugUpdates)
                Debug.Log($"[LobbyController] Ready state message received: {message.isReady}");
        }

        #endregion
    }

    // Messages are already defined in GameManager.cs, but keeping this for reference
    /*
    // Mensajes para la comunicación de estado "ready" y cambio de equipo
    public struct ReadyStateMessage : NetworkMessage
    {
        public bool isReady;
    }
    
    public struct TeamChangeMessage : NetworkMessage
    {
        public int teamId;
    }
    */
}