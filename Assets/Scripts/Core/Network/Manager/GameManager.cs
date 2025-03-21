using UnityEngine;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Network;
using EpochLegends.Core.Player;

namespace EpochLegends
{
    public enum GameState
    {
        Lobby,
        HeroSelection,
        Playing,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Configuration")]
        [SerializeField] private float heroSelectionTime = 60f;
        [SerializeField] private float gameStartCountdown = 5f;
        
        [Header("Scene References")]
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string heroSelectionScene = "HeroSelection";
        [SerializeField] private string gameplayScene = "Gameplay";

        private GameState _currentState = GameState.Lobby;
        private float _stateTimer = 0f;
        private Dictionary<NetworkConnection, PlayerInfo> _connectedPlayers = new Dictionary<NetworkConnection, PlayerInfo>();

        public GameState CurrentState => _currentState;
        public int ConnectedPlayerCount => _connectedPlayers.Count;
        public Dictionary<NetworkConnection, PlayerInfo> ConnectedPlayers => _connectedPlayers;

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

        public void OnPlayerJoined(NetworkConnection conn)
        {
            if (!_connectedPlayers.ContainsKey(conn))
            {
                var playerInfo = new PlayerInfo
                {
                    Connection = conn,
                    IsReady = false,
                    TeamId = AssignTeam()
                };
                
                _connectedPlayers.Add(conn, playerInfo);
                Debug.Log($"Player joined. Total players: {_connectedPlayers.Count}");
            }
        }

        public void OnPlayerLeft(NetworkConnection conn)
        {
            if (_connectedPlayers.ContainsKey(conn))
            {
                _connectedPlayers.Remove(conn);
                Debug.Log($"Player left. Total players: {_connectedPlayers.Count}");
            }
        }

        public void SetPlayerReady(NetworkConnection conn, bool isReady)
        {
            if (_connectedPlayers.ContainsKey(conn))
            {
                var playerInfo = _connectedPlayers[conn];
                playerInfo.IsReady = isReady;
                _connectedPlayers[conn] = playerInfo;

                // Check if all players are ready
                CheckAllPlayersReady();
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
            
            // Load hero selection scene on all clients
            NetworkManager.Instance.ServerChangeScene(heroSelectionScene);
        }

        [Server]
        public void StartGame()
        {
            _currentState = GameState.Playing;
            
            // Load gameplay scene on all clients
            NetworkManager.Instance.ServerChangeScene(gameplayScene);
        }

        [Server]
        public void EndGame(int winningTeamId)
        {
            _currentState = GameState.GameOver;
            _stateTimer = 10f; // Display results for 10 seconds
            
            // Notify all clients about game result
            foreach (var playerEntry in _connectedPlayers)
            {
                // Send game over message with results
                // Example implementation would need corresponding client handlers
            }
        }

        [Server]
        private void ReturnToLobby()
        {
            _currentState = GameState.Lobby;
            
            // Reset player ready status
            foreach (var conn in _connectedPlayers.Keys)
            {
                var playerInfo = _connectedPlayers[conn];
                playerInfo.IsReady = false;
                playerInfo.SelectedHeroId = "";
                _connectedPlayers[conn] = playerInfo;
            }
            
            // Load lobby scene on all clients
            NetworkManager.Instance.ServerChangeScene(lobbyScene);
        }
    }

    public struct PlayerInfo
    {
        public NetworkConnection Connection;
        public bool IsReady;
        public int TeamId;
        public string SelectedHeroId;
        public int Kills;
        public int Deaths;
        public int Assists;
    }
}