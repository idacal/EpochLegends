using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mirror;
using EpochLegends.Core.Hero;
using EpochLegends.Core.HeroSelection.Manager;
using EpochLegends.Core.HeroSelection.Registry;

namespace EpochLegends.UI.HeroSelection
{
    public class HeroSelectionUIController : NetworkBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text timerText;  // Soporta tanto Text como TextMeshProUGUI
        [SerializeField] private Transform heroGridContainer;
        [SerializeField] private GameObject heroCardPrefab;
        
        [Header("Hero Details")]
        [SerializeField] private GameObject heroDetailsPanel;
        [SerializeField] private Image heroImage;
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text heroRoleText;
        [SerializeField] private TMP_Text heroDescriptionText;
        [SerializeField] private Transform abilitiesContainer;
        [SerializeField] private GameObject abilityDisplayPrefab;
        
        [Header("Team Composition")]
        [SerializeField] private Transform[] teamPanels;
        [SerializeField] private GameObject playerSelectionPrefab;
        
        [Header("Selection Controls")]
        [SerializeField] private Button selectButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip readySound;
        [SerializeField] private AudioClip timerTickSound;
        
        // References
        private HeroSelectionManager selectionManager;
        private HeroRegistry heroRegistry;
        
        // State tracking
        private HeroDefinition selectedHero;
        private Dictionary<uint, PlayerSelectionDisplay> playerSelections = new Dictionary<uint, PlayerSelectionDisplay>();
        private List<HeroCard> heroCards = new List<HeroCard>();
        private bool isReady = false;
        private float lastSecond = 0;
        
        private void Awake()
        {
            // Find managers
            selectionManager = FindObjectOfType<HeroSelectionManager>();
            heroRegistry = FindObjectOfType<HeroRegistry>();
            
            if (selectionManager == null || heroRegistry == null)
            {
                Debug.LogError("[HeroSelectionUIController] Required managers not found in scene!");
                return;
            }
            
            // Set up button listeners
            selectButton.onClick.AddListener(OnSelectButtonClicked);
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            
            // Initially hide details panel until hero is selected
            heroDetailsPanel.SetActive(false);
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Register for hero selection events
            HeroSelectionManager.OnHeroSelected += OnHeroSelected;
            HeroSelectionManager.OnSelectionComplete += OnSelectionComplete;
            
            // Load hero grid
            PopulateHeroGrid();
            
            // Initialize player selection displays
            InitializePlayerSelections();
        }
        
        private void OnDestroy()
        {
            // Unregister from events - these operations are safe even if events have no subscribers
            HeroSelectionManager.OnHeroSelected -= OnHeroSelected;
            HeroSelectionManager.OnSelectionComplete -= OnSelectionComplete;
        }
        
        private void Update()
        {
            // Update timer display
            UpdateTimer();
        }
        
        #region UI Initialization
        
        private void PopulateHeroGrid()
        {
            // Clear existing hero cards
            foreach (Transform child in heroGridContainer)
            {
                Destroy(child.gameObject);
            }
            heroCards.Clear();
            
            // Get all heroes from registry
            List<HeroDefinition> heroes = heroRegistry.GetAllHeroes();
            
            // Create hero cards
            foreach (HeroDefinition hero in heroes)
            {
                GameObject cardObject = Instantiate(heroCardPrefab, heroGridContainer);
                HeroCard card = cardObject.GetComponent<HeroCard>();
                
                if (card != null)
                {
                    card.Initialize(hero);
                    card.OnClicked += OnHeroCardClicked;
                    heroCards.Add(card);
                }
            }
            
            Debug.Log($"[HeroSelectionUIController] Populated {heroes.Count} hero cards");
        }
        
        private void InitializePlayerSelections()
        {
            // Clear existing selections
            ClearPlayerSelections();
            
            // In a real implementation, this would get all connected players
            // For this demo, we'll create dummy entries for testing
            
            // Check if we're in networked mode or test mode
            if (NetworkClient.isConnected)
            {
                // In networked mode, wait for player data to sync
                // The entries will be created/updated when player data changes
                UpdatePlayerSelections();
            }
            else
            {
                // In test mode, create some dummy entries
                CreateDummyPlayerSelections();
            }
        }
        
