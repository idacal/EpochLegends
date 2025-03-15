using UnityEngine;
using Mirror;
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

    // Convertido a NetworkBehaviour para poder utilizar funcionalidades de red.
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
                    // Lógica para estado Lobby
                    break;

                case GameState.HeroSelection:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        StartGame();
                    }
                    break;

                case GameState.Playing:
                    // Lógica para gameplay
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
            SetGameState(GameState.Lobby);
            Debug.Log("GameManager notificado del inicio del servidor");
        }

        public void OnPlayerJoined(NetworkConnection conn)
        {
            if (_connectedPlayers.ContainsKey(conn))
            {
                Debug.Log("El jugador ya está registrado. Actualizando información.");
                var playerInfo = _connectedPlayers[conn];
                playerInfo.IsReady = false;
                _connectedPlayers[conn] = playerInfo;
            }
            else
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

                UpdateLobbyUI();
                CheckAllPlayersReady();
                Debug.Log($"Player readiness changed. Ready: {isReady}");
            }
        }
        
        // Actualiza la UI del lobby (se puede implementar con SyncVars o mensajes de red)
        private void UpdateLobbyUI()
        {
            Debug.Log("Updating lobby UI");
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
            switch (newState)
            {
                case GameState.MainMenu:
                    break;
                    
                case GameState.Lobby:
                    break;
                    
                case GameState.HeroSelection:
                    _stateTimer = heroSelectionTime;
                    break;
                    
                case GameState.Playing:
                    break;
                    
                case GameState.GameOver:
                    _stateTimer = 10f;
                    break;
            }
            
            Debug.Log($"Game state changed to: {newState}");
        }

        [Server]
        public void StartHeroSelection()
        {
            SetGameState(GameState.HeroSelection);
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
            Debug.Log($"Game ended. Winning team: {winningTeamId}");
        }

        [Server]
        private void ReturnToLobby()
        {
            SetGameState(GameState.Lobby);

            foreach (var conn in _connectedPlayers.Keys)
            {
                var playerInfo = _connectedPlayers[conn];
                playerInfo.IsReady = false;
                playerInfo.SelectedHeroId = "";
                _connectedPlayers[conn] = playerInfo;
            }
            
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.ServerChangeScene(lobbyScene);
            }
            
            Debug.Log("Returning to lobby");
        }
        
        public void OnHeroCreated(Hero hero)
        {
            Debug.Log($"GameManager: Hero created {hero.name}");
        }

        public void OnHeroSelectionComplete(Dictionary<uint, string> selectionResults)
        {
            Debug.Log("GameManager: Hero selection complete");
            foreach (var selection in selectionResults)
            {
                Debug.Log($"Player {selection.Key} selected hero {selection.Value}");
            }
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
