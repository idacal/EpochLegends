using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System;
using EpochLegends.Core.Network;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.Player;
using EpochLegends.Core.Hero;

namespace EpochLegends
{
    // Define ReadyStateMessage here for the GameManager
    public struct ReadyStateMessage : NetworkMessage
    {
        public bool isReady;
    }

    public enum GameState
    {
        Lobby,
        HeroSelection,
        Playing,
        GameOver
    }
    
    [Serializable]
    public struct PlayerInfo : IEquatable<PlayerInfo>
    {
        public uint NetId;
        public bool IsReady;
        public int TeamId;
        public string PlayerName;      // Añadido: nombre del jugador
        public string SelectedHeroId;
        public int Kills;
        public int Deaths;
        public int Assists;
        
        public bool Equals(PlayerInfo other)
        {
            return NetId == other.NetId;
        }
    }

    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        // Evento para notificar cuando los datos de jugadores cambian
        public delegate void PlayerDataChangedEvent();
        public static event PlayerDataChangedEvent OnPlayerDataChanged;

        [Header("Game Configuration")]
        [SerializeField] private float heroSelectionTime = 60f;
        [SerializeField] private float gameStartCountdown = 5f;
        
        [Header("Scene References")]
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string heroSelectionScene = "HeroSelection";
        [SerializeField] private string gameplayScene = "Gameplay";
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool persistAcrossScenes = true;

        [SyncVar(hook = nameof(OnGameStateChanged))]
        private GameState _currentState = GameState.Lobby;
        
        [SyncVar]
        private float _stateTimer = 0f;
        
        // Use SyncDictionary for player data
        private readonly SyncDictionary<uint, PlayerInfo> _connectedPlayers = new SyncDictionary<uint, PlayerInfo>();
        
        // Connection to netId mapping (server-side only)
        private Dictionary<NetworkConnection, uint> _connectionToNetId = new Dictionary<NetworkConnection, uint>();
        
        // Diccionario para almacenar selecciones de héroes para poder recuperarlas después de un cambio de escena
        private Dictionary<uint, string> _heroSelections = new Dictionary<uint, string>();

        public GameState CurrentState => _currentState;
        public int ConnectedPlayerCount => _connectedPlayers.Count;
        
        // Keep this as a property to maintain compatibility with existing code
        public Dictionary<uint, PlayerInfo> ConnectedPlayers 
        {
            get
            {
                // Create a copy to avoid external modification
                Dictionary<uint, PlayerInfo> copy = new Dictionary<uint, PlayerInfo>();
                foreach (var player in _connectedPlayers)
                {
                    copy[player.Key] = player.Value;
                }
                return copy;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            // Mantener entre escenas si está configurado así
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
                
                if (enableDebugLogs)
                    Debug.Log("[GameManager] Configurado para persistir entre escenas");
            }
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Initialized");
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Eliminar cualquier manejador existente para evitar duplicados
            NetworkServer.UnregisterHandler<ReadyStateMessage>();
            
            // Register server-side message handlers
            NetworkServer.RegisterHandler<ReadyStateMessage>(OnReadyStateMessage);
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Server started - registered message handlers");
        }
        
        private void NotifyPlayerDataChanged()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Notifying player data changed");
                
            OnPlayerDataChanged?.Invoke();
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Register for network manager events
            EpochNetworkManager.OnGameStateUpdated += OnNetworkGameStateUpdated;
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Client started");
                
