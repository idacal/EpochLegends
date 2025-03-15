using UnityEngine;
using Mirror; // Para NetworkBehaviour
using System.Collections.Generic;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.Player;
using EpochLegends.Core.UI.Manager;
using EpochLegends.Core.Hero;

namespace EpochLegends
{
    public enum GameState
    {
        MainMenu,
        Lobby,
        HeroSelection,
        Playing,
        GameOver
    }

    // Cambiamos de MonoBehaviour a NetworkBehaviour para poder usar RPC
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Configuration")]
        [SerializeField] private float heroSelectionTime = 60f;
        [SerializeField] private float gameStartCountdown = 5f;
        
        [Header("Scene References")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string heroSelectionScene = "HeroSelection";
        [SerializeField] private string gameplayScene = "Gameplay";

        private GameState _currentState = GameState.MainMenu;
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

        public void OnServerStarted()
        {
            // This gets called when the EpochNetworkManager starts the server
            SetGameState(GameState.Lobby);
            Debug.Log("GameManager notified of server start");
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
                
                // Notify clients to update lobby UI
                if (_currentState == GameState.Lobby)
                {
                    UpdateLobbyUI();
                }
            }
        }

        public void OnPlayerLeft(NetworkConnection conn)
        {
            if (_connectedPlayers.ContainsKey(conn))
            {
                _connectedPlayers.Remove(conn);
                Debug.Log($"Player left. Total players: {_connectedPlayers.Count}");
                
                // Notify clients to update lobby UI
                if (_currentState == GameState.Lobby)
                {
                    UpdateLobbyUI();
                }
            }
        }

        public void SetPlayerReady(NetworkConnection conn, bool isReady)
        {
            if (_connectedPlayers.ContainsKey(conn))
            {
                var playerInfo = _connectedPlayers[conn];
                playerInfo.IsReady = isReady;
                _connectedPlayers[conn] = playerInfo;

                // Update lobby UI
                UpdateLobbyUI();
                
                // Check if all players are ready
                CheckAllPlayersReady();
                
                Debug.Log($"Player readiness changed. Ready: {isReady}");
            }
        }
        
        // En lugar de usar RPC, simplemente llamamos a un método normal
        private void UpdateLobbyUI()
        {
            // Implementación sin RPC
            Debug.Log("Updating lobby UI");
            
            // Para notificar a clientes sin usar RPC, necesitaríamos otro enfoque
            // Por ejemplo, podríamos usar SyncVars o enviar mensajes de red explícitos
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

        public void SetGameState(GameState newState)
        {
            _currentState = newState;
            
            // Handle state-specific setup
            switch (newState)
            {
                case GameState.MainMenu:
                    // Nothing special needed for main menu
                    break;
                    
                case GameState.Lobby:
                    // Setup for lobby state
                    break;
                    
                case GameState.HeroSelection:
                    _stateTimer = heroSelectionTime;
                    break;
                    
                case GameState.Playing:
                    // Setup for gameplay
                    break;
                    
                case GameState.GameOver:
                    _stateTimer = 10f; // Show results for 10 seconds
                    break;
            }
            
            Debug.Log($"Game state changed to: {newState}");
        }

        [Server]
        public void StartHeroSelection()
        {
            SetGameState(GameState.HeroSelection);
            
            // Load hero selection scene on all clients
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.ServerChangeScene(heroSelectionScene);
            }
            
            Debug.Log("Starting hero selection phase");
        }

        [Server]
        public void StartGame()
        {
            SetGameState(GameState.Playing);
            
            // Load gameplay scene on all clients
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.ServerChangeScene(gameplayScene);
            }
            
            Debug.Log("Starting gameplay phase");
        }

        [Server]
        public void EndGame(int winningTeamId)
        {
            SetGameState(GameState.GameOver);
            
            // Notificar el fin de juego sin usar RPC
            Debug.Log($"Game ended. Winning team: {winningTeamId}");
            
            // En lugar de RPC, podrías usar SyncVars o almacenar el resultado en algún lugar
            // para que los clientes lo consulten
        }

        [Server]
        private void ReturnToLobby()
        {
            SetGameState(GameState.Lobby);
            
            // Reset player ready status
            foreach (var conn in _connectedPlayers.Keys)
            {
                var playerInfo = _connectedPlayers[conn];
                playerInfo.IsReady = false;
                playerInfo.SelectedHeroId = "";
                _connectedPlayers[conn] = playerInfo;
            }
            
            // Load lobby scene on all clients
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.ServerChangeScene(lobbyScene);
            }
            
            Debug.Log("Returning to lobby");
        }
        
        public void OnHeroCreated(Hero hero)
        {
            Debug.Log($"GameManager: Hero created {hero.name}");
            // Add logic for tracking created heroes
        }

        public void OnHeroSelectionComplete(Dictionary<uint, string> selectionResults)
        {
            Debug.Log("GameManager: Hero selection complete");
            
            // Process selection results
            foreach (var selection in selectionResults)
            {
                Debug.Log($"Player {selection.Key} selected hero {selection.Value}");
            }
            
            // Transition to gameplay
            StartGame();
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