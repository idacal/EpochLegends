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

        [SyncVar(hook = nameof(OnGameStateChanged))]
        private GameState _currentState = GameState.Lobby;
        
        [SyncVar]
        private float _stateTimer = 0f;
        
        // Use SyncDictionary for player data
        private readonly SyncDictionary<uint, PlayerInfo> _connectedPlayers = new SyncDictionary<uint, PlayerInfo>();
        
        // Connection to netId mapping (server-side only)
        private Dictionary<NetworkConnection, uint> _connectionToNetId = new Dictionary<NetworkConnection, uint>();

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
            // ELIMINADO: DontDestroyOnLoad(gameObject);
            // El ManagersController se encargará de preservar este objeto
            
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
                        Debug.LogError("[GameManager] Timer de selección de héroes llegó a cero - iniciando juego");
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
                var playerInfo = new PlayerInfo
                {
                    NetId = netId,
                    IsReady = false,
                    TeamId = AssignTeam()
                };
                
                // Add to SyncDictionary to automatically sync to clients
                _connectedPlayers[netId] = playerInfo;
                
                if (enableDebugLogs)
                    Debug.Log($"[GameManager] Player joined. NetId: {netId}, Total players: {_connectedPlayers.Count}");
                
                // Explicitly notify all clients about the player joining
                RpcPlayerJoined(netId, playerInfo.TeamId);
                
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
                        Debug.Log($"[GameManager] Player left. NetId: {netId}, Total players: {_connectedPlayers.Count}");
                    
                    // Notify all clients about player leaving
                    RpcPlayerLeft(netId, playerInfo.TeamId);
                    
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
                    Debug.Log($"[GameManager] Player {netId} ready state set to {isReady}");
                
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
        private void RpcPlayerJoined(uint playerNetId, int teamId)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} joined team {teamId}");
                
            // No need to update dictionary here - it's already synced via SyncDictionary
            // This RPC ensures all clients are notified, including the host
            
            // Notify subscribers that player data has changed
            NotifyPlayerDataChanged();
        }
        
        [ClientRpc]
        private void RpcPlayerLeft(uint playerNetId, int teamId)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (enableDebugLogs)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} left from team {teamId}");
                
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
            
            Debug.LogError($"[GameManager] Game state changed from {oldState} to {newState}");
                
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
            
            Debug.LogError("Starting hero selection phase with delay");
            
            // Añadir un retraso antes de cambiar la escena
            StartCoroutine(DelayedSceneChange());
        }

        // Añade este nuevo método
        [Server]
        private System.Collections.IEnumerator DelayedSceneChange()
        {
            // Esperar para dar tiempo a los clientes a prepararse
            yield return new WaitForSeconds(1.0f);
            
            // Cambiar a la escena de selección de héroes
            NetworkManager.singleton.ServerChangeScene(heroSelectionScene);
            
            Debug.LogError("Changed to hero selection scene");
        }

        [Server]
        public void StartGame()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.LogError("GAMEMANAGER: StartGame llamado");
            _currentState = GameState.Playing;
            
            // Notify all clients about the state change
            RpcGameStateChanged(_currentState);
            
            // Mostrar información de la escena que vamos a cargar
            Debug.LogError($"Intentando cambiar a escena: {gameplayScene}");
            if (string.IsNullOrEmpty(gameplayScene))
            {
                Debug.LogError("ERROR: gameplayScene está vacío!");
                return;
            }
            
            // Mostrar todas las escenas disponibles en el NetworkManager
            if (NetworkManager.singleton != null && NetworkManager.singleton is Mirror.NetworkManager netManager)
            {
                if (netManager.onlineScene != null)
                    Debug.LogError($"NetworkManager.onlineScene: {netManager.onlineScene}");
                    
                // Si la escena no está configurada en NetworkManager, intentar cambiarla manualmente
                if (string.IsNullOrEmpty(netManager.onlineScene) || !netManager.onlineScene.Contains(gameplayScene))
                {
                    Debug.LogError($"La escena {gameplayScene} no está configurada en NetworkManager. Configurando manualmente.");
                    netManager.onlineScene = gameplayScene;
                }
            }
            else
            {
                Debug.LogError("NetworkManager no encontrado o no es de tipo Mirror.NetworkManager");
            }
            
            // Load gameplay scene on all clients
            try {
                NetworkManager.singleton.ServerChangeScene(gameplayScene);
                Debug.LogError("Solicitud de cambio de escena enviada");
            } catch (System.Exception ex) {
                Debug.LogError($"Error al cambiar escena: {ex.Message}\n{ex.StackTrace}");
            }
            
            Debug.LogError("Starting gameplay phase");
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
            
            Debug.LogError($"[GameManager] Game state changed to {newState}");
                
            // This RPC explicitly notifies clients about state changes
            // Notify subscribers that game state has changed
            NotifyPlayerDataChanged();
        }

        [Server]
        public void ReturnToLobby()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.LogError("[GameManager] ReturnToLobby llamado");
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
            
            // Load lobby scene on all clients
            try {
                NetworkManager.singleton.ServerChangeScene(lobbyScene);
                Debug.LogError("Solicitado cambio a escena de lobby");
            } catch (System.Exception ex) {
                Debug.LogError($"Error al cambiar a escena de lobby: {ex.Message}");
            }
            
            Debug.LogError("Returning to lobby");
        }
        
        [Server]
        public void OnHeroSelectionComplete(Dictionary<uint, string> selectionResults)
        {
            if (this == null || !isActiveAndEnabled) return;
            
            Debug.LogError("GAMEMANAGER: OnHeroSelectionComplete llamado");
            
            // Proceso de diagnóstico
            foreach (var selection in selectionResults)
            {
                Debug.LogError($"Procesando selección: Jugador {selection.Key} seleccionó héroe: {selection.Value}");
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
                }
                
                Debug.LogError($"Jugador {playerNetId} seleccionó héroe: {heroId}");
            }
            
            // Asegurarse que la escena que queremos cargar existe
            Debug.LogError($"Intentando cambiar a escena: {gameplayScene}");
            
            // Transition to next phase
            Debug.LogError("Iniciando juego (StartGame)");
            try {
                StartGame();
            } catch (System.Exception ex) {
                Debug.LogError($"EXCEPCIÓN al iniciar juego: {ex.Message}\n{ex.StackTrace}");
                // Intentar cargar la escena de forma directa
                try {
                    Debug.LogError("Intentando cargar escena directamente: " + gameplayScene);
                    NetworkManager.singleton.ServerChangeScene(gameplayScene);
                } catch (System.Exception ex2) {
                    Debug.LogError($"SEGUNDA EXCEPCIÓN al cargar escena: {ex2.Message}");
                }
            }
        }
        // Añade este método a la clase GameManager.cs