            // Request full state update when client connects
            if (isClientOnly)
            {
                // Give the connection a moment to establish
                Invoke(nameof(RequestStateUpdate), 1f);
            }
        }
        
        private void OnNetworkGameStateUpdated()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Game state updated event received");
                
            // Notify all subscribers that player data has changed
            NotifyPlayerDataChanged();
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            // Unregister from events when stopping client
            EpochNetworkManager.OnGameStateUpdated -= OnNetworkGameStateUpdated;
        }
        
        [Client]
        private void RequestStateUpdate()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (isClientOnly && NetworkClient.active)
            {
                if (enableDebugLogs)
                    Debug.Log("[GameManager] Client requesting state update from server");
                
                // Send request for game state update
                NetworkClient.Send(new GameStateRequestMessage());
            }
        }

        private void Update()
        {
            if (!isActiveAndEnabled || !NetworkServer.active) return;

            switch (_currentState)
            {
                case GameState.Lobby:
                    // Logic for lobby state
                    break;

                case GameState.HeroSelection:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        Debug.Log("[GameManager] Timer de selección de héroes llegó a cero - iniciando juego");
                        StartGame();
                    }
                    break;

                case GameState.Playing:
                    // Logic for gameplay state
                    break;

                case GameState.GameOver:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        ReturnToLobby();
                    }
                    break;
            }
        }

        [Server]
        public void OnPlayerJoined(NetworkConnection conn)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (conn == null || conn.identity == null) 
            {
                Debug.LogError("OnPlayerJoined: Connection or identity is null");
                return;
            }
            
            uint netId = conn.identity.netId;
            _connectionToNetId[conn] = netId;
            
            if (!_connectedPlayers.ContainsKey(netId))
            {
                // Intentar obtener el nombre del jugador
                string playerName = "Player " + netId;
                
                // Buscar componente PlayerNetwork para obtener el nombre
                var playerComp = conn.identity.GetComponent<PlayerNetwork>();
                if (playerComp != null && !string.IsNullOrEmpty(playerComp.playerName))
                {
                    playerName = playerComp.playerName;
                }
                else if (EpochNetworkManager.Instance != null && !string.IsNullOrEmpty(EpochNetworkManager.Instance.playerName))
                {
                    // Intentar obtener el nombre del NetworkManager como respaldo
                    playerName = EpochNetworkManager.Instance.playerName;
                }
                
                var playerInfo = new PlayerInfo
                {
                    NetId = netId,
                    IsReady = false,
                    TeamId = AssignTeam(),
                    PlayerName = playerName
                };
                
                // Add to SyncDictionary to automatically sync to clients
                _connectedPlayers[netId] = playerInfo;
                
                if (enableDebugLogs)
                    Debug.Log($"[GameManager] Player joined. NetId: {netId}, Name: {playerName}, Total players: {_connectedPlayers.Count}");
                
                // Explicitly notify all clients about the player joining
                RpcPlayerJoined(netId, playerInfo.TeamId, playerName);
                
                // Send the full state to the new client specifically
                SendStateToClient(conn);
                
                // Explicitly tell all clients to refresh their UI after a short delay
                // to ensure the SyncDictionary has tiempo de replicarse
                Invoke(nameof(TriggerUIRefreshForAllClients), 0.5f);
            }
        }
        
        [Server]
        private void TriggerUIRefreshForAllClients()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            RpcTriggerUIRefresh();
        }
        
        [ClientRpc]
        private void RpcTriggerUIRefresh()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Received UI refresh command from server");
                
            NotifyPlayerDataChanged();
        }

        [Server]
        public void OnPlayerLeft(NetworkConnection conn)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (_connectionToNetId.TryGetValue(conn, out uint netId))
            {
                if (_connectedPlayers.ContainsKey(netId))
                {
                    var playerInfo = _connectedPlayers[netId];
                    _connectedPlayers.Remove(netId);
                    _connectionToNetId.Remove(conn);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[GameManager] Player left. NetId: {netId}, Name: {playerInfo.PlayerName}, Total players: {_connectedPlayers.Count}");
                    
                    // Notify all clients about player leaving
                    RpcPlayerLeft(netId, playerInfo.TeamId, playerInfo.PlayerName);
                    
                    // Trigger UI refresh after a short delay
                    Invoke(nameof(TriggerUIRefreshForAllClients), 0.5f);
                }
            }
        }

        [Server]
        public void SetPlayerReady(NetworkConnection conn, bool isReady)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (_connectionToNetId.TryGetValue(conn, out uint netId) && _connectedPlayers.ContainsKey(netId))
            {
                var playerInfo = _connectedPlayers[netId];
                playerInfo.IsReady = isReady;
                _connectedPlayers[netId] = playerInfo;

                if (enableDebugLogs)
                    Debug.Log($"[GameManager] Player {netId} ({playerInfo.PlayerName}) ready state set to {isReady}");
                
                // Notify all clients about player ready state change
                RpcPlayerReadyChanged(netId, isReady);

                // Trigger UI refresh after a short delay
                Invoke(nameof(TriggerUIRefreshForAllClients), 0.2f);

                // Check if all players are ready
                CheckAllPlayersReady();
            }
        }
        
        [Server]
        private void OnReadyStateMessage(NetworkConnectionToClient conn, ReadyStateMessage msg)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] Server received ready state: {msg.isReady} from connection {conn.connectionId}");
            
            SetPlayerReady(conn, msg.isReady);
        }

        [ClientRpc]
        private void RpcPlayerJoined(uint playerNetId, int teamId, string playerName)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} ({playerName}) joined team {teamId}");
                
            // No need to update dictionary here - it's already synced via SyncDictionary
            // This RPC ensures all clients are notified, including the host
            
            // Notify subscribers that player data has changed
            NotifyPlayerDataChanged();
        }
        
        [ClientRpc]
        private void RpcPlayerLeft(uint playerNetId, int teamId, string playerName)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} ({playerName}) left from team {teamId}");
                
            // No need to update dictionary here - it's already synced via SyncDictionary
            // This RPC ensures all clients are notified, including the host
            
            // Notify subscribers that player data has changed
            NotifyPlayerDataChanged();
        }
        
        [ClientRpc]
        private void RpcPlayerReadyChanged(uint playerNetId, bool isReady)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} ready state updated to {isReady}");
            
            // No need to update dictionary - it's already synced via SyncDictionary
            // This is just an explicit notification for UI updates
            
            // Notify subscribers that player data has changed
            NotifyPlayerDataChanged();
        }
        
        [Server]
        public void SendStateToClient(NetworkConnection conn)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] Sending game state to client {conn}");
                
            // First send the current game state
            GameStateResponseMessage response = new GameStateResponseMessage
            {
                connectedPlayerCount = _connectedPlayers.Count,
                currentGameState = _currentState
            };
            
            conn.Send(response);
            
            // Use TargetRpc to tell just this client to refresh its UI
            TargetRefreshUI(conn);
        }
        
        [TargetRpc]
        private void TargetRefreshUI(NetworkConnection target)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log("[GameManager] Received explicit UI refresh command from server");
            
            // Notify subscribers that player data has changed
            NotifyPlayerDataChanged();
        }
        
        // Game state change callback
        private void OnGameStateChanged(GameState oldState, GameState newState)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log($"[GameManager] Game state changed from {oldState} to {newState}");
                
            // Notify subscribers that game state has changed
            if (isClient)
            {
                NotifyPlayerDataChanged();
            }
        }

        private void CheckAllPlayersReady()
        {
            if (_connectedPlayers.Count < 2)
                return;

            bool allReady = true;
            foreach (var player in _connectedPlayers.Values)
            {
                if (!player.IsReady)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                StartHeroSelection();
            }
        }

        private int AssignTeam()
        {
            // Simple team assignment - count players in each team and assign to team with fewer players
            int team1Count = 0;
            int team2Count = 0;

            foreach (var player in _connectedPlayers.Values)
            {
                if (player.TeamId == 1)
                    team1Count++;
                else if (player.TeamId == 2)
                    team2Count++;
            }

            return team1Count <= team2Count ? 1 : 2;
        }

        [Server]
        public void StartHeroSelection()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            _currentState = GameState.HeroSelection;
            _stateTimer = heroSelectionTime;
            
            // Notificar a los clientes sobre el cambio de estado
            RpcGameStateChanged(_currentState);
            
            Debug.Log("Starting hero selection phase with delay");
            
            // Añadir un retraso antes de cambiar la escena
            StartCoroutine(DelayedSceneChange(heroSelectionScene, 1.0f));
        }

        // Método seguro para cambiar de escena con retraso
        [Server]
        private System.Collections.IEnumerator DelayedSceneChange(string sceneName, float delay)
        {
            // Esperar para dar tiempo a los clientes a prepararse
            yield return new WaitForSeconds(delay);
            
            // Precaución para evitar errores si el objeto es destruido mientras esperamos
            if (this == null || !isActiveAndEnabled) yield break;
            
            try
            {
                // Cambiar la escena
                Debug.Log($"Cambiando a escena: {sceneName}");
                NetworkManager.singleton.ServerChangeScene(sceneName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error al cambiar a escena {sceneName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [Server]
        public void StartGame()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log("GAMEMANAGER: StartGame llamado");
            _currentState = GameState.Playing;
            
            // Notify all clients about the state change
            RpcGameStateChanged(_currentState);
            
            // Mostrar información de la escena que vamos a cargar
            Debug.Log($"Intentando cambiar a escena: {gameplayScene}");
            if (string.IsNullOrEmpty(gameplayScene))
            {
                Debug.LogError("ERROR: gameplayScene está vacío!");
                return;
            }
            
            // Preparar a los clientes para el cambio de escena
            RpcPrepareForSceneChange();
            
            // Mostrar todas las escenas disponibles en el NetworkManager
            if (NetworkManager.singleton != null && NetworkManager.singleton is Mirror.NetworkManager netManager)
            {
                if (netManager.onlineScene != null)
                    Debug.Log($"NetworkManager.onlineScene: {netManager.onlineScene}");
                    
                // Si la escena no está configurada en NetworkManager, intentar cambiarla manualmente
                if (string.IsNullOrEmpty(netManager.onlineScene) || !netManager.onlineScene.Contains(gameplayScene))
                {
                    Debug.Log($"La escena {gameplayScene} no está configurada en NetworkManager. Configurando manualmente.");
                    netManager.onlineScene = gameplayScene;
                }
            }
            else
            {
                Debug.LogError("NetworkManager no encontrado o no es de tipo Mirror.NetworkManager");
            }
            
            // Usar un retraso para dar tiempo a los clientes a prepararse
            StartCoroutine(DelayedSceneChange(gameplayScene, 0.5f));
        }
        
        [ClientRpc]
        private void RpcPrepareForSceneChange()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log("[GameManager] Preparando para cambio de escena");
            
            // Desactivar controladores que podrían causar problemas
            var lobbyController = FindObjectOfType<EpochLegends.UI.Lobby.LobbyController>();
            if (lobbyController != null)
            {
                lobbyController.enabled = false;
            }
            
            // Mostrar una pantalla de carga si es posible
            var uiManager = FindObjectOfType<EpochLegends.Core.UI.Manager.UIManager>();
            if (uiManager != null)
            {
                uiManager.ShowPanel(EpochLegends.Core.UI.Manager.UIPanel.Loading);
            }
        }

        [Server]
        public void EndGame(int winningTeamId)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            _currentState = GameState.GameOver;
            _stateTimer = 10f; // Display results for 10 seconds
            
            // Notify all clients about game result
            RpcNotifyGameOver(winningTeamId);
            
            Debug.Log($"Game over. Team {winningTeamId} wins!");
        }

        [ClientRpc]
        private void RpcNotifyGameOver(int winningTeamId)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log($"Game over! Team {winningTeamId} wins!");
            // Client code to show game over UI
        }
        
        [ClientRpc]
        private void RpcGameStateChanged(GameState newState)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log($"[GameManager] Game state changed to {newState}");
                
            // This RPC explicitly notifies clients about state changes
            // Notify subscribers that game state has changed
            NotifyPlayerDataChanged();
        }

        [Server]
        public void ReturnToLobby()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log("[GameManager] ReturnToLobby llamado");
            _currentState = GameState.Lobby;
            
            // Reset player ready status
            List<uint> playerKeys = new List<uint>(_connectedPlayers.Keys);
            foreach (uint netId in playerKeys)
            {
                var playerInfo = _connectedPlayers[netId];
                playerInfo.IsReady = false;
                playerInfo.SelectedHeroId = "";
                _connectedPlayers[netId] = playerInfo;
            }
            
            // Notify clients about the state change
            RpcGameStateChanged(_currentState);
            
            // Prepara a los clientes para el cambio de escena
            RpcPrepareForSceneChange();
            
            // Cargar la escena de lobby con retraso
            StartCoroutine(DelayedSceneChange(lobbyScene, 0.5f));
        }
        
        [Server]
        public void OnHeroSelectionComplete(Dictionary<uint, string> selectionResults)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.Log("GAMEMANAGER: OnHeroSelectionComplete llamado");
            
            // Guardar las selecciones en nuestro diccionario local
            _heroSelections.Clear();
            foreach (var pair in selectionResults)
            {
                _heroSelections[pair.Key] = pair.Value;
            }
            
            // Proceso de diagnóstico
            foreach (var selection in selectionResults)
            {
                Debug.Log($"Procesando selección: Jugador {selection.Key} seleccionó héroe: {selection.Value}");
            }
            
            // Process the hero selections
            foreach (var selection in selectionResults)
            {
                uint playerNetId = selection.Key;
                string heroId = selection.Value;
                
                // Update player info with selected hero
                if (_connectedPlayers.TryGetValue(playerNetId, out PlayerInfo playerInfo))
                {
                    playerInfo.SelectedHeroId = heroId;
                    _connectedPlayers[playerNetId] = playerInfo;
                    
                    Debug.Log($"Actualizada info del jugador {playerNetId} con héroe: {heroId}");
                }
                else
                {
                    Debug.LogError($"No se encontró al jugador {playerNetId} en _connectedPlayers!");
                }
            }
            
            // Asegurarse que la escena que queremos cargar existe
            Debug.Log($"Intentando cambiar a escena: {gameplayScene}");
            
            // Transition to next phase
            Debug.Log("Iniciando juego (StartGame)");
            try {
                StartGame();
            } catch (System.Exception ex) {
                Debug.LogError($"EXCEPCIÓN al iniciar juego: {ex.Message}\n{ex.StackTrace}");
                
                // Intento de recuperación: esperar un poco y volver a intentar
                StartCoroutine(RetryStartGame());
            }
        }
        
        private System.Collections.IEnumerator RetryStartGame()
        {
            yield return new WaitForSeconds(1.0f);
            
            if (this == null || !isActiveAndEnabled) yield break;
            
            Debug.Log("Reintentando StartGame...");
            
            try
            {
                // Mover directamente a Playing state
                _currentState = GameState.Playing;
                RpcGameStateChanged(_currentState);
                
                // Intentar cargar la escena directamente
                if (NetworkManager.singleton != null)
                {
                    Debug.Log("Cambiando escena directamente en segundo intento");
                    NetworkManager.singleton.ServerChangeScene(gameplayScene);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ERROR en segundo intento: {ex.Message}");
            }
        }
        
        [Server]
        public void OnHeroCreated(Hero hero)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (hero != null)
            {
                Debug.Log($"[GameManager] Hero created: {hero.name}");
                
                // Buscar el ID del jugador propietario - adapta esto según la estructura de tu Hero
                uint ownerId = 0;
                
                // Intentar obtener el ID del propietario de diferentes formas posibles
                var netIdentity = hero.GetComponent<NetworkIdentity>();
                if (netIdentity != null && netIdentity.connectionToClient != null)
                {
                    // Intenta obtener el NetId del jugador propietario
                    var playerIdentity = netIdentity.connectionToClient.identity;
                    if (playerIdentity != null)
                    {
                        ownerId = playerIdentity.netId;
                        Debug.Log($"Se encontró ownerId utilizando connectionToClient: {ownerId}");
                    }
                }
                
                // Si no se pudo obtener de esa forma, intentar con el NetId del propio héroe
                if (ownerId == 0 && netIdentity != null)
                {
                    ownerId = netIdentity.netId;
                    Debug.Log($"Usando netId del héroe como fallback: {ownerId}");
                }
                
                // Si encontramos un ID, buscar la selección correspondiente
                if (ownerId != 0 && _heroSelections.TryGetValue(ownerId, out string heroId))
                {
                    Debug.Log($"Encontrado heroId {heroId} para jugador {ownerId}");
                    
                    // Intentar configurar el héroe según el tipo seleccionado
                    try 
                    {
                        // Verificar si el héroe tiene un método para establecer su tipo
                        var setHeroTypeMethod = hero.GetType().GetMethod("SetHeroType");
                        if (setHeroTypeMethod != null)
                        {
                            setHeroTypeMethod.Invoke(hero, new object[] { heroId });
                            Debug.Log($"Configurado héroe usando método SetHeroType");
                        }
                        // O si tiene una propiedad HeroId o similar
                        else 
                        {
                            var heroIdProperty = hero.GetType().GetProperty("HeroId") ?? 
                                                hero.GetType().GetProperty("HeroType") ?? 
                                                hero.GetType().GetProperty("Type");
                            
                            if (heroIdProperty != null && heroIdProperty.CanWrite)
                            {
                                heroIdProperty.SetValue(hero, heroId);
                                Debug.Log($"Configurado héroe usando propiedad {heroIdProperty.Name}");
                            }
                            else
                            {
                                Debug.LogError("No se encontró método o propiedad para configurar el tipo de héroe");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Error al configurar héroe: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"No se encontró heroId para jugador {ownerId}");
                }
            }
            else
            {
                Debug.LogError("[GameManager] OnHeroCreated called with null hero");
            }
        }
        
        // Método para forzar verificación de la configuración
        public void VerifyNetworkSettings() 
        {
            Debug.Log("=== VERIFICACIÓN DE CONFIGURACIÓN DEL GAMEMANAGER ===");
            Debug.Log($"Escena de lobby: {lobbyScene}");
            Debug.Log($"Escena de selección: {heroSelectionScene}");
            Debug.Log($"Escena de gameplay: {gameplayScene}");
            Debug.Log($"Estado actual: {_currentState}");
            Debug.Log($"Temporizador: {_stateTimer}");
            Debug.Log($"Jugadores conectados: {_connectedPlayers.Count}");
            
            // Verificar NetworkManager
            if (NetworkManager.singleton != null)
            {
                Debug.Log($"NetworkManager encontrado: {NetworkManager.singleton.GetType().Name}");
                Debug.Log($"Escena offline: {NetworkManager.singleton.offlineScene}");
                Debug.Log($"Escena online: {NetworkManager.singleton.onlineScene}");
                Debug.Log($"Servidor activo: {NetworkServer.active}");
                Debug.Log($"Cliente activo: {NetworkClient.active}");
            }
            else
            {
                Debug.LogError("NetworkManager.singleton es NULL!");
            }
            
            // Verificar jugadores
            foreach (var player in _connectedPlayers)
            {
                Debug.Log($"Jugador {player.Key}: Name={player.Value.PlayerName}, Ready={player.Value.IsReady}, Team={player.Value.TeamId}, Hero={player.Value.SelectedHeroId}");
            }
            
            // Verificar selecciones guardadas
            foreach (var selection in _heroSelections)
            {
                Debug.Log($"Selección guardada: Jugador {selection.Key} -> Héroe {selection.Value}");
            }
        }
        
        // Nuevo método para actualizar el nombre del jugador
        [Server]
        public void UpdatePlayerName(uint netId, string newName)
        {
            if (!_connectedPlayers.ContainsKey(netId))
                return;
                
            var playerInfo = _connectedPlayers[netId];
            string oldName = playerInfo.PlayerName;
            playerInfo.PlayerName = newName;
            _connectedPlayers[netId] = playerInfo;
            
            // Notificar a todos los clientes sobre el cambio de nombre
            RpcPlayerNameChanged(netId, oldName, newName);
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] Player {netId} name changed from '{oldName}' to '{newName}'");
                
            // Forzar actualización de UI
            Invoke(nameof(TriggerUIRefreshForAllClients), 0.2f);
        }
        
        [ClientRpc]
        private void RpcPlayerNameChanged(uint playerNetId, string oldName, string newName)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} name changed from '{oldName}' to '{newName}'");
            
            // Notificar a los suscriptores que los datos del jugador han cambiado
            NotifyPlayerDataChanged();
        }
    }
}