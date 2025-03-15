using UnityEngine;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Systems.Team.Manager;

namespace EpochLegends.Systems.Team.Assignment
{
    public class TeamAssignment : NetworkBehaviour
    {
        [Header("Team Balance Settings")]
        [SerializeField] private bool balanceTeamsBySkill = true;
        [SerializeField] private bool allowManualTeamSelection = true;
        [SerializeField] private bool enablePartyPreservation = true;
        
        [Header("References")]
        [SerializeField] private TeamManager teamManager;
        
        // Party tracking for keeping groups together
        private Dictionary<string, List<NetworkConnection>> parties = new Dictionary<string, List<NetworkConnection>>();
        
        // Cached player skill ratings for balancing
        private Dictionary<NetworkConnection, float> playerSkillRatings = new Dictionary<NetworkConnection, float>();
        
        private void Awake()
        {
            // Find TeamManager if not assigned
            if (teamManager == null)
            {
                teamManager = FindObjectOfType<TeamManager>();
                
                if (teamManager == null)
                {
                    Debug.LogError("TeamAssignment requires a TeamManager reference!");
                }
            }
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Listen for player connection/disconnection events
            NetworkServer.RegisterHandler<PlayerSkillMessage>(OnPlayerSkillReceived);
            NetworkServer.RegisterHandler<TeamRequestMessage>(OnTeamRequestReceived);
            NetworkServer.RegisterHandler<PartyInfoMessage>(OnPartyInfoReceived);
        }
        
        #region Auto Team Assignment
        
        [Server]
        public void AssignTeamToPlayer(NetworkConnection player)
        {
            // If player is already assigned, do nothing
            if (teamManager.GetPlayerTeam(player) != -1)
                return;
                
            // Check if player is part of a party
            string partyId = GetPlayerPartyId(player);
            
            if (!string.IsNullOrEmpty(partyId) && enablePartyPreservation)
            {
                // If party has a team already, use that
                int partyTeam = GetPartyTeam(partyId);
                if (partyTeam != -1)
                {
                    AssignPlayerToTeam(player, partyTeam);
                    return;
                }
                
                // Otherwise, assign the whole party to a team
                AssignPartyToTeam(partyId);
            }
            else if (balanceTeamsBySkill)
            {
                // Balance based on player skill
                AssignTeamBySkill(player);
            }
            else
            {
                // Use TeamManager's default assignment (usually smallest team)
                teamManager.AssignPlayerToTeam(player);
            }
        }
        
        [Server]
        private void AssignTeamBySkill(NetworkConnection player)
        {
            // Get player skill rating
            float playerSkill = GetPlayerSkill(player);
            
            // Calculate team average skills
            Dictionary<int, float> teamAverageSkills = CalculateTeamAverageSkills();
            
            // Find team with lowest average skill
            int targetTeam = -1;
            float lowestAverage = float.MaxValue;
            
            foreach (var team in teamAverageSkills)
            {
                if (team.Value < lowestAverage)
                {
                    lowestAverage = team.Value;
                    targetTeam = team.Key;
                }
            }
            
            // If for some reason no team was found, use default assignment
            if (targetTeam == -1)
            {
                targetTeam = teamManager.AssignPlayerToTeam(player);
            }
            else
            {
                AssignPlayerToTeam(player, targetTeam);
            }
        }
        
