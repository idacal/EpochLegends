using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Core.Hero;
using EpochLegends.Core.Network;
using EpochLegends.Core.HeroSelection.Registry;
using EpochLegends.Core.UI.Manager;
using EpochLegends.Systems.Team.Manager;

namespace EpochLegends.Core.HeroSelection.Manager
{
    public class HeroSelectionManager : NetworkBehaviour
    {
        [Header("Selection Settings")]
        [SerializeField] private float selectionTime = 60f;
        [SerializeField] private bool enforceUniquePicks = true;
        [SerializeField] private bool randomIfNotSelected = true;
        
        [Header("References")]
        [SerializeField] private HeroRegistry heroRegistry;
        [SerializeField] private TeamManager teamManager;
        
        // Synced state
        [SyncVar(hook = nameof(OnTimerChanged))]
        private float remainingTime = 60f;
        
        // Selection tracking using standard dictionaries for server-side logic
        private readonly Dictionary<uint, string> selectedHeroes = new Dictionary<uint, string>();
        private readonly Dictionary<uint, bool> readyPlayers = new Dictionary<uint, bool>();
        
        // SyncVars for client display
        [SyncVar]
        private string syncSelectedHeroesJson = "{}";
        
        [SyncVar]
        private string syncReadyPlayersJson = "{}";
        
        // Private state
        private bool selectionInProgress = false;
        private bool allPlayersReady = false;
        
        // Events
        public delegate void HeroSelectionEvent(uint playerNetId, string heroId);
        public static event HeroSelectionEvent OnHeroSelected;
        
        public delegate void SelectionPhaseEvent();
        public static event SelectionPhaseEvent OnSelectionComplete;
        
        #region Lifecycle
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize
            selectedHeroes.Clear();
            readyPlayers.Clear();
            
            // Find references if not set
            if (heroRegistry == null) heroRegistry = FindObjectOfType<HeroRegistry>();
            if (teamManager == null) teamManager = FindObjectOfType<TeamManager>();
            
            // Register for network callbacks
            NetworkServer.RegisterHandler<HeroSelectionMessage>(OnHeroSelectionReceived);
            NetworkServer.RegisterHandler<ReadyStatusMessage>(OnReadyStatusReceived);
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Initial deserialization of dictionaries
            DeserializeSelectedHeroes();
            DeserializeReadyPlayers();
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
        }
        
        private void Update()
        {
            if (isServer && selectionInProgress)
            {
                // Update timer
                remainingTime -= Time.deltaTime;
                
                // Check if time expired
                if (remainingTime <= 0f)
                {
                    HandleTimeExpired();
                }
                
                // Check if all players are ready
                if (!allPlayersReady && AreAllPlayersReady())
                {
                    allPlayersReady = true;
                    StartSelectionCountdown();
                }
            }
        }
        
        #endregion
        
        #region Server Selection Management
        
        [Server]
        public void StartHeroSelection()
        {
            // Reset state
            selectedHeroes.Clear();
            readyPlayers.Clear();
            syncSelectedHeroesJson = "{}";
            syncReadyPlayersJson = "{}";
            remainingTime = selectionTime;
            allPlayersReady = false;
            selectionInProgress = true;
            
            // Notify clients
            RpcHeroSelectionStarted(selectionTime);
            
            Debug.Log("Hero selection phase started");
        }
        
        [Server]
        private void HandleTimeExpired()
        {
            // Time's up, ensure all players have selections
            if (randomIfNotSelected)
            {
                AssignRandomHeroesToNonReadyPlayers();
            }
            
            // Complete selection phase
            CompleteHeroSelection();
        }
        
        [Server]
        private void StartSelectionCountdown()
        {
            // All players ready, start a short countdown before completing
            float countdownTime = Mathf.Min(5f, remainingTime);
            RpcStartCountdown(countdownTime);
            
            // Set timer to countdown time
            remainingTime = countdownTime;
        }
        
        [Server]
        private void CompleteHeroSelection()
        {
            selectionInProgress = false;
            
            // Notify clients
            RpcHeroSelectionComplete();
            
            // Trigger event
            OnSelectionComplete?.Invoke();
            
            // Notify game manager to transition to next phase
            EpochLegends.GameManager gameManager = FindObjectOfType<EpochLegends.GameManager>();
            if (gameManager != null)
            {
                gameManager.OnHeroSelectionComplete(GetSelectionResults());
            }
            
            Debug.Log("Hero selection phase completed");
        }
        
