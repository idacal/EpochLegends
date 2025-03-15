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
        
        // Using SyncDictionary without Callback
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
            DontDestroyOnLoad(gameObject);
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Register server-side message handlers
            NetworkServer.RegisterHandler<ReadyStateMessage>(OnReadyStateMessage);
            
            Debug.Log("GameManager: Server started - registered message handlers");
        }
        
        // Hook for SyncDictionary changes
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Register for SyncDictionary events (using different approach)
            StartCoroutine(MonitorDictionaryChanges());
        }
        
        private System.Collections.IEnumerator MonitorDictionaryChanges()
        {
            Dictionary<uint, PlayerInfo> previousState = new Dictionary<uint, PlayerInfo>();
            
            while (true)
            {
                bool changed = false;
                
                // Check for new or changed items
                foreach (var kvp in _connectedPlayers)
                {
                    if (!previousState.TryGetValue(kvp.Key, out PlayerInfo prevValue) || 
                        !prevValue.Equals(kvp.Value))
                    {
                        changed = true;
                        break;
                    }
                }
                
                // Check for removed items
                if (!changed)
                {
                    foreach (var prevKey in previousState.Keys)
                    {
                        if (!_connectedPlayers.ContainsKey(prevKey))
                        {
                            changed = true;
                            break;
                        }
                    }
                }
                
                // If changes detected, update UI
                if (changed)
                {
                    Debug.Log("Connected players dictionary changed");
                    
                    // Update previous state
                    previousState.Clear();
                    foreach (var kvp in _connectedPlayers)
                    {
                        previousState[kvp.Key] = kvp.Value;
                    }
                    
                    // Call UI update methods
                    OnConnectedPlayersChanged();
                }
                
                yield return new WaitForSeconds(0.2f);
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
                Debug.Log($"Player joined. Total players: {_connectedPlayers.Count}");
                
                // Notify all clients about updated player list
                RpcUpdatePlayerList();
            }
        }

        [Server]
        public void OnPlayerLeft(NetworkConnection conn)
        {
            if (_connectionToNetId.TryGetValue(conn, out uint netId))
            {
                if (_connectedPlayers.ContainsKey(netId))
                {
                    _connectedPlayers.Remove(netId);
                    _connectionToNetId.Remove(conn);
                    
                    Debug.Log($"Player left. Total players: {_connectedPlayers.Count}");
                    
                    // Notify all clients about updated player list
                    RpcUpdatePlayerList();
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

                Debug.Log($"Player {netId} ready state set to {isReady}");
                
                // Notify clients of the update
                RpcUpdatePlayerReadyState(netId, isReady);

                // Check if all players are ready
                CheckAllPlayersReady();
            }
        }
        
        [Server]
        private void OnReadyStateMessage(NetworkConnectionToClient conn, ReadyStateMessage msg)
        {
            Debug.Log($"Server received ready state: {msg.isReady} from connection {conn.connectionId}");
            SetPlayerReady(conn, msg.isReady);
        }

        [ClientRpc]
        private void RpcUpdatePlayerList()
        {
            Debug.Log($"Updated player list received. Total players: {_connectedPlayers.Count}");
            // Client code to refresh UI goes here
            OnConnectedPlayersChanged();
        }
        
        [ClientRpc]
        private void RpcUpdatePlayerReadyState(uint playerNetId, bool isReady)
        {
            Debug.Log($"Player {playerNetId} ready state updated to {isReady}");
            // Update UI for this specific player's ready state
            OnConnectedPlayersChanged();
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
            
            // Load hero selection scene on all clients
            NetworkManager.singleton.ServerChangeScene(heroSelectionScene);
                
            Debug.Log("Starting hero selection phase");
        }

        [Server]
        public void StartGame()
        {
            _currentState = GameState.Playing;
            
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
            
            // Load lobby scene on all clients
            NetworkManager.singleton.ServerChangeScene(lobbyScene);
                
            Debug.Log("Returning to lobby");
        }
        
        [Server]
        public void SendStateToClient(NetworkConnection conn)
        {
            // No need to send the whole connected players dictionary as it's already synced
            // Just notify the client to refresh its UI
            TargetRefreshUI(conn);
        }
        
        [TargetRpc]
        private void TargetRefreshUI(NetworkConnection target)
        {
            Debug.Log("Received refresh UI command from server");
            // Client code to refresh all UI elements with current state
            OnConnectedPlayersChanged();
        }
        
        // Method to handle player dictionary changes
        private void OnConnectedPlayersChanged()
        {
            Debug.Log($"Player dictionary changed - Current count: {_connectedPlayers.Count}");
            // Refresh UI based on the changes
            
            // This would normally update your UI elements
            // For now, we'll just log the players
            foreach (var player in _connectedPlayers)
            {
                Debug.Log($"Player NetID: {player.Key}, Ready: {player.Value.IsReady}, Team: {player.Value.TeamId}");
            }
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