        [Server]
        private Dictionary<int, float> CalculateTeamAverageSkills()
        {
            Dictionary<int, float> teamTotalSkills = new Dictionary<int, float>();
            Dictionary<int, int> teamPlayerCounts = new Dictionary<int, int>();
            Dictionary<int, float> teamAverageSkills = new Dictionary<int, float>();
            
            // Initialize dictionaries
            for (int i = 0; i < teamManager.TeamCount; i++)
            {
                TeamConfig config = teamManager.GetTeamConfig(i);
                if (config != null)
                {
                    teamTotalSkills[config.teamId] = 0f;
                    teamPlayerCounts[config.teamId] = 0;
                    teamAverageSkills[config.teamId] = 0f;
                }
            }
            
            // Calculate total skill per team
            foreach (var playerSkill in playerSkillRatings)
            {
                NetworkConnection player = playerSkill.Key;
                float skill = playerSkill.Value;
                
                int playerTeam = teamManager.GetPlayerTeam(player);
                if (playerTeam != -1 && teamTotalSkills.ContainsKey(playerTeam))
                {
                    teamTotalSkills[playerTeam] += skill;
                    teamPlayerCounts[playerTeam]++;
                }
            }
            
            // Calculate averages
            foreach (var team in teamTotalSkills.Keys)
            {
                if (teamPlayerCounts[team] > 0)
                {
                    teamAverageSkills[team] = teamTotalSkills[team] / teamPlayerCounts[team];
                }
                else
                {
                    teamAverageSkills[team] = 0f; // Empty team has 0 average
                }
            }
            
            return teamAverageSkills;
        }
        
        [Server]
        private void AssignPartyToTeam(string partyId)
        {
            if (!parties.TryGetValue(partyId, out List<NetworkConnection> partyMembers) || partyMembers.Count == 0)
                return;
                
            // Find team with most space
            int targetTeam = FindTeamWithMostSpace(partyMembers.Count);
            
            // Assign all party members to the same team
            foreach (var member in partyMembers)
            {
                AssignPlayerToTeam(member, targetTeam);
            }
            
            Debug.Log($"Assigned party {partyId} to team {targetTeam}");
        }
        
        [Server]
        private int FindTeamWithMostSpace(int requiredSpace)
        {
            int bestTeamId = -1;
            int maxAvailableSpace = -1;
            
            for (int i = 0; i < teamManager.TeamCount; i++)
            {
                TeamConfig config = teamManager.GetTeamConfig(i);
                if (config != null)
                {
                    int teamId = config.teamId;
                    int currentSize = teamManager.GetTeamSize(teamId);
                    int availableSpace = config.maxPlayers - currentSize;
                    
                    if (availableSpace >= requiredSpace && availableSpace > maxAvailableSpace)
                    {
                        maxAvailableSpace = availableSpace;
                        bestTeamId = teamId;
                    }
                }
            }
            
            // If no team has enough space, just return the one with most space
            if (bestTeamId == -1)
            {
                for (int i = 0; i < teamManager.TeamCount; i++)
                {
                    TeamConfig config = teamManager.GetTeamConfig(i);
                    if (config != null)
                    {
                        int teamId = config.teamId;
                        int currentSize = teamManager.GetTeamSize(teamId);
                        int availableSpace = config.maxPlayers - currentSize;
                        
                        if (availableSpace > maxAvailableSpace)
                        {
                            maxAvailableSpace = availableSpace;
                            bestTeamId = teamId;
                        }
                    }
                }
            }
            
            // If still no valid team, use first team
            if (bestTeamId == -1 && teamManager.TeamCount > 0)
            {
                bestTeamId = teamManager.GetTeamConfig(0).teamId;
            }
            
            return bestTeamId;
        }
        
        [Server]
        private void AssignPlayerToTeam(NetworkConnection player, int teamId)
        {
            // Request the team change from the TeamManager
            bool success = teamManager.RequestTeamChange(player, teamId);
            
            if (!success)
            {
                // Fallback to default assignment if specific assignment failed
                teamManager.AssignPlayerToTeam(player);
            }
        }
        
        #endregion
        
        #region Player Party & Skill Management
        
        [Server]
        private string GetPlayerPartyId(NetworkConnection player)
        {
            foreach (var party in parties)
            {
                if (party.Value.Contains(player))
                {
                    return party.Key;
                }
            }
            
            return string.Empty;
        }
        