// Cerca de los otros métodos relacionados con los héroes

        // Añade este método a la clase GameManager.cs
// Con el namespace correcto para Hero

[Server]
public void OnHeroCreated(EpochLegends.Core.Hero.Hero hero)
{
    if (this == null || !isActiveAndEnabled) return;
    
    if (hero != null)
    {
        Debug.Log($"[GameManager] Hero created: {hero.name}");
        
        // Aquí puedes añadir lógica adicional según sea necesario,
        // como almacenar referencias a héroes, asignar estadísticas, etc.
    }
    else
    {
        Debug.LogError("[GameManager] OnHeroCreated called with null hero");
    }
}
        // Método para forzar verificación de la configuración
        public void VerifyNetworkSettings() 
        {
            Debug.LogError("=== VERIFICACIÓN DE CONFIGURACIÓN DEL GAMEMANAGER ===");
            Debug.LogError($"Escena de lobby: {lobbyScene}");
            Debug.LogError($"Escena de selección: {heroSelectionScene}");
            Debug.LogError($"Escena de gameplay: {gameplayScene}");
            Debug.LogError($"Estado actual: {_currentState}");
            Debug.LogError($"Temporizador: {_stateTimer}");
            Debug.LogError($"Jugadores conectados: {_connectedPlayers.Count}");
            
            // Verificar NetworkManager
            if (NetworkManager.singleton != null)
            {
                Debug.LogError($"NetworkManager encontrado: {NetworkManager.singleton.GetType().Name}");
                Debug.LogError($"Escena offline: {NetworkManager.singleton.offlineScene}");
                Debug.LogError($"Escena online: {NetworkManager.singleton.onlineScene}");
                Debug.LogError($"Servidor activo: {NetworkServer.active}");
                Debug.LogError($"Cliente activo: {NetworkClient.active}");
            }
            else
            {
                Debug.LogError("NetworkManager.singleton es NULL!");
            }
        }
    }
}