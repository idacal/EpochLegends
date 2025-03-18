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
        
        // SyncDictionaries to store team memberships
        private readonly SyncDictionary<uint, int> syncPlayerTeams = new SyncDictionary<uint, int>();
        private readonly SyncDictionary<uint, int> syncHeroTeams = new SyncDictionary<uint, int>();
        
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
            // ELIMINADO: DontDestroyOnLoad(gameObject);
            // El ManagersController se encargará de preservar este objeto
            
            // Initialize team dictionaries
            InitializeTeamDictionaries();
            
            if (debugTeamUpdates)
                Debug.Log("[TeamManager] Initialized");
        }
        
        private void Update()
        {
            if(!isActiveAndEnabled) return;
            
            // Periodic check for changes
            if (Time.time - lastUpdateTime >= UPDATE_INTERVAL)
            {
                UpdateLocalDictionaries();
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
            
            // Register for dictionary changes
            // Note: No need for callbacks in this version of Mirror
            
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
        
        // Update client-side dictionaries based on syncvar data
        private void UpdateLocalDictionaries()
        {
            if(!isActiveAndEnabled) return;
            
            // Clear and rebuild dictionaries
            foreach (var config in teamConfigs)
            {
                playersByTeam[config.teamId] = new List<uint>();
                heroesByTeam[config.teamId] = new List<uint>();
            }
            
            // Populate from syncvars
            foreach (var playerTeam in syncPlayerTeams)
            {
                uint playerId = playerTeam.Key;
                int teamId = playerTeam.Value;
                
                if (playersByTeam.ContainsKey(teamId))
                {
                    playersByTeam[teamId].Add(playerId);
                }
            }
            
            foreach (var heroTeam in syncHeroTeams)
            {
                uint heroId = heroTeam.Key;
                int teamId = heroTeam.Value;
                
                if (heroesByTeam.ContainsKey(teamId))
                {
                    heroesByTeam[teamId].Add(heroId);
                }
            }
            
            if (debugTeamUpdates && isClient && Time.frameCount % 60 == 0) // Log only every 60 frames
                Debug.Log("[TeamManager] Local dictionaries updated");
                
            // Refresh UI when dictionaries update
            RefreshTeamUI();
        }
        
        // Helper method to refresh team UI
        private void RefreshTeamUI()
        {
            if(!isActiveAndEnabled) return;
            
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
        
        #region Team Assignment
        
        [Server]
        public int AssignPlayerToTeam(NetworkConnection player)
        {
            if(!isActiveAndEnabled) return -1;
            
            if (player == null || player.identity == null) return -1;
            
            uint netId = player.identity.netId;
            connectionToNetId[player] = netId;
            
            // If player already has a team, return that team
            if (syncPlayerTeams.ContainsKey(netId))
            {
                return syncPlayerTeams[netId];
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
            if(!isActiveAndEnabled) return false;
            
            if (player == null || player.identity == null) return false;
            uint netId = player.identity.netId;
            
            // Validate team ID
            if (!IsValidTeamId(newTeamId))
            {
                Debug.LogWarning($"Invalid team ID requested: {newTeamId}");
                return false;
            }
            
            // Check if team has space
            if (GetTeamSize(newTeamId) >= GetTeamConfig(newTeamId).maxPlayers)
            {
                Debug.LogWarning($"Team {newTeamId} is full");
                return false;
            }
            
            // If valid, change team
            int oldTeamId = -1;
            if (syncPlayerTeams.TryGetValue(netId, out oldTeamId))
            {
                // Only send notification if team actually changes
                if (oldTeamId != newTeamId)
                {
                    AssignToTeam(player, newTeamId);
                    
                    // Notify clients about team change
                    RpcNotifyTeamChange(netId, oldTeamId, newTeamId);
                }
                return true;
            }
            else
            {
                // Player not found in team, just assign them
                AssignToTeam(player, newTeamId);
                return true;
            }
        }
        
        [Server]
        private void AssignToTeam(NetworkConnection player, int teamId)
        {
            if(!isActiveAndEnabled) return;
            
            if (player == null || player.identity == null) return;
            uint netId = player.identity.netId;
            connectionToNetId[player] = netId;
            
            if (!IsValidTeamId(teamId))
            {
                Debug.LogError($"Cannot assign player to invalid team ID: {teamId}");
                return;
            }
            
            // Update player team in SyncDictionary
            syncPlayerTeams[netId] = teamId;
            
            // Notify the player about their team assignment
            TargetNotifyTeamAssignment(player, teamId);
            
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] Player {netId} assigned to team {teamId}");
                
            // Force UI refresh
            Invoke(nameof(ForceUIRefresh), 0.2f);
        }
        
        [Server]
        private void ForceUIRefresh()
        {
            if(!isActiveAndEnabled) return;
            
            RpcForceUIRefresh();
        }
        
        [ClientRpc]
        private void RpcForceUIRefresh()
        {
            if(!isActiveAndEnabled) return;
            
            RefreshTeamUI();
        }
        
        [TargetRpc]
        private void TargetNotifyTeamAssignment(NetworkConnection target, int teamId)
        {
            if(!isActiveAndEnabled) return;
            
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
            if(!isActiveAndEnabled) return;
            
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
                int playerCount = GetTeamSize(teamId);
                
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
            if(!isActiveAndEnabled) return;
            
            if (!IsValidTeamId(teamId) || hero == null) return;
            
            uint heroNetId = hero.netId;
            
            // Add hero to team tracking
            syncHeroTeams[heroNetId] = teamId;
            
            // Set the hero's team
            hero.SetTeamId(teamId);
            
            // Apply team-specific visuals
            ApplyTeamVisuals(hero, teamId);
            
            if (debugTeamUpdates)
                Debug.Log($"[TeamManager] Hero {hero.name} registered to team {teamId}");
        }
        
        [Server]
        public void UnregisterHero(Hero hero)
        {
            if(!isActiveAndEnabled) return;
            
            if (hero == null) return;
            
            int teamId = hero.TeamId;
            uint heroNetId = hero.netId;
            
            if (IsValidTeamId(teamId) && syncHeroTeams.ContainsKey(heroNetId))
            {
                syncHeroTeams.Remove(heroNetId);
                
                if (debugTeamUpdates)
                    Debug.Log($"[TeamManager] Hero {hero.name} unregistered from team {teamId}");
            }
        }
        
        [Server]
        private void ApplyTeamVisuals(Hero hero, int teamId)
        {
            if(!isActiveAndEnabled) return;
            
            // Apply team colors, effects, etc.
            TeamConfig config = GetTeamConfig(teamId);
            
            // Notify clients to update visuals
            RpcApplyTeamVisuals(hero.netId, teamId);
        }
        
        [ClientRpc]
        private void RpcApplyTeamVisuals(uint heroNetId, int teamId)
        {
            if(!isActiveAndEnabled) return;
            
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
            if(!isActiveAndEnabled) return null;
            
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
            if(!isActiveAndEnabled) return Color.white;
            
            TeamConfig config = GetTeamConfig(teamId);
            return config != null ? config.teamColor : Color.white;
        }
        
        public int GetTeamSize(int teamId)
        {
            if(!isActiveAndEnabled) return 0;
            
            if (playersByTeam.TryGetValue(teamId, out List<uint> players))
            {
                return players.Count;
            }
            
            return 0;
        }
        
        public List<Hero> GetTeamHeroes(int teamId)
        {
            if(!isActiveAndEnabled) return new List<Hero>();
            
            List<Hero> heroes = new List<Hero>();
            
            if (heroesByTeam.TryGetValue(teamId, out List<uint> heroNetIds))
            {
                foreach (uint heroNetId in heroNetIds)
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
            if(!isActiveAndEnabled) return -1;
            
            if (player == null || player.identity == null) return -1;
            
            uint netId = player.identity.netId;
            if (syncPlayerTeams.TryGetValue(netId, out int teamId))
            {
                return teamId;
            }
            
            return -1;
        }
        
        public bool AreAllies(Hero hero1, Hero hero2)
        {
            if(!isActiveAndEnabled) return false;
            
            if (hero1 == null || hero2 == null)
                return false;
                
            return hero1.TeamId == hero2.TeamId;
        }
        
        public bool AreEnemies(Hero hero1, Hero hero2)
        {
            if(!isActiveAndEnabled) return false;
            
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
            if(!isActiveAndEnabled) return null;
            
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
            if(!isActiveAndEnabled) return null;
            
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