using UnityEngine;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Hero;
using EpochLegends.Core.Player;

namespace EpochLegends.Systems.Team.Manager
{
    [System.Serializable]
    public class TeamConfig
    {
        public int teamId;
        public string teamName;
        public Color teamColor = Color.white;
        public Transform[] spawnPoints;
        public int maxPlayers = 5;
    }
    
    public class TeamManager : NetworkBehaviour
    {
        public static TeamManager Instance { get; private set; }
        
        [Header("Team Configuration")]
        [SerializeField] private TeamConfig[] teamConfigs = new TeamConfig[2];
        [SerializeField] private bool autoBalanceTeams = true;
        [SerializeField] private bool debugTeamUpdates = true;
        
        // Create proper SyncLists to replace string serialization
        [SyncVar(hook = nameof(OnTeam1PlayersChanged))]
        private string team1Players = "";
        
        [SyncVar(hook = nameof(OnTeam2PlayersChanged))]
        private string team2Players = "";
        
        // Using the standard SyncDictionary without callback
        private readonly SyncDictionary<uint, int> teamByPlayer = new SyncDictionary<uint, int>();
        
        // Client-side dictionaries for easier management
        private Dictionary<int, List<uint>> playersByTeam = new Dictionary<int, List<uint>>();
        private Dictionary<int, List<uint>> heroesByTeam = new Dictionary<int, List<uint>>();
        
        // Server-side cache for NetworkConnection to NetId mapping
        private Dictionary<NetworkConnection, uint> connectionToNetId = new Dictionary<NetworkConnection, uint>();
        
        // Time tracking for periodic updates
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.5f;
        
        // Properties
        public int TeamCount => teamConfigs.Length;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize team dictionaries
            InitializeTeamDictionaries();
        }
        
        private void Update()
        {
            // Periodic check for changes (instead of using Callback)
            if (Time.time - lastUpdateTime >= UPDATE_INTERVAL)
            {
                UpdateLocalDictionaries();
                RefreshTeamUI();
                lastUpdateTime = Time.time;
            }
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Additional server-side initialization if needed
            if (debugTeamUpdates)
                Debug.Log("[TeamManager] Server started");
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Initialize local dictionaries
            UpdateLocalDictionaries();
            
            if (debugTeamUpdates)
                Debug.Log("[TeamManager] Client started");
        }
        
