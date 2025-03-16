using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Systems.Team.Manager;
using EpochLegends.Systems.Team.Assignment;
using EpochLegends.Core.Network;

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
            // First try direct find
            teamManager = FindObjectOfType<EpochLegends.Systems.Team.Manager.TeamManager>();
            teamAssignment = FindObjectOfType<EpochLegends.Systems.Team.Assignment.TeamAssignment>();
            gameManager = FindObjectOfType<EpochLegends.GameManager>();
            
            // If not found, try looking through the ManagersController
            if (teamManager == null || teamAssignment == null || gameManager == null)
            {
                EpochLegends.Core.ManagersController controller = FindObjectOfType<EpochLegends.Core.ManagersController>();
                if (controller != null)
                {
                    if (teamManager == null)
                        teamManager = controller.GetManager<EpochLegends.Systems.Team.Manager.TeamManager>("TeamManager");
                        
                    if (teamAssignment == null)
                        teamAssignment = controller.GetManager<EpochLegends.Systems.Team.Assignment.TeamAssignment>("TeamAssignment");
                        
                    if (gameManager == null)
                        gameManager = controller.GetManager<EpochLegends.GameManager>("GameManager");
                }
            }
            
            if (teamManager == null || teamAssignment == null || gameManager == null)
            {
                // Log which managers are missing
                string missing = "";
                if (teamManager == null) missing += "TeamManager, ";
                if (teamAssignment == null) missing += "TeamAssignment, ";
                if (gameManager == null) missing += "GameManager, ";
                
                Debug.LogError($"No se encontraron los managers requeridos en la escena: {missing.TrimEnd(',', ' ')}");
                
                // Try one last time with a delay
                StartCoroutine(DelayedManagerFind());
            }
            else
            {
                Debug.Log("All required managers found successfully.");
            }
        }

        private System.Collections.IEnumerator DelayedManagerFind()
        {
            Debug.Log("Attempting delayed manager find...");
            
            // Wait a short time to ensure DontDestroyOnLoad objects are properly initialized
            yield return new WaitForSeconds(0.5f);
            
            // Try again
            teamManager = FindObjectOfType<EpochLegends.Systems.Team.Manager.TeamManager>();
            teamAssignment = FindObjectOfType<EpochLegends.Systems.Team.Assignment.TeamAssignment>();
            gameManager = FindObjectOfType<EpochLegends.GameManager>();
            
            if (teamManager == null || teamAssignment == null || gameManager == null)
            {
                string missing = "";
                if (teamManager == null) missing += "TeamManager, ";
                if (teamAssignment == null) missing += "TeamAssignment, ";
                if (gameManager == null) missing += "GameManager, ";
                
                Debug.LogError($"Still couldn't find managers after delay: {missing.TrimEnd(',', ' ')}");
            }
            else
            {
                Debug.Log("All required managers found after delay!");
                
                // Since we found the managers after the delay, we need to setup the UI
                SetupButtonListeners();
                RefreshUI();
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
            // Subscribirse a eventos de cambios de datos
            EpochLegends.GameManager.OnPlayerDataChanged += OnPlayerDataChanged;
            
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
            
            // También programar un refresco retrasado para asegurar que la UI se actualice
            // después de que toda la información de red esté disponible
            Invoke(nameof(RequestSyncData), 0.5f);
            Invoke(nameof(RequestSyncData), 1.5f);
        }
        
        private void RequestSyncData()
        {
            if (debugUpdates)
                Debug.Log("[LobbyController] Requesting sync data");
                
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
            if (gameManager != null)
                EpochLegends.GameManager.OnPlayerDataChanged -= OnPlayerDataChanged;
        }
        
        private void OnPlayerDataChanged()
        {
            if (debugUpdates)
                Debug.Log("[LobbyController] Player data changed event received - refreshing UI");
                
            // Actualizar UI cuando los datos de jugadores cambian
            PerformFullUIRefresh();
        }

        // Método público para permitir que otros componentes soliciten una actualización de UI
        public void RefreshUI()
        {
            if (debugUpdates)
                Debug.Log("[LobbyController] RefreshUI called - performing full UI refresh");
                
            PerformFullUIRefresh();
        }
        
        // Nuevo método para actualizar la UI desde los datos del sincronizador
        public void UpdateFromSyncData(string serverName, int playerCount, int maxPlayers, 
                                     List<LobbyDataSynchronizer.SyncPlayerData> players)
        {
            if (debugUpdates)
                Debug.Log($"[LobbyController] Updating from sync data: {serverName}, Players: {playerCount}/{maxPlayers}, Player count: {players.Count}");
                
            // Actualizar información del servidor
            if (serverNameText != null)
                serverNameText.text = $"Server: {serverName}";
                
            if (playerCountText != null)
                playerCountText.text = $"Players: {playerCount}";
                
            // Actualizar visibilidad del texto "waiting for players"
            if (waitingForPlayersText != null)
                waitingForPlayersText.SetActive(playerCount < 2);
                
            // Actualizar estado del botón de inicio
            UpdateStartButtonState();
            
            // Convertir los datos sincronizados a nuestro formato interno
            Dictionary<uint, PlayerUIInfo> syncedPlayerInfos = new Dictionary<uint, PlayerUIInfo>();
            
            foreach (var playerData in players)
            {
                bool isHost = NetworkServer.active && NetworkClient.localPlayer != null && 
                              NetworkClient.localPlayer.netId == playerData.netId;
                              
                syncedPlayerInfos[playerData.netId] = new PlayerUIInfo
                {
                    NetId = playerData.netId,
                    PlayerName = playerData.playerName,
                    TeamId = playerData.teamId,
                    IsReady = playerData.isReady,
                    IsHost = isHost
                };
                
                // Si este es el jugador local, actualizar estado ready
                if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.netId == playerData.netId)
                {
                    bool oldReady = isPlayerReady;
                    isPlayerReady = playerData.isReady;
                    
                    if (oldReady != isPlayerReady)
                    {
                        UpdateReadyButtonText();
                    }
                }
            }
            
            // Refrescar la lista de jugadores con los datos sincronizados
            RefreshPlayerListFromSync(syncedPlayerInfos);
        }
        
        private void PerformFullUIRefresh()
        {
            // Verificar que estamos en la escena correcta antes de actualizar la UI
            // en caso de que este componente no se haya destruido durante una transición de escena
            if (!gameObject.activeInHierarchy) return;
            
            UpdateServerInfo();
            UpdateTeamNames();
            UpdatePlayerCount();
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
            RefreshPlayerList(true); // Force complete refresh
            
            if (debugUpdates)
                Debug.Log("[LobbyController] Full UI refresh completed");
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
        
        // Nuevo método para actualizar la lista desde los datos sincronizados
        private void RefreshPlayerListFromSync(Dictionary<uint, PlayerUIInfo> updatedInfos)
        {
            if (debugUpdates)
                Debug.Log($"[LobbyController] Refreshing player list from sync data. Players: {updatedInfos.Count}");
                
            // Limpiar todas las entradas actuales
            foreach (var entry in playerEntries)
            {
                Destroy(entry.Value);
            }
            playerEntries.Clear();
            
            // Limpiar contenedores
            foreach (var container in teamContainers)
            {
                if (container != null)
                {
                    for (int i = container.childCount - 1; i >= 0; i--)
                    {
                        Destroy(container.GetChild(i).gameObject);
                    }
                }
            }

            // Crear nuevas entradas para cada jugador
            foreach (var info in updatedInfos)
            {
                CreatePlayerEntry(info.Value);
            }
            
            // Actualizar nuestro cache de información de jugadores
            currentPlayerInfos = updatedInfos;
        }

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

            // Si estamos realizando un refresco completo o hay cambios, actualizamos la UI
            if (forceFullRefresh || !ArePlayerInfosEqual(currentPlayerInfos, updatedInfos))
            {
                if (debugUpdates)
                    Debug.Log("[LobbyController] Player list has changed - updating UI");
                    
                RefreshPlayerListFromSync(updatedInfos);
            }
        }

        private bool ArePlayerInfosEqual(Dictionary<uint, PlayerUIInfo> a, Dictionary<uint, PlayerUIInfo> b)
        {
            if (a.Count != b.Count)
                return false;
                
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out PlayerUIInfo otherInfo))
                    return false;
                    
                if (kvp.Value.TeamId != otherInfo.TeamId || 
                    kvp.Value.IsReady != otherInfo.IsReady ||
                    kvp.Value.PlayerName != otherInfo.PlayerName)
                    return false;
            }
            
            return true;
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
                
                if (debugUpdates)
                    Debug.Log($"[LobbyController] Created UI entry for player {info.NetId} on team {info.TeamId}");
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
                
                // Solicitar actualización después de un breve retraso
                Invoke(nameof(RequestSyncData), 0.5f);
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
}