        private void ClearPlayerSelections()
        {
            foreach (var display in playerSelections.Values)
            {
                if (display != null)
                {
                    Destroy(display.gameObject);
                }
            }
            playerSelections.Clear();
            
            // Clear team panels
            foreach (Transform teamPanel in teamPanels)
            {
                foreach (Transform child in teamPanel)
                {
                    // Skip static UI elements like headers
                    if (!child.CompareTag("DontDestroy"))
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }
        
        // For testing in non-networked mode
        private void CreateDummyPlayerSelections()
        {
            // Team 1 players (typically 5 players per team in MOBAs)
            for (int i = 0; i < 5; i++)
            {
                CreatePlayerSelectionDisplay(
                    (uint)(100 + i),  // Dummy netId
                    $"Player {i+1}",
                    1,  // Team 1
                    i == 0 ? selectedHero?.HeroId : "",
                    i == 0  // First player is local
                );
            }
            
            // Team 2 players
            for (int i = 0; i < 5; i++)
            {
                CreatePlayerSelectionDisplay(
                    (uint)(200 + i),  // Dummy netId
                    $"Player {i+6}",
                    2,  // Team 2
                    "",  // No hero selected
                    false  // Not local player
                );
            }
        }
        
        #endregion
        
        #region Hero Details
        
        private void ShowHeroDetails(HeroDefinition hero)
        {
            if (hero == null)
            {
                heroDetailsPanel.SetActive(false);
                selectedHero = null;
                return;
            }
            
            // Update selected hero
            selectedHero = hero;
            
            // Show panel
            heroDetailsPanel.SetActive(true);
            
            // Update hero details
            heroNameText.text = hero.DisplayName;
            heroRoleText.text = hero.Archetype.ToString();
            heroDescriptionText.text = hero.Description;
            
            if (heroImage != null && hero.HeroPortrait != null)
            {
                heroImage.sprite = hero.HeroPortrait;
            }
            
            // Clear ability container
            foreach (Transform child in abilitiesContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Populate abilities
            if (hero.Abilities != null)
            {
                foreach (var ability in hero.Abilities)
                {
                    GameObject abilityObj = Instantiate(abilityDisplayPrefab, abilitiesContainer);
                    AbilityDisplay abilityDisplay = abilityObj.GetComponent<AbilityDisplay>();
                    
                    if (abilityDisplay != null)
                    {
                        abilityDisplay.Initialize(ability);
                    }
                }
                
                Debug.Log($"[HeroSelectionUIController] Displayed {hero.Abilities.Count} abilities for {hero.DisplayName}");
            }
            
            // Update select button
            bool isHeroSelected = selectionManager.IsHeroSelectedByAnyPlayer(hero.HeroId);
            bool isLocalHeroSelection = IsLocalPlayerSelection(hero.HeroId);
            
            selectButton.interactable = !isHeroSelected || isLocalHeroSelection;
            // Obtener el texto del botón - funciona con Text o TMP_Text
            var buttonTextComponent = selectButton.GetComponentInChildren<TMP_Text>() as Component;
            if (buttonTextComponent == null)
                buttonTextComponent = selectButton.GetComponentInChildren<Text>();
                
            // Establecer el texto según el estado
            string buttonText = isLocalHeroSelection ? "Seleccionado" : (isHeroSelected ? "No Disponible" : "Seleccionar Héroe");
            
            if (buttonTextComponent is TMP_Text)
                ((TMP_Text)buttonTextComponent).text = buttonText;
            else if (buttonTextComponent is Text)
                ((Text)buttonTextComponent).text = buttonText;
        }
        
        #endregion
        
        #region Player Selection Display
        
        private void UpdatePlayerSelections()
        {
            if (teamPanels == null || teamPanels.Length < 2 || selectionManager == null)
                return;
                
            // In a real implementation, you'd get all connected players and their selections
            // For now, we're using a simplified approach
            
            // This would iterate through all connected players
            // For networked mode, you'd get this from GameManager
            var connectedPlayers = EpochLegends.GameManager.Instance?.ConnectedPlayers;
            
            if (connectedPlayers != null && connectedPlayers.Count > 0)
            {
                // Clear existing player entries
                ClearPlayerSelections();
                
                foreach (var playerEntry in connectedPlayers)
                {
                    uint netId = playerEntry.Key;
                    var playerInfo = playerEntry.Value;
                    
                    // Get player name - could get from identity
                    string playerName = "Player " + netId;
                    
                    // Determine if local player
                    bool isLocalPlayer = (NetworkClient.localPlayer != null && 
                                          NetworkClient.localPlayer.netId == netId);
                    
                    // Get selected hero if any
                    HeroDefinition selectedHero = null;
                    if (!string.IsNullOrEmpty(playerInfo.SelectedHeroId))
                    {
                        selectedHero = heroRegistry.GetHeroById(playerInfo.SelectedHeroId);
                    }
                    
                    // Create/update player entry
                    CreatePlayerSelectionDisplay(
                        netId,
                        playerName,
                        playerInfo.TeamId,
                        playerInfo.SelectedHeroId,
                        isLocalPlayer
                    );
                    
                    // Update ready status
                    if (playerSelections.TryGetValue(netId, out PlayerSelectionDisplay display))
                    {
                        display.SetReadyStatus(playerInfo.IsReady);
                        
                        // Update local ready state if this is local player
                        if (isLocalPlayer)
                        {
                            isReady = playerInfo.IsReady;
                            UpdateReadyButtonText();
                        }
                    }
                }
            }
            else
            {
                // Fallback to dummy data for testing
                CreateDummyPlayerSelections();
            }
        }
        
        private void CreatePlayerSelectionDisplay(uint netId, string playerName, int teamId, string heroId, bool isLocalPlayer)
        {
            // Validate team ID (1-based in our system)
            int teamIndex = teamId - 1;
            if (teamIndex < 0 || teamIndex >= teamPanels.Length)
                teamIndex = 0;

            Transform parentPanel = teamPanels[teamIndex];
            
            GameObject displayObj = Instantiate(playerSelectionPrefab, parentPanel);
            PlayerSelectionDisplay display = displayObj.GetComponent<PlayerSelectionDisplay>();
            
            if (display != null)
            {
                // Get hero definition if hero ID is provided
                HeroDefinition hero = null;
                if (!string.IsNullOrEmpty(heroId))
                {
                    hero = heroRegistry.GetHeroById(heroId);
                }
                
                display.Initialize(playerName, isLocalPlayer, hero);
                display.SetPlayerNetId(netId);
                playerSelections[netId] = display;
            }
        }
        
        private void UpdateLocalPlayerSelectionUI()
        {
            // Update ready button text
            UpdateReadyButtonText();
            
            // Update hero grid to show selected/unavailable heroes
            foreach (var card in heroCards)
            {
                bool isSelected = selectionManager.IsHeroSelectedByAnyPlayer(card.HeroId);
                bool isLocalSelection = IsLocalPlayerSelection(card.HeroId);
                
                card.SetState(
                    isLocalSelection ? HeroCardState.Selected : 
                    isSelected ? HeroCardState.Unavailable : 
                    HeroCardState.Available
                );
            }
            
            // Update hero details if currently viewing selected hero
            if (selectedHero != null)
            {
                ShowHeroDetails(selectedHero);
            }
        }
        
        private void UpdateReadyButtonText()
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = isReady ? "NO LISTO" : "LISTO";
            }
        }
        
        private bool IsLocalPlayerSelection(string heroId)
        {
            // Check if the local player has selected this hero
            if (NetworkClient.localPlayer != null)
            {
                uint localPlayerNetId = NetworkClient.localPlayer.netId;
                return selectionManager.GetSelectedHero(localPlayerNetId) == heroId;
            }
            
            // For test mode, just use player 1
            return selectionManager.GetSelectedHero(100) == heroId;
        }
        
        #endregion
        
        #region Timer & Game Flow
        
        private void UpdateTimer()
        {
            if (timerText != null && selectionManager != null)
            {
                float remainingTime = selectionManager.GetRemainingTime();
                timerText.text = $"Tiempo Restante: {Mathf.CeilToInt(remainingTime)}s";
                
                // Play tick sound on each second change when time is low
                int currentSecond = Mathf.CeilToInt(remainingTime);
                if (currentSecond != lastSecond && currentSecond <= 10 && currentSecond > 0)
                {
                    PlaySound(timerTickSound);
                    
                    // Make timer text red when time is low
                    if (currentSecond <= 5)
                    {
                        timerText.color = Color.red;
                    }
                }
                lastSecond = currentSecond;
            }
        }
        
        private bool CheckAllPlayersReady()
        {
            if (selectionManager == null) return false;
            
            // Check if all players are ready
            var connectedPlayers = EpochLegends.GameManager.Instance?.ConnectedPlayers;
            
            if (connectedPlayers != null && connectedPlayers.Count > 0)
            {
                foreach (var player in connectedPlayers.Values)
                {
                    if (!player.IsReady)
                    {
                        return false;
                    }
                }
                return true;
            }
            
            return false;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnHeroCardClicked(HeroDefinition hero)
        {
            ShowHeroDetails(hero);
            PlaySound(selectSound);
        }
        
        public void OnSelectButtonClicked()
        {
            if (selectedHero == null || selectionManager == null) return;
            
            // Send hero selection to server
            selectionManager.SelectHeroLocally(selectedHero.HeroId);
            
            // Update UI
            UpdateLocalPlayerSelectionUI();
            UpdatePlayerSelections();
            
            // Play selection sound
            PlaySound(selectSound);
        }
        
        public void OnReadyButtonClicked()
        {
            if (selectionManager == null) return;
            
            // Check if a hero is selected
            uint localPlayerNetId = NetworkClient.localPlayer?.netId ?? 100; // Use 100 for testing
            string selectedHeroId = selectionManager.GetSelectedHero(localPlayerNetId);
            
            if (string.IsNullOrEmpty(selectedHeroId))
            {
                // Cannot ready without selecting a hero
                // Show error message or feedback
                Debug.Log("Cannot ready up without selecting a hero first");
                return;
            }
            
            // Toggle ready status
            isReady = !isReady;
            
            // Send ready status to server
            selectionManager.SetReadyStatus(isReady);
            
            // Update UI
            UpdateLocalPlayerSelectionUI();
            UpdatePlayerSelections();
            
            // Play ready sound
            PlaySound(readySound);
        }
        
        private void OnHeroSelected(uint playerNetId, string heroId)
        {
            // Update the UI when any player selects a hero
            UpdateLocalPlayerSelectionUI();
            UpdatePlayerSelections();
        }
        
        private void OnSelectionComplete()
        {
            // Selection phase is over, UI will be transitioned automatically
            // by the game manager to the game scene
            Debug.Log("Hero selection complete");
        }
        
        #endregion
        
        #region Utility Methods
        
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        
        #endregion
    }
}