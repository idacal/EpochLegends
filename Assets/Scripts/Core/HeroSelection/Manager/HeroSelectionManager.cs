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
        [SyncVar(hook = nameof(UpdateTimerDisplay))]
        private float remainingTime = 60f;
        
        // Selection tracking
        private readonly SyncDictionary<uint, string> selectedHeroes = new SyncDictionary<uint, string>();
        private readonly SyncDictionary<uint, bool> readyPlayers = new SyncDictionary<uint, bool>();
        
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
            
            // Register for sync events
            selectedHeroes.Callback += OnSelectedHeroesUpdated;
            readyPlayers.Callback += OnReadyPlayersUpdated;
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
            GameManager gameManager = FindObjectOfType<GameManager>();
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
            
            // Update selection
            selectedHeroes[playerNetId] = heroId;
            
            // Trigger event
            OnHeroSelected?.Invoke(playerNetId, heroId);
            
            Debug.Log($"Player {playerNetId} selected hero {heroId}");
        }
        
        [Server]
        private void SetPlayerReady(uint playerNetId, bool isReady)
        {
            readyPlayers[playerNetId] = isReady;
            
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
            // Convert SyncDictionary to regular Dictionary for return
            Dictionary<uint, string> results = new Dictionary<uint, string>();
            
            foreach (var entry in selectedHeroes)
            {
                results[entry.Key] = entry.Value;
            }
            
            return results;
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
        
        #region Sync Var Hooks
        
        // This method is used as a hook for remainingTime SyncVar
        private void UpdateTimerDisplay(float oldValue, float newValue)
        {
            // Update UI with new timer value
            Debug.Log($"Selection timer: {newValue:F1} seconds");
        }
        
        private void OnSelectedHeroesUpdated(SyncDictionary<uint, string>.Operation op, uint key, string item)
        {
            // Update UI when selections change
            Debug.Log($"Selection updated: Player {key}, Hero {item}");
            
            // UI would update hero selection display
        }
        
        private void OnReadyPlayersUpdated(SyncDictionary<uint, bool>.Operation op, uint key, bool item)
        {
            // Update UI when ready status changes
            Debug.Log($"Ready status updated: Player {key}, Ready {item}");
            
            // UI would update ready status indicators
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
            foreach (var entry in selectedHeroes)
            {
                if (entry.Value == heroId)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        [Client]
        public string GetSelectedHero(uint playerNetId)
        {
            if (selectedHeroes.TryGetValue(playerNetId, out string heroId))
            {
                return heroId;
            }
            
            return string.Empty;
        }
        
        [Client]
        public bool IsPlayerReady(uint playerNetId)
        {
            if (readyPlayers.TryGetValue(playerNetId, out bool isReady))
            {
                return isReady;
            }
            
            return false;
        }
        
        #endregion
    }
}