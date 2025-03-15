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
        
        // Team tracking
        private readonly Dictionary<int, List<NetworkConnection>> playersByTeam = new Dictionary<int, List<NetworkConnection>>();
        private readonly Dictionary<NetworkConnection, int> teamByPlayer = new Dictionary<NetworkConnection, int>();
        private readonly Dictionary<int, List<Hero>> heroesByTeam = new Dictionary<int, List<Hero>>();
        
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
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Additional server-side initialization if needed
        }
        
        // Initialize team tracking dictionaries
        private void InitializeTeamDictionaries()
        {
            playersByTeam.Clear();
            teamByPlayer.Clear();
            heroesByTeam.Clear();
            
            // Initialize dictionaries for each team
            foreach (var config in teamConfigs)
            {
                playersByTeam[config.teamId] = new List<NetworkConnection>();
                heroesByTeam[config.teamId] = new List<Hero>();
            }
        }
        
        #region Team Assignment
        
        [Server]
        public int AssignPlayerToTeam(NetworkConnection player)
        {
            // If player already has a team, return that team
            if (teamByPlayer.TryGetValue(player, out int existingTeam))
            {
                return existingTeam;
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
            if (player.identity != null)
            {
                RpcNotifyTeamChange(player.identity.netId, oldTeamId, newTeamId);
            }
            
            return true;
        }
        
        [Server]
        private void AssignToTeam(NetworkConnection player, int teamId)
        {
            if (!IsValidTeamId(teamId))
            {
                Debug.LogError($"Cannot assign player to invalid team ID: {teamId}");
                return;
            }
            
            // Add to team tracking
            if (!playersByTeam[teamId].Contains(player))
            {
                playersByTeam[teamId].Add(player);
            }
            
            // Update player-to-team mapping
            teamByPlayer[player] = teamId;
            
            // Notify the player about their team assignment
            if (player.identity != null)
            {
                TargetNotifyTeamAssignment(player, teamId);
            }
            
            Debug.Log($"Player assigned to team {teamId}");
        }
        
        [Server]
        private int RemoveFromCurrentTeam(NetworkConnection player)
        {
            // If player isn't assigned to a team, return -1
            if (!teamByPlayer.TryGetValue(player, out int currentTeam))
            {
                return -1;
            }
            
            // Remove from team list
            if (playersByTeam.ContainsKey(currentTeam))
            {
                playersByTeam[currentTeam].Remove(player);
            }
            
            // Remove from mapping
            teamByPlayer.Remove(player);
            
            return currentTeam;
        }
        
        [TargetRpc]
        private void TargetNotifyTeamAssignment(NetworkConnection target, int teamId)
        {
            // Client-side notification of team assignment
            Debug.Log($"You've been assigned to team {teamId}");
            
            // Get team configuration for visuals, etc.
            TeamConfig config = GetTeamConfig(teamId);
            
            // Client could update UI or player indicator colors here
        }
        
        [ClientRpc]
        private void RpcNotifyTeamChange(uint playerNetId, int oldTeamId, int newTeamId)
        {
            // Client-side notification of team change for any player
            NetworkIdentity identity = null;
            if (NetworkClient.spawned.TryGetValue(playerNetId, out identity))
            {
                Debug.Log($"Player {identity.name} changed from team {oldTeamId} to {newTeamId}");
                
                // Update visual indicators, UI, etc.
            }
        }
        
        private int GetSmallestTeam()
        {
            int smallestTeamId = teamConfigs[0].teamId;
            int smallestCount = int.MaxValue;
            
            foreach (var config in teamConfigs)
            {
                int teamId = config.teamId;
                int playerCount = playersByTeam[teamId].Count;
                
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
            if (!IsValidTeamId(teamId) || hero == null)
                return;
                
            // Add hero to team tracking
            if (!heroesByTeam[teamId].Contains(hero))
            {
                heroesByTeam[teamId].Add(hero);
                
                // Set the hero's team
                hero.SetTeamId(teamId);
                
                // Apply team-specific visuals
                ApplyTeamVisuals(hero, teamId);
                
                Debug.Log($"Hero {hero.name} registered to team {teamId}");
            }
        }
        
        [Server]
        public void UnregisterHero(Hero hero)
        {
            if (hero == null)
                return;
                
            int teamId = hero.TeamId;
            
            if (IsValidTeamId(teamId) && heroesByTeam.ContainsKey(teamId))
            {
                heroesByTeam[teamId].Remove(hero);
                Debug.Log($"Hero {hero.name} unregistered from team {teamId}");
            }
        }
        
        [Server]
        private void ApplyTeamVisuals(Hero hero, int teamId)
        {
            // Apply team colors, effects, etc.
            TeamConfig config = GetTeamConfig(teamId);
            
            // Example: This would be replaced with your own visual system
            // For example, you might have a HeroVisuals component that handles team coloring
            
            // Notify clients to update visuals
            if (hero.netIdentity != null)
            {
                RpcApplyTeamVisuals(hero.netIdentity.netId, teamId);
            }
        }
        
        [ClientRpc]
        private void RpcApplyTeamVisuals(uint heroNetId, int teamId)
        {
            // Client-side application of team visuals
            NetworkIdentity identity = null;
            if (NetworkClient.spawned.TryGetValue(heroNetId, out identity))
            {
                Hero hero = identity.GetComponent<Hero>();
                if (hero != null)
                {
                    TeamConfig config = GetTeamConfig(teamId);
                    // Apply visual changes (renderer colors, particle effects, etc.)
                    
                    // For example:
                    // Renderer[] renderers = hero.GetComponentsInChildren<Renderer>();
                    // foreach (var renderer in renderers)
                    // {
                    //     // Apply team color to materials
                    // }
                    
                    Debug.Log($"Applied team {teamId} visuals to hero {hero.name}");
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
            if (IsValidTeamId(teamId) && heroesByTeam.ContainsKey(teamId))
            {
                return new List<Hero>(heroesByTeam[teamId]);
            }
            
            return new List<Hero>();
        }
        
        public int GetPlayerTeam(NetworkConnection player)
        {
            if (teamByPlayer.TryGetValue(player, out int teamId))
            {
                return teamId;
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