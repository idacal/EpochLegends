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
        
        // Using standard SyncDictionaries with serializable types
        private readonly SyncDictionary<int, string> playersByTeamSerialized = new SyncDictionary<int, string>();
        private readonly SyncDictionary<uint, int> teamByPlayer = new SyncDictionary<uint, int>();
        private readonly SyncDictionary<int, string> heroesByTeamSerialized = new SyncDictionary<int, string>();
        
        // Client-side dictionaries converted from the serialized versions
        private Dictionary<int, List<uint>> playersByTeam = new Dictionary<int, List<uint>>();
        private Dictionary<int, List<uint>> heroesByTeam = new Dictionary<int, List<uint>>();
        
        // Previous state for change detection
        private Dictionary<int, string> prevPlayersByTeamSerialized = new Dictionary<int, string>();
        private Dictionary<uint, int> prevTeamByPlayer = new Dictionary<uint, int>();
        private Dictionary<int, string> prevHeroesByTeamSerialized = new Dictionary<int, string>();
        
        // Server-side cache for NetworkConnection to NetId mapping
        private Dictionary<NetworkConnection, uint> connectionToNetId = new Dictionary<NetworkConnection, uint>();
        
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
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Initialize local dictionaries
            UpdateLocalDictionaries();
            
            // Start monitoring for dictionary changes
            StartCoroutine(MonitorDictionaryChanges());
        }
        
        // Use a coroutine to monitor for dictionary changes instead of callbacks
        private System.Collections.IEnumerator MonitorDictionaryChanges()
        {
            // Initialize previous state
            UpdatePreviousState();
            
            while (true)
            {
                // Check for changes in playersByTeamSerialized
                foreach (var kvp in playersByTeamSerialized)
                {
                    if (!prevPlayersByTeamSerialized.TryGetValue(kvp.Key, out string prevValue) || 
                        prevValue != kvp.Value)
                    {
                        // Update local dictionary
                        playersByTeam[kvp.Key] = DeserializeUintList(kvp.Value);
                        
                        // Trigger change event
                        OnTeamPlayersChanged(kvp.Key);
                    }
                }
                
                // Check for removed teams in playersByTeamSerialized
                List<int> keysToRemove = new List<int>();
                foreach (var prevKvp in prevPlayersByTeamSerialized)
                {
                    if (!playersByTeamSerialized.ContainsKey(prevKvp.Key))
                    {
                        keysToRemove.Add(prevKvp.Key);
                    }
                }
                
                foreach (int key in keysToRemove)
                {
                    playersByTeam.Remove(key);
                    OnTeamPlayersChanged(key);
                }
                
                // Check for changes in teamByPlayer
                foreach (var kvp in teamByPlayer)
                {
                    if (!prevTeamByPlayer.TryGetValue(kvp.Key, out int prevValue) || 
                        prevValue != kvp.Value)
                    {
                        // Trigger change event
                        OnPlayerTeamChanged(kvp.Key, kvp.Value);
                    }
                }
                
                // Check for removed players in teamByPlayer
                List<uint> playerKeysToRemove = new List<uint>();
                foreach (var prevKvp in prevTeamByPlayer)
                {
                    if (!teamByPlayer.ContainsKey(prevKvp.Key))
                    {
                        playerKeysToRemove.Add(prevKvp.Key);
                    }
                }
                
                foreach (uint key in playerKeysToRemove)
                {
                    OnPlayerTeamChanged(key, -1); // -1 indicates removal
                }
                
                // Check for changes in heroesByTeamSerialized
                foreach (var kvp in heroesByTeamSerialized)
                {
                    if (!prevHeroesByTeamSerialized.TryGetValue(kvp.Key, out string prevValue) || 
                        prevValue != kvp.Value)
                    {
                        // Update local dictionary
                        heroesByTeam[kvp.Key] = DeserializeUintList(kvp.Value);
                        
                        // Trigger change event
                        OnTeamHeroesChanged(kvp.Key);
                    }
                }
                
                // Check for removed teams in heroesByTeamSerialized
                keysToRemove.Clear();
                foreach (var prevKvp in prevHeroesByTeamSerialized)
                {
                    if (!heroesByTeamSerialized.ContainsKey(prevKvp.Key))
                    {
                        keysToRemove.Add(prevKvp.Key);
                    }
                }
                
                foreach (int key in keysToRemove)
                {
                    heroesByTeam.Remove(key);
                    OnTeamHeroesChanged(key);
                }
                
                // Update previous state
                UpdatePreviousState();
                
                // Wait before checking again
                yield return new WaitForSeconds(0.2f);
            }
        }
        
        // Update the previous state for change detection
        private void UpdatePreviousState()
        {
            // Update playersByTeamSerialized
            prevPlayersByTeamSerialized.Clear();
            foreach (var kvp in playersByTeamSerialized)
            {
                prevPlayersByTeamSerialized[kvp.Key] = kvp.Value;
            }
            
            // Update teamByPlayer
            prevTeamByPlayer.Clear();
            foreach (var kvp in teamByPlayer)
            {
                prevTeamByPlayer[kvp.Key] = kvp.Value;
            }
            
            // Update heroesByTeamSerialized
            prevHeroesByTeamSerialized.Clear();
            foreach (var kvp in heroesByTeamSerialized)
            {
                prevHeroesByTeamSerialized[kvp.Key] = kvp.Value;
            }
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
                    // Initialize with empty list serialized as string
                    if (!playersByTeamSerialized.ContainsKey(config.teamId))
                        playersByTeamSerialized[config.teamId] = "";
                }
                
                if (!heroesByTeam.ContainsKey(config.teamId))
                {
                    heroesByTeam[config.teamId] = new List<uint>();
                    // Initialize with empty list serialized as string
                    if (!heroesByTeamSerialized.ContainsKey(config.teamId))
                        heroesByTeamSerialized[config.teamId] = "";
                }
            }
            
            Debug.Log($"Team dictionaries initialized for {teamConfigs.Length} teams");
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
        
        // Update client-side dictionaries based on serialized versions
        private void UpdateLocalDictionaries()
        {
            // Update playersByTeam
            foreach (var kvp in playersByTeamSerialized)
            {
                playersByTeam[kvp.Key] = DeserializeUintList(kvp.Value);
            }
            
            // Update heroesByTeam
            foreach (var kvp in heroesByTeamSerialized)
            {
                heroesByTeam[kvp.Key] = DeserializeUintList(kvp.Value);
            }
        }
        
        // Event handlers for dictionary changes
        private void OnTeamPlayersChanged(int teamId)
        {
            Debug.Log($"Players in team {teamId} changed");
            // Update UI for team members
        }
        
        private void OnPlayerTeamChanged(uint playerId, int teamId)
        {
            Debug.Log($"Player {playerId} team assignment changed to team {teamId}");
            // Update player UI for team assignment
        }
        
        private void OnTeamHeroesChanged(int teamId)
        {
            Debug.Log($"Heroes in team {teamId} changed");
            // Update UI for team heroes
        }
        
        #region Team Assignment
        
        [Server]
        public int AssignPlayerToTeam(NetworkConnection player)
        {
            if (player.identity == null) return -1;
            
            uint netId = player.identity.netId;
            connectionToNetId[player] = netId;
            
            // If player already has a team, return that team
            if (teamByPlayer.TryGetValue(netId, out int existingTeam))
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
            if (player.identity == null) return false;
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
            if (player.identity == null) return;
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
                // Update the serialized version
                playersByTeamSerialized[teamId] = SerializeUintList(playersByTeam[teamId]);
            }
            
            // Update player-to-team mapping
            teamByPlayer[netId] = teamId;
            
            // Notify the player about their team assignment
            TargetNotifyTeamAssignment(player, teamId);
            
            Debug.Log($"Player {netId} assigned to team {teamId}");
        }
        
        [Server]
        private int RemoveFromCurrentTeam(NetworkConnection player)
        {
            if (player.identity == null) return -1;
            uint netId = player.identity.netId;
            
            // If player isn't assigned to a team, return -1
            if (!teamByPlayer.TryGetValue(netId, out int currentTeam))
            {
                return -1;
            }
            
            // Remove from team list
            if (playersByTeam.ContainsKey(currentTeam))
            {
                playersByTeam[currentTeam].Remove(netId);
                // Update the serialized version
                playersByTeamSerialized[currentTeam] = SerializeUintList(playersByTeam[currentTeam]);
            }
            
            // Remove from mapping
            teamByPlayer.Remove(netId);
            
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
            if (NetworkClient.spawned.TryGetValue(playerNetId, out NetworkIdentity identity))
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
                // Update the serialized version
                heroesByTeamSerialized[teamId] = SerializeUintList(heroesByTeam[teamId]);
                
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
            if (hero == null) return;
            
            int teamId = hero.TeamId;
            uint heroNetId = hero.netId;
            
            if (IsValidTeamId(teamId) && heroesByTeam.ContainsKey(teamId))
            {
                heroesByTeam[teamId].Remove(heroNetId);
                // Update the serialized version
                heroesByTeamSerialized[teamId] = SerializeUintList(heroesByTeam[teamId]);
                
                Debug.Log($"Hero {hero.name} unregistered from team {teamId}");
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
            if (player.identity == null) return -1;
            
            uint netId = player.identity.netId;
            if (teamByPlayer.TryGetValue(netId, out int teamId))
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