        [Server]
        private void AssignRandomHeroesToNonReadyPlayers()
        {
            // Get all connected players
            foreach (NetworkConnection conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    uint netId = conn.identity.netId;
                    
                    // If player hasn't selected a hero or isn't ready
                    if (!selectedHeroes.ContainsKey(netId) || !readyPlayers.ContainsKey(netId) || !readyPlayers[netId])
                    {
                        // Assign random hero
                        string randomHeroId = GetRandomAvailableHero();
                        if (!string.IsNullOrEmpty(randomHeroId))
                        {
                            SelectHero(netId, randomHeroId);
                            SetPlayerReady(netId, true);
                        }
                    }
                }
            }
        }
        
        [Server]
        private void SelectHero(uint playerNetId, string heroId)
        {
            // Validate hero selection
            if (!IsHeroAvailable(heroId, playerNetId))
            {
                // If enforcing unique picks and this hero is already taken, pick another
                if (enforceUniquePicks)
                {
                    string alternateHero = GetRandomAvailableHero();
                    if (!string.IsNullOrEmpty(alternateHero))
                    {
                        heroId = alternateHero;
                    }
                }
            }
            
            // Update selection in dictionary
            selectedHeroes[playerNetId] = heroId;
            
            // Update synced JSON
            SerializeSelectedHeroes();
            
            // Trigger event locally and send to clients
            OnHeroSelected?.Invoke(playerNetId, heroId);
            RpcHeroSelected(playerNetId, heroId);
            
            Debug.Log($"Player {playerNetId} selected hero {heroId}");
        }
        
        [Server]
        private void SetPlayerReady(uint playerNetId, bool isReady)
        {
            // Update ready status
            readyPlayers[playerNetId] = isReady;
            
            // Update synced JSON
            SerializeReadyPlayers();
            
            // Notify clients
            RpcPlayerReadyChanged(playerNetId, isReady);
            
            Debug.Log($"Player {playerNetId} ready status set to {isReady}");
        }
        
        [Server]
        private bool AreAllPlayersReady()
        {
            // Check if we have any players
            if (NetworkServer.connections.Count == 0 || selectedHeroes.Count == 0)
                return false;
                
            // Check that all players have selected and are ready
            foreach (NetworkConnection conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    uint netId = conn.identity.netId;
                    
                    // If player is connected but not ready or hasn't selected
                    if (!readyPlayers.ContainsKey(netId) || !readyPlayers[netId] || 
                        !selectedHeroes.ContainsKey(netId))
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        [Server]
        private bool IsHeroAvailable(string heroId, uint playerNetId)
        {
            // Validate hero exists
            if (heroRegistry == null || !heroRegistry.DoesHeroExist(heroId))
                return false;
                
            // Check if enforcing unique picks
            if (enforceUniquePicks)
            {
                // Check if another player has already picked this hero
                foreach (var entry in selectedHeroes)
                {
                    if (entry.Key != playerNetId && entry.Value == heroId)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        [Server]
        private string GetRandomAvailableHero()
        {
            if (heroRegistry == null) return string.Empty;
            
            // Get all heroes
            List<string> allHeroIds = heroRegistry.GetAllHeroIds();
            
            // Shuffle the list
            allHeroIds = allHeroIds.OrderBy(x => Random.value).ToList();
            
            // Find first available hero
            foreach (string heroId in allHeroIds)
            {
                bool isAvailable = true;
                
                // Check if enforcing unique picks
                if (enforceUniquePicks)
                {
                    foreach (var entry in selectedHeroes)
                    {
                        if (entry.Value == heroId)
                        {
                            isAvailable = false;
                            break;
                        }
                    }
                }
                
                if (isAvailable)
                {
                    return heroId;
                }
            }
            
            // If we couldn't find an available hero, just return the first one
            if (allHeroIds.Count > 0)
            {
                return allHeroIds[0];
            }
            
            return string.Empty;
        }
        
        [Server]
        private Dictionary<uint, string> GetSelectionResults()
        {
            // Return a copy of the selected heroes dictionary
            return new Dictionary<uint, string>(selectedHeroes);
        }
        
        #endregion
        
        #region Dictionary Serialization
        
        // Serialize the selected heroes dictionary to JSON
        [Server]
        private void SerializeSelectedHeroes()
        {
            // Simple JSON format: {"key1":"value1","key2":"value2"}
            string json = "{";
            bool first = true;
            
            foreach (var kvp in selectedHeroes)
            {
                if (!first)
                    json += ",";
                    
                json += $"\"{kvp.Key}\":\"{kvp.Value}\"";
                first = false;
            }
            
            json += "}";
            syncSelectedHeroesJson = json;
        }
        
        // Serialize the ready players dictionary to JSON
        [Server]
        private void SerializeReadyPlayers()
        {
            // Simple JSON format: {"key1":true,"key2":false}
            string json = "{";
            bool first = true;
            
            foreach (var kvp in readyPlayers)
            {
                if (!first)
                    json += ",";
                    
                json += $"\"{kvp.Key}\":{kvp.Value.ToString().ToLower()}";
                first = false;
            }
            
            json += "}";
            syncReadyPlayersJson = json;
        }
        
        // Deserialize the selected heroes JSON to a dictionary
        [Client]
        private Dictionary<uint, string> DeserializeSelectedHeroes()
        {
            Dictionary<uint, string> result = new Dictionary<uint, string>();
            
            // Skip if JSON is empty or invalid
            if (string.IsNullOrEmpty(syncSelectedHeroesJson) || syncSelectedHeroesJson == "{}")
                return result;
                
            try
            {
                // Parse the JSON manually for simple format
                string content = syncSelectedHeroesJson.Trim('{', '}');
                string[] pairs = content.Split(',');
                
                foreach (string pair in pairs)
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    
                    string[] keyValue = pair.Split(':');
                    if (keyValue.Length != 2) continue;
                    
                    string key = keyValue[0].Trim('"');
                    string value = keyValue[1].Trim('"');
                    
                    if (uint.TryParse(key, out uint netId))
                    {
                        result[netId] = value;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error deserializing selected heroes: {e.Message}");
            }
            
            return result;
        }
        
        // Deserialize the ready players JSON to a dictionary
        [Client]
        private Dictionary<uint, bool> DeserializeReadyPlayers()
        {
            Dictionary<uint, bool> result = new Dictionary<uint, bool>();
            
            // Skip if JSON is empty or invalid
            if (string.IsNullOrEmpty(syncReadyPlayersJson) || syncReadyPlayersJson == "{}")
                return result;
                
            try
            {
                // Parse the JSON manually for simple format
                string content = syncReadyPlayersJson.Trim('{', '}');
                string[] pairs = content.Split(',');
                
                foreach (string pair in pairs)
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    
                    string[] keyValue = pair.Split(':');
                    if (keyValue.Length != 2) continue;
                    
                    string key = keyValue[0].Trim('"');
                    string value = keyValue[1].Trim();
                    
                    if (uint.TryParse(key, out uint netId) && bool.TryParse(value, out bool isReady))
                    {
                        result[netId] = isReady;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error deserializing ready players: {e.Message}");
            }
            
            return result;
        }
        
        #endregion
        
        #region Network Messages
        
        // Hero selection message from client
        private struct HeroSelectionMessage : NetworkMessage
        {
            public string heroId;
        }
        
        [Server]
        private void OnHeroSelectionReceived(NetworkConnection conn, HeroSelectionMessage msg)
        {
            if (!selectionInProgress || conn.identity == null) return;
            
            uint playerNetId = conn.identity.netId;
            
            // Validate and process hero selection
            if (IsHeroAvailable(msg.heroId, playerNetId))
            {
                SelectHero(playerNetId, msg.heroId);
            }
            else
            {
                // Notify client about invalid selection
                TargetInvalidSelection(conn, msg.heroId);
            }
        }
        
        // Ready status message from client
        private struct ReadyStatusMessage : NetworkMessage
        {
            public bool isReady;
        }
        
        [Server]
        private void OnReadyStatusReceived(NetworkConnection conn, ReadyStatusMessage msg)
        {
            if (!selectionInProgress || conn.identity == null) return;
            
            uint playerNetId = conn.identity.netId;
            
            // Only allow ready if a hero is selected
            if (msg.isReady && !selectedHeroes.ContainsKey(playerNetId))
            {
                TargetNotifyNoHeroSelected(conn);
                return;
            }
            
            // Update ready status
            SetPlayerReady(playerNetId, msg.isReady);
            
            // Check if all players are now ready
            if (msg.isReady && AreAllPlayersReady())
            {
                allPlayersReady = true;
                StartSelectionCountdown();
            }
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        private void RpcHeroSelectionStarted(float time)
        {
            // Show hero selection UI
            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowPanel(UIPanel.HeroSelection);
            }
            
            // Inform UI about time
            remainingTime = time;
            
            Debug.Log("Hero selection started");
        }
        
        [ClientRpc]
        private void RpcStartCountdown(float countdownTime)
        {
            Debug.Log($"All players ready. Selection completing in {countdownTime} seconds.");
            
            // UI could display countdown animation
        }
        
        [ClientRpc]
        private void RpcHeroSelectionComplete()
        {
            // Hide hero selection UI
            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowPanel(UIPanel.Loading);
            }
            
            Debug.Log("Hero selection completed");
        }
        
        [ClientRpc]
        private void RpcHeroSelected(uint playerNetId, string heroId)
        {
            // Trigger hero selected event for UI updates
            OnHeroSelected?.Invoke(playerNetId, heroId);
        }
        
        [ClientRpc]
        private void RpcPlayerReadyChanged(uint playerNetId, bool isReady)
        {
            // UI would update ready status indicators
            Debug.Log($"Player {playerNetId} ready status changed to {isReady}");
        }
        
        [TargetRpc]
        private void TargetInvalidSelection(NetworkConnection target, string heroId)
        {
            Debug.LogWarning($"Invalid hero selection: {heroId}");
            
            // UI could display error message
        }
        
        [TargetRpc]
        private void TargetNotifyNoHeroSelected(NetworkConnection target)
        {
            Debug.LogWarning("Cannot ready up without selecting a hero");
            
            // UI could display error message
        }
        
        #endregion
        
        #region Sync Hooks
        
        private void OnTimerChanged(float oldValue, float newValue)
        {
            // Update UI with new timer value
            Debug.Log($"Selection timer: {newValue:F1} seconds");
        }
        
        #endregion
        
        #region Client Methods
        
        [Client]
        public void SelectHeroLocally(string heroId)
        {
            if (!isClientOnly) return;
            
            // Send selection to server
            HeroSelectionMessage msg = new HeroSelectionMessage
            {
                heroId = heroId
            };
            
            NetworkClient.Send(msg);
        }
        
        [Client]
        public void SetReadyStatus(bool isReady)
        {
            if (!isClientOnly) return;
            
            // Send ready status to server
            ReadyStatusMessage msg = new ReadyStatusMessage
            {
                isReady = isReady
            };
            
            NetworkClient.Send(msg);
        }
        
        [Client]
        public float GetRemainingTime()
        {
            return remainingTime;
        }
        
        [Client]
        public bool IsHeroSelectedByAnyPlayer(string heroId)
        {
            // Get the current selected heroes from the synced JSON
            Dictionary<uint, string> currentSelections = DeserializeSelectedHeroes();
            
            // Check if any player has selected this hero
            foreach (var selection in currentSelections)
            {
                if (selection.Value == heroId)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        [Client]
        public string GetSelectedHero(uint playerNetId)
        {
            // Get the current selected heroes from the synced JSON
            Dictionary<uint, string> currentSelections = DeserializeSelectedHeroes();
            
            // Check if the player has selected a hero
            if (currentSelections.TryGetValue(playerNetId, out string heroId))
            {
                return heroId;
            }
            
            return string.Empty;
        }
        
        [Client]
        public bool IsPlayerReady(uint playerNetId)
        {
            // Get the current ready players from the synced JSON
            Dictionary<uint, bool> currentReadyPlayers = DeserializeReadyPlayers();
            
            // Check if the player is ready
            if (currentReadyPlayers.TryGetValue(playerNetId, out bool isReady))
            {
                return isReady;
            }
            
            return false;
        }
        
        #endregion
    }

    
}