        [Server]
        private int GetPartyTeam(string partyId)
        {
            if (!parties.TryGetValue(partyId, out List<NetworkConnection> partyMembers) || partyMembers.Count == 0)
                return -1;
                
            // Check if any party member is already assigned to a team
            foreach (var member in partyMembers)
            {
                int teamId = teamManager.GetPlayerTeam(member);
                if (teamId != -1)
                {
                    return teamId;
                }
            }
            
            return -1;
        }
        
        [Server]
        private float GetPlayerSkill(NetworkConnection player)
        {
            if (playerSkillRatings.TryGetValue(player, out float skill))
            {
                return skill;
            }
            
            // Default skill if not set
            return 1000f;
        }
        
        [Server]
        private void RegisterParty(string partyId, List<NetworkConnection> members)
        {
            if (string.IsNullOrEmpty(partyId) || members == null || members.Count == 0)
                return;
                
            parties[partyId] = new List<NetworkConnection>(members);
            
            Debug.Log($"Registered party {partyId} with {members.Count} members");
        }
        
        [Server]
        private void RegisterPlayerSkill(NetworkConnection player, float skillRating)
        {
            playerSkillRatings[player] = skillRating;
        }
        
        #endregion
        
        #region Network Message Handlers
        
        // Message struct for receiving player skill information
        private struct PlayerSkillMessage : NetworkMessage
        {
            public float skillRating;
        }
        
        [Server]
        private void OnPlayerSkillReceived(NetworkConnection conn, PlayerSkillMessage msg)
        {
            RegisterPlayerSkill(conn, msg.skillRating);
            
            // If auto-assignment is enabled, assign now that we have skill info
            AssignTeamToPlayer(conn);
        }
        
        // Message struct for player requesting team change
        private struct TeamRequestMessage : NetworkMessage
        {
            public int requestedTeamId;
        }
        
        [Server]
        private void OnTeamRequestReceived(NetworkConnection conn, TeamRequestMessage msg)
        {
            if (!allowManualTeamSelection)
            {
                // Inform player team selection is disabled
                return;
            }
            
            // Process team change request
            AssignPlayerToTeam(conn, msg.requestedTeamId);
        }
        
        // Message struct for party information
        private struct PartyInfoMessage : NetworkMessage
        {
            public string partyId;
            public uint[] memberNetIds;
        }
        
        [Server]
        private void OnPartyInfoReceived(NetworkConnection conn, PartyInfoMessage msg)
        {
            List<NetworkConnection> partyMembers = new List<NetworkConnection>();
            
            // Convert NetIds to NetworkConnections
            foreach (uint netId in msg.memberNetIds)
            {
                if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                {
                    if (identity.connectionToClient != null)
                    {
                        partyMembers.Add(identity.connectionToClient);
                    }
                }
            }
            
            // Register the party
            RegisterParty(msg.partyId, partyMembers);
            
            // If party preservation is enabled, try to assign the party to a team
            if (enablePartyPreservation)
            {
                AssignPartyToTeam(msg.partyId);
            }
        }
        
        #endregion
        
        #region Client Methods
        
        // Client can request team change
        [Client]
        public void RequestTeamChange(int teamId)
        {
            if (!allowManualTeamSelection)
                return;
                
            TeamRequestMessage msg = new TeamRequestMessage
            {
                requestedTeamId = teamId
            };
            
            NetworkClient.Send(msg);
        }
        
        // Client can send skill info
        [Client]
        public void SendPlayerSkill(float skillRating)
        {
            PlayerSkillMessage msg = new PlayerSkillMessage
            {
                skillRating = skillRating
            };
            
            NetworkClient.Send(msg);
        }
        
        // Client can send party info
        [Client]
        public void SendPartyInfo(string partyId, uint[] memberNetIds)
        {
            PartyInfoMessage msg = new PartyInfoMessage
            {
                partyId = partyId,
                memberNetIds = memberNetIds
            };
            
            NetworkClient.Send(msg);
        }
        
        #endregion
    }
}