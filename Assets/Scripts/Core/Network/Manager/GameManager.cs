using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System;
using EpochLegends.Core.Network;
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

        [Header("Game Configuration")]
        [SerializeField] private float heroSelectionTime = 60f;
        [SerializeField] private float gameStartCountdown = 5f;
        
        [Header("Scene References")]
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string heroSelectionScene = "HeroSelection";
        [SerializeField] private string gameplayScene = "Gameplay";

        [SyncVar(hook = nameof(OnGameStateChanged))]
        private GameState _currentState = GameState.Lobby;
        
        [SyncVar]
        private float _stateTimer = 0f;
        
        // Use SyncDictionary with proper callbacks
        private readonly SyncDictionary<uint, PlayerInfo> _connectedPlayers = new SyncDictionary<uint, PlayerInfo>();
        
        // Connection to netId mapping (server-side only)
        private Dictionary<NetworkConnection, uint> _connectionToNetId = new Dictionary<NetworkConnection, uint>();

        // Debug flag to track updates
        private bool _debugNetworkUpdates = true;

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
            DontDestroyOnLoad(gameObject);
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Register server-side message handlers
            NetworkServer.RegisterHandler<ReadyStateMessage>(OnReadyStateMessage);
            
            if (_debugNetworkUpdates)
                Debug.Log("[GameManager] Server started - registered message handlers");
        }
        
        // Setup SyncDictionary callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Register for SyncDictionary events
            _connectedPlayers.Callback += OnConnectedPlayersChanged;
            
            if (_debugNetworkUpdates)
                Debug.Log("[GameManager] Client started - SyncDictionary callbacks registered");
                
            // Request full state update when client connects
            if (isClientOnly)
            {
                // Give the connection a moment to establish
                Invoke(nameof(RequestStateUpdate), 1f);
            }
        }
        
        [Client]
        private void RequestStateUpdate()
        {
            if (isClientOnly && NetworkClient.active)
            {
                if (_debugNetworkUpdates)
                    Debug.Log("[GameManager] Client requesting state update from server");
                
                // Send request for game state update
                NetworkClient.Send(new GameStateRequestMessage());
            }
        }

        private void Update()
        {
            if (!NetworkServer.active) return;

            switch (_currentState)
            {
                case GameState.Lobby:
                    // Logic for lobby state
                    break;

                case GameState.HeroSelection:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
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
            if (conn.identity == null) return;
            
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
                
                _connectedPlayers[netId] = playerInfo;
                
                if (_debugNetworkUpdates)
                    Debug.Log($"[GameManager] Player joined. NetId: {netId}, Total players: {_connectedPlayers.Count}");
                
                // Explicitly notify the just-connected player about all current players
                TargetSendFullPlayerList(conn);
                
                // Also broadcast a notification to all clients
                RpcPlayerJoined(netId, playerInfo.TeamId);
            }
        }

        [Server]
        public void OnPlayerLeft(NetworkConnection conn)
        {
            if (_connectionToNetId.TryGetValue(conn, out uint netId))
            {
                if (_connectedPlayers.ContainsKey(netId))
                {
                    var playerInfo = _connectedPlayers[netId];
                    _connectedPlayers.Remove(netId);
                    _connectionToNetId.Remove(conn);
                    
                    if (_debugNetworkUpdates)
                        Debug.Log($"[GameManager] Player left. NetId: {netId}, Total players: {_connectedPlayers.Count}");
                    
                    // Notify all clients about player leaving
                    RpcPlayerLeft(netId, playerInfo.TeamId);
                }
            }
        }

        [Server]
        public void SetPlayerReady(NetworkConnection conn, bool isReady)
        {
            if (_connectionToNetId.TryGetValue(conn, out uint netId) && _connectedPlayers.ContainsKey(netId))
            {
                var playerInfo = _connectedPlayers[netId];
                playerInfo.IsReady = isReady;
                _connectedPlayers[netId] = playerInfo;

                if (_debugNetworkUpdates)
                    Debug.Log($"[GameManager] Player {netId} ready state set to {isReady}");
                
                // Notify all clients about player ready state change
                RpcPlayerReadyChanged(netId, isReady);

                // Check if all players are ready
                CheckAllPlayersReady();
            }
        }
        
        [Server]
        private void OnReadyStateMessage(NetworkConnectionToClient conn, ReadyStateMessage msg)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] Server received ready state: {msg.isReady} from connection {conn.connectionId}");
            
            SetPlayerReady(conn, msg.isReady);
        }

        [ClientRpc]
        private void RpcPlayerJoined(uint playerNetId, int teamId)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} joined team {teamId}");
                
            // No need to update dictionary here - it's already synced
            // This is just to trigger UI updates specifically
            
            // Force UI refresh on all clients
            RefreshLobbyUI();
        }
        
        [ClientRpc]
        private void RpcPlayerLeft(uint playerNetId, int teamId)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} left from team {teamId}");
                
            // No need to update dictionary here - it's already synced
            // This is just to trigger UI updates specifically
            
            // Force UI refresh on all clients
            RefreshLobbyUI();
        }
        
        [ClientRpc]
        private void RpcPlayerReadyChanged(uint playerNetId, bool isReady)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] RPC: Player {playerNetId} ready state updated to {isReady}");
            
            // No need to update dictionary - it's already synced
            // This is just an explicit notification for UI updates
            
            // Force UI refresh on all clients
            RefreshLobbyUI();
        }
        
        [TargetRpc]
        private void TargetSendFullPlayerList(NetworkConnection target)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] Sending full player list to newly connected client");
                
            // The player list will already be synced by Mirror, but we're sending
            // an explicit notification to trigger UI update
            
            // Force UI refresh
            RefreshLobbyUI();
        }
        
        // Called by the SyncDictionary callback
        private void OnConnectedPlayersChanged(SyncDictionary<uint, PlayerInfo>.Operation op, uint key, PlayerInfo item)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] SyncDictionary changed: {op} for player {key}");
                
            // This will be called on all clients when the dictionary changes
            // We can use this to update the UI
            RefreshLobbyUI();
        }
        
        // Helper method to refresh the lobby UI
        private void RefreshLobbyUI()
        {
            // Find and update any LobbyController or LobbyUI instances
            LobbyUI.LobbyUI lobbyUI = FindObjectOfType<LobbyUI.LobbyUI>();
            if (lobbyUI != null)
            {
                if (_debugNetworkUpdates)
                    Debug.Log("[GameManager] Refreshing LobbyUI");
                
                lobbyUI.RefreshUI();
            }
            
            // Also update the other lobby controller if it exists
            EpochLegends.UI.Lobby.LobbyController lobbyController = FindObjectOfType<EpochLegends.UI.Lobby.LobbyController>();
            if (lobbyController != null)
            {
                if (_debugNetworkUpdates)
                    Debug.Log("[GameManager] Refreshing LobbyController");
                
                // This is a direct call and might need to be adapted based on your implementation
                // lobbyController might need a public RefreshUI method
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
            _currentState = GameState.HeroSelection;
            _stateTimer = heroSelectionTime;
            
            // Start a countdown using the gameStartCountdown field
            Debug.Log($"Hero selection starting in {gameStartCountdown} seconds");
            
            // Notify all clients about the state change
            RpcGameStateChanged(_currentState);
            
            // Load hero selection scene on all clients
            NetworkManager.singleton.ServerChangeScene(heroSelectionScene);
                
            Debug.Log("Starting hero selection phase");
        }

        [Server]
        public void StartGame()
        {
            _currentState = GameState.Playing;
            
            // Notify all clients about the state change
            RpcGameStateChanged(_currentState);
            
            // Load gameplay scene on all clients
            NetworkManager.singleton.ServerChangeScene(gameplayScene);
                
            Debug.Log("Starting gameplay phase");
        }

        [Server]
        public void EndGame(int winningTeamId)
        {
            _currentState = GameState.GameOver;
            _stateTimer = 10f; // Display results for 10 seconds
            
            // Notify all clients about game result
            RpcNotifyGameOver(winningTeamId);
            
            Debug.Log($"Game over. Team {winningTeamId} wins!");
        }

        [ClientRpc]
        private void RpcNotifyGameOver(int winningTeamId)
        {
            Debug.Log($"Game over! Team {winningTeamId} wins!");
            // Client code to show game over UI
        }
        
        [ClientRpc]
        private void RpcGameStateChanged(GameState newState)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] Game state changed to {newState}");
                
            // This RPC explicitly notifies clients about state changes
            // Can be used to trigger UI updates or other state-dependent actions
        }

        [Server]
        private void ReturnToLobby()
        {
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
            NetworkManager.singleton.ServerChangeScene(lobbyScene);
                
            Debug.Log("Returning to lobby");
        }
        
        [Server]
        public void SendStateToClient(NetworkConnection conn)
        {
            if (_debugNetworkUpdates)
                Debug.Log($"[GameManager] Sending game state to client {conn.connectionId}");
                
            // First send the current game state
            GameStateResponseMessage response = new GameStateResponseMessage
            {
                connectedPlayerCount = _connectedPlayers.Count,
                currentGameState = _currentState
            };
            
            conn.Send(response);
            
            // Then explicitly tell the client to refresh its UI
            TargetRefreshUI(conn);
        }
        
        [TargetRpc]
        private void TargetRefreshUI(NetworkConnection target)
        {
            if (_debugNetworkUpdates)
                Debug.Log("[GameManager] Received explicit UI refresh command from server");
            
            // Force all UI elements to refresh
            RefreshLobbyUI();
        }
        
        // Game state change callback
        private void OnGameStateChanged(GameState oldState, GameState newState)
        {
            Debug.Log($"Game state changed from {oldState} to {newState}");
            // Update UI based on game state
        }
        
        // Added for compatibility with HeroFactory
        [Server]
        public void OnHeroCreated(Hero hero)
        {
            Debug.Log($"Hero created: {hero.name}");
            // Add any logic needed when a hero is created
        }
        
        // Added for compatibility with HeroSelectionManager
        [Server]
        public void OnHeroSelectionComplete(Dictionary<uint, string> selectionResults)
        {
            Debug.Log("Hero selection phase completed");
            
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
                
                Debug.Log($"Player {playerNetId} selected hero: {heroId}");
            }
            
            // Transition to next phase
            StartGame();
        }
    }
}