        // Initialize team tracking dictionaries
        private void InitializeTeamDictionaries()
        {
            // Initialize dictionaries for each team
            foreach (var config in teamConfigs)
            {
                if (!playersByTeam.ContainsKey(config.teamId))
                {
                    playersByTeam[config.teamId] = new List<uint>();
                }
                
                if (!heroesByTeam.ContainsKey(config.teamId))
                {
                    heroesByTeam[config.teamId] = new List<uint>();
                }
            }
            
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] Team dictionaries initialized for {teamConfigs.Length} teams");
        }
        
        // Update client-side dictionaries based on serialized versions
        private void UpdateLocalDictionaries()
        {
            // Initialize team 1 players
            if (!string.IsNullOrEmpty(team1Players))
            {
                playersByTeam[1] = DeserializeUintList(team1Players);
            }
            else
            {
                playersByTeam[1] = new List<uint>();
            }
            
            // Initialize team 2 players
            if (!string.IsNullOrEmpty(team2Players))
            {
                playersByTeam[2] = DeserializeUintList(team2Players);
            }
            else
            {
                playersByTeam[2] = new List<uint>();
            }
            
            if (debugTeamUpdates && isClient)
                Debug.Log("[TeamManager] Local dictionaries updated");
        }
        
        // SyncVar hook for team 1 players
        private void OnTeam1PlayersChanged(string oldValue, string newValue)
        {
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] Team 1 players changed: {newValue}");
                
            playersByTeam[1] = DeserializeUintList(newValue);
            
            // Notify UI about team changes
            RefreshTeamUI();
        }
        
        // SyncVar hook for team 2 players
        private void OnTeam2PlayersChanged(string oldValue, string newValue)
        {
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] Team 2 players changed: {newValue}");
                
            playersByTeam[2] = DeserializeUintList(newValue);
            
            // Notify UI about team changes
            RefreshTeamUI();
        }
        
        // Helper method to refresh team UI
        private void RefreshTeamUI()
        {
            // Find LobbyController instances and refresh them
            var lobbyController = FindObjectOfType<EpochLegends.UI.Lobby.LobbyController>();
            if (lobbyController != null)
            {
                // Call RefreshUI if available
                if (typeof(EpochLegends.UI.Lobby.LobbyController).GetMethod("RefreshUI") != null)
                {
                    lobbyController.SendMessage("RefreshUI");
                }
            }
            
            // Find LobbyUI instances and refresh them
            var lobbyUI = FindObjectOfType<EpochLegends.Core.UI.Lobby.LobbyUI>();
            if (lobbyUI != null)
            {
                lobbyUI.RefreshUI();
            }
        }
        
        // Utility method to serialize a uint list as comma-separated string
        private string SerializeUintList(List<uint> list)
        {
            return string.Join(",", list);
        }
        
        // Utility method to deserialize a comma-separated string to uint list
        private List<uint> DeserializeUintList(string serialized)
        {
            List<uint> result = new List<uint>();
            
            if (string.IsNullOrEmpty(serialized))
                return result;
                
            string[] parts = serialized.Split(',');
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part) && uint.TryParse(part, out uint value))
                {
                    result.Add(value);
                }
            }
            
            return result;
        }
        
        #region Team Assignment
        
        [Server]
        public int AssignPlayerToTeam(NetworkConnection player)
        {
            if (player == null || player.identity == null) return -1;
            
            uint netId = player.identity.netId;
            connectionToNetId[player] = netId;
            
            // If player already has a team, return that team
            if (teamByPlayer.ContainsKey(netId))
            {
                return teamByPlayer[netId];
            }
            
            // If auto-balance is enabled, assign to team with fewer players
            if (autoBalanceTeams)
            {
                int targetTeam = GetSmallestTeam();
                
                // Assign to selected team
                AssignToTeam(player, targetTeam);
                return targetTeam;
            }
            else
            {
                // Fallback to first team if auto-balance is disabled
                AssignToTeam(player, teamConfigs[0].teamId);
                return teamConfigs[0].teamId;
            }
        }
        
        [Server]
        public bool RequestTeamChange(NetworkConnection player, int newTeamId)
        {
            if (player == null || player.identity == null) return false;
            uint netId = player.identity.netId;
            
            // Validate team ID
            if (!IsValidTeamId(newTeamId))
            {
                Debug.LogWarning($"Invalid team ID requested: {newTeamId}");
                return false;
            }
            
            // Check if team has space
            if (playersByTeam[newTeamId].Count >= GetTeamConfig(newTeamId).maxPlayers)
            {
                Debug.LogWarning($"Team {newTeamId} is full");
                return false;
            }
            
            // If valid, change team
            int oldTeamId = RemoveFromCurrentTeam(player);
            AssignToTeam(player, newTeamId);
            
            // Notify clients about team change
            RpcNotifyTeamChange(netId, oldTeamId, newTeamId);
            
            return true;
        }
        
        [Server]
        private void AssignToTeam(NetworkConnection player, int teamId)
        {
            if (player == null || player.identity == null) return;
            uint netId = player.identity.netId;
            connectionToNetId[player] = netId;
            
            if (!IsValidTeamId(teamId))
            {
                Debug.LogError($"Cannot assign player to invalid team ID: {teamId}");
                return;
            }
            
            // Add to team tracking
            if (!playersByTeam.ContainsKey(teamId))
            {
                playersByTeam[teamId] = new List<uint>();
            }
            
            if (!playersByTeam[teamId].Contains(netId))
            {
                playersByTeam[teamId].Add(netId);
                
                // Update the synced variables based on team ID
                if (teamId == 1)
                {
                    team1Players = SerializeUintList(playersByTeam[1]);
                }
                else if (teamId == 2)
                {
                    team2Players = SerializeUintList(playersByTeam[2]);
                }
            }
            
            // Update player-to-team mapping
            teamByPlayer[netId] = teamId;
            
            // Notify the player about their team assignment
            TargetNotifyTeamAssignment(player, teamId);
            
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] Player {netId} assigned to team {teamId}");
        }
        
        [Server]
        private int RemoveFromCurrentTeam(NetworkConnection player)
        {
            if (player == null || player.identity == null) return -1;
            uint netId = player.identity.netId;
            
            // If player isn't assigned to a team, return -1
            if (!teamByPlayer.ContainsKey(netId))
            {
                return -1;
            }
            
            int currentTeam = teamByPlayer[netId];
            
            // Remove from team list
            if (playersByTeam.ContainsKey(currentTeam))
            {
                playersByTeam[currentTeam].Remove(netId);
                
                // Update the synced variables based on team ID
                if (currentTeam == 1)
                {
                    team1Players = SerializeUintList(playersByTeam[1]);
                }
                else if (currentTeam == 2)
                {
                    team2Players = SerializeUintList(playersByTeam[2]);
                }
            }
            
            // Remove from mapping
            teamByPlayer.Remove(netId);
            
            return currentTeam;
        }
        
        [TargetRpc]
        private void TargetNotifyTeamAssignment(NetworkConnection target, int teamId)
        {
            // Client-side notification of team assignment
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] You've been assigned to team {teamId}");
            
            // Get team configuration for visuals, etc.
            TeamConfig config = GetTeamConfig(teamId);
            
            // Client could update UI or player indicator colors here
            RefreshTeamUI();
        }
        
        [ClientRpc]
        private void RpcNotifyTeamChange(uint playerNetId, int oldTeamId, int newTeamId)
        {
            // Client-side notification of team change for any player
            if (NetworkClient.spawned.TryGetValue(playerNetId, out NetworkIdentity identity))
            {
                if (debugTeamUpdates)
                    Debug.Log($"[TeamManager] Player {identity.name} changed from team {oldTeamId} to {newTeamId}");
                
                // Update visual indicators, UI, etc.
                RefreshTeamUI();
            }
        }
        
        private int GetSmallestTeam()
        {
            int smallestTeamId = teamConfigs[0].teamId;
            int smallestCount = int.MaxValue;
            
            foreach (var config in teamConfigs)
            {
                int teamId = config.teamId;
                int playerCount = playersByTeam.ContainsKey(teamId) ? playersByTeam[teamId].Count : 0;
                
                if (playerCount < smallestCount)
                {
                    smallestCount = playerCount;
                    smallestTeamId = teamId;
                }
            }
            
            return smallestTeamId;
        }
        
        #endregion
        
        #region Hero Registration
        
        [Server]
        public void RegisterHero(Hero hero, int teamId)
        {
            if (!IsValidTeamId(teamId) || hero == null) return;
            
            uint heroNetId = hero.netId;
            
            // Add hero to team tracking
            if (!heroesByTeam.ContainsKey(teamId))
            {
                heroesByTeam[teamId] = new List<uint>();
            }
            
            if (!heroesByTeam[teamId].Contains(heroNetId))
            {
                heroesByTeam[teamId].Add(heroNetId);
                
                // Set the hero's team
                hero.SetTeamId(teamId);
                
                // Apply team-specific visuals
                ApplyTeamVisuals(hero, teamId);
                
                if (debugTeamUpdates)
                    Debug.Log($"[TeamManager] Hero {hero.name} registered to team {teamId}");
            }
        }
        
        [Server]
        public void UnregisterHero(Hero hero)
        {
            if (hero == null) return;
            
            int teamId = hero.TeamId;
            uint heroNetId = hero.netId;
            
            if (IsValidTeamId(teamId) && heroesByTeam.ContainsKey(teamId))
            {
                heroesByTeam[teamId].Remove(heroNetId);
                
                if (debugTeamUpdates)
                    Debug.Log($"[TeamManager] Hero {hero.name} unregistered from team {teamId}");
            }
        }
        
        [Server]
        private void ApplyTeamVisuals(Hero hero, int teamId)
        {
            // Apply team colors, effects, etc.
            TeamConfig config = GetTeamConfig(teamId);
            
            // Notify clients to update visuals
            RpcApplyTeamVisuals(hero.netId, teamId);
        }
        
        [ClientRpc]
        private void RpcApplyTeamVisuals(uint heroNetId, int teamId)
        {
            // Client-side application of team visuals
            if (NetworkClient.spawned.TryGetValue(heroNetId, out NetworkIdentity identity))
            {
                Hero hero = identity.GetComponent<Hero>();
                if (hero != null)
                {
                    TeamConfig config = GetTeamConfig(teamId);
                    // Apply visual changes (renderer colors, particle effects, etc.)
                    
                    if (debugTeamUpdates)
                        Debug.Log($"[TeamManager] Applied team {teamId} visuals to hero {hero.name}");
                }
            }
        }
        
        #endregion
        
        #region Team Information
        
        public TeamConfig GetTeamConfig(int teamId)
        {
            foreach (var config in teamConfigs)
            {
                if (config.teamId == teamId)
                {
                    return config;
                }
            }
            
            Debug.LogWarning($"No configuration found for team {teamId}");
            return null;
        }
        
        public Color GetTeamColor(int teamId)
        {
            TeamConfig config = GetTeamConfig(teamId);
            return config != null ? config.teamColor : Color.white;
        }
        
        public int GetTeamSize(int teamId)
        {
            if (IsValidTeamId(teamId) && playersByTeam.ContainsKey(teamId))
            {
                return playersByTeam[teamId].Count;
            }
            
            return 0;
        }
        
        public List<Hero> GetTeamHeroes(int teamId)
        {
            List<Hero> heroes = new List<Hero>();
            
            if (IsValidTeamId(teamId) && heroesByTeam.ContainsKey(teamId))
            {
                foreach (uint heroNetId in heroesByTeam[teamId])
                {
                    if (NetworkClient.spawned.TryGetValue(heroNetId, out NetworkIdentity identity))
                    {
                        Hero hero = identity.GetComponent<Hero>();
                        if (hero != null)
                        {
                            heroes.Add(hero);
                        }
                    }
                }
            }
            
            return heroes;
        }
        
        public int GetPlayerTeam(NetworkConnection player)
        {
            if (player == null || player.identity == null) return -1;
            
            uint netId = player.identity.netId;
            if (teamByPlayer.ContainsKey(netId))
            {
                return teamByPlayer[netId];
            }
            
            return -1;
        }
        
        public bool AreAllies(Hero hero1, Hero hero2)
        {
            if (hero1 == null || hero2 == null)
                return false;
                
            return hero1.TeamId == hero2.TeamId;
        }
        
        public bool AreEnemies(Hero hero1, Hero hero2)
        {
            if (hero1 == null || hero2 == null)
                return false;
                
            return hero1.TeamId != hero2.TeamId;
        }
        
        private bool IsValidTeamId(int teamId)
        {
            foreach (var config in teamConfigs)
            {
                if (config.teamId == teamId)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        #endregion
        
        #region Spawn Points
        
        public Transform GetTeamSpawnPoint(int teamId, int index = 0)
        {
            TeamConfig config = GetTeamConfig(teamId);
            
            if (config == null || config.spawnPoints == null || config.spawnPoints.Length == 0)
            {
                Debug.LogWarning($"No spawn points configured for team {teamId}");
                return null;
            }
            
            // Use modulo to wrap around if index is out of bounds
            int safeIndex = index % config.spawnPoints.Length;
            return config.spawnPoints[safeIndex];
        }
        
        public Transform GetRandomTeamSpawnPoint(int teamId)
        {
            TeamConfig config = GetTeamConfig(teamId);
            
            if (config == null || config.spawnPoints == null || config.spawnPoints.Length == 0)
            {
                Debug.LogWarning($"No spawn points configured for team {teamId}");
                return null;
            }
            
            int randomIndex = Random.Range(0, config.spawnPoints.Length);
            return config.spawnPoints[randomIndex];
        }
        
        #endregion
    }
}