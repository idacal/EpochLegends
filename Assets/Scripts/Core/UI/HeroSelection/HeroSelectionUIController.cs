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
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Transform heroGridContainer;
        [SerializeField] private GameObject heroCardPrefab;
        
        [Header("Hero Details")]
        [SerializeField] private GameObject heroDetailsPanel;
        [SerializeField] private Image heroImage;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private TextMeshProUGUI heroRoleText;
        [SerializeField] private TextMeshProUGUI heroDescriptionText;
        [SerializeField] private Transform abilitiesContainer;
        [SerializeField] private GameObject abilityDisplayPrefab;
        
        [Header("Team Composition")]
        [SerializeField] private Transform[] teamPanels;
        [SerializeField] private GameObject playerSelectionPrefab;
        
        [Header("Selection Controls")]
        [SerializeField] private Button selectButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        
        // References
        private HeroSelectionManager selectionManager;
        private HeroRegistry heroRegistry;
        
        // State tracking
        private HeroDefinition selectedHero;
        private Dictionary<uint, PlayerSelectionDisplay> playerSelections = new Dictionary<uint, PlayerSelectionDisplay>();
        private List<HeroCard> heroCards = new List<HeroCard>();
        private bool isReady = false;
        
        private void Awake()
        {
            // Find managers
            selectionManager = FindObjectOfType<HeroSelectionManager>();
            heroRegistry = FindObjectOfType<HeroRegistry>();
            
            if (selectionManager == null || heroRegistry == null)
            {
                Debug.LogError("Required managers not found in scene!");
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
        }
        
        private void OnDestroy()
        {
            // Unregister from events
            HeroSelectionManager.OnHeroSelected -= OnHeroSelected;
            HeroSelectionManager.OnSelectionComplete -= OnSelectionComplete;
        }
        
        private void Update()
        {
            // Update timer
            UpdateTimer();
        }
        
        #region UI Initialization
        
        private void PopulateHeroGrid()
        {
            if (heroRegistry == null || heroCardPrefab == null || heroGridContainer == null)
                return;
                
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
        }
        
        private void ShowHeroDetails(HeroDefinition hero)
        {
            if (hero == null)
            {
                heroDetailsPanel.SetActive(false);
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
            if (hero.Abilities != null && abilityDisplayPrefab != null)
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
            }
            
            // Update select button
            bool isHeroSelected = selectionManager.IsHeroSelectedByAnyPlayer(hero.HeroId);
            bool isLocalHeroSelection = IsLocalPlayerSelection(hero.HeroId);
            
            selectButton.interactable = !isHeroSelected || isLocalHeroSelection;
            selectButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                isLocalHeroSelection ? "Selected" : (isHeroSelected ? "Not Available" : "Select Hero");
        }
        
        private void UpdateTimer()
        {
            if (timerText != null && selectionManager != null)
            {
                float remainingTime = selectionManager.GetRemainingTime();
                timerText.text = $"Time Remaining: {Mathf.CeilToInt(remainingTime)}s";
            }
        }
        
        #endregion
        
        #region Player Selection Display
        
        private void UpdatePlayerSelections()
        {
            if (teamPanels == null || teamPanels.Length < 2 || selectionManager == null)
                return;
                
            // For this demo, we're just showing two teams with player selections
            // In a real implementation, you'd get all connected players and their selections
                
            // Clear current displays
            foreach (var display in playerSelections.Values)
            {
                if (display != null)
                {
                    Destroy(display.gameObject);
                }
            }
            playerSelections.Clear();
            
            // This would iterate through all connected players in a real implementation
            // For now we'll use a simplified approach with dummy data
            CreateDummyPlayerSelections();
        }
        
        // This is just for demonstration - in a real implementation 
        // you'd get actual player data from the network
        private void CreateDummyPlayerSelections()
        {
            // Team 1 players
            for (int i = 0; i < 3; i++)
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
            for (int i = 0; i < 3; i++)
            {
                CreatePlayerSelectionDisplay(
                    (uint)(200 + i),  // Dummy netId
                    $"Player {i+4}",
                    2,  // Team 2
                    "",  // No hero selected
                    false  // Not local player
                );
            }
        }
        
        private void CreatePlayerSelectionDisplay(uint netId, string playerName, int teamId, string heroId, bool isLocalPlayer)
        {
            if (teamPanels == null || teamId < 1 || teamId > teamPanels.Length)
                return;
                
            // Get parent panel (0-based index)
            Transform parentPanel = teamPanels[teamId - 1];
            
            GameObject displayObj = Instantiate(playerSelectionPrefab, parentPanel);
            PlayerSelectionDisplay display = displayObj.GetComponent<PlayerSelectionDisplay>();
            
            if (display != null)
            {
                HeroDefinition hero = null;
                if (!string.IsNullOrEmpty(heroId))
                {
                    hero = heroRegistry.GetHeroById(heroId);
                }
                
                display.Initialize(playerName, isLocalPlayer, hero);
                playerSelections[netId] = display;
            }
        }
        
        private void UpdateLocalPlayerSelectionUI()
        {
            // Update ready button text
            readyButtonText.text = isReady ? "Not Ready" : "Ready";
            
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
        
        private bool IsLocalPlayerSelection(string heroId)
        {
            // In a real implementation, you'd check if the local player has selected this hero
            // For now, we'll use a simplified approach
            uint localPlayerNetId = 100; // Dummy ID from our example above
            
            return selectionManager.GetSelectedHero(localPlayerNetId) == heroId;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnHeroCardClicked(HeroDefinition hero)
        {
            ShowHeroDetails(hero);
        }
        
        private void OnSelectButtonClicked()
        {
            if (selectedHero == null || selectionManager == null) return;
            
            // Send hero selection to server
            selectionManager.SelectHeroLocally(selectedHero.HeroId);
            
            // Update UI
            UpdateLocalPlayerSelectionUI();
            UpdatePlayerSelections();
        }
        
        private void OnReadyButtonClicked()
        {
            if (selectionManager == null) return;
            
            // Toggle ready status
            isReady = !isReady;
            
            // Send ready status to server
            selectionManager.SetReadyStatus(isReady);
            
            // Update UI
            UpdateLocalPlayerSelectionUI();
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
    }
    
    #region Helper Classes
    
    // Enum to represent the state of a hero card
    public enum HeroCardState
    {
        Available,
        Selected,
        Unavailable
    }
    
    // Component for individual hero cards in the grid
    public class HeroCard : MonoBehaviour
    {
        [SerializeField] private Image heroIconImage;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button button;
        
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color selectedColor = Color.green;
        [SerializeField] private Color unavailableColor = Color.gray;
        
        private HeroDefinition heroDefinition;
        private HeroCardState currentState = HeroCardState.Available;
        
        // Event for when card is clicked
        public delegate void HeroCardClickHandler(HeroDefinition hero);
        public event HeroCardClickHandler OnClicked;
        
        public string HeroId => heroDefinition?.HeroId;
        
        private void Awake()
        {
            // Set up button click handler
            if (button != null)
            {
                button.onClick.AddListener(OnCardClicked);
            }
        }
        
        public void Initialize(HeroDefinition hero)
        {
            heroDefinition = hero;
            
            // Set icon and name
            if (heroIconImage != null && hero.HeroIcon != null)
            {
                heroIconImage.sprite = hero.HeroIcon;
            }
            
            if (heroNameText != null)
            {
                heroNameText.text = hero.DisplayName;
            }
            
            // Default to available state
            SetState(HeroCardState.Available);
        }
        
        public void SetState(HeroCardState state)
        {
            currentState = state;
            
            // Update visual state
            if (backgroundImage != null)
            {
                switch (state)
                {
                    case HeroCardState.Available:
                        backgroundImage.color = availableColor;
                        button.interactable = true;
                        break;
                    case HeroCardState.Selected:
                        backgroundImage.color = selectedColor;
                        button.interactable = true;
                        break;
                    case HeroCardState.Unavailable:
                        backgroundImage.color = unavailableColor;
                        button.interactable = false;
                        break;
                }
            }
        }
        
        private void OnCardClicked()
        {
            OnClicked?.Invoke(heroDefinition);
        }
    }
    
    // Component for displaying abilities in the hero details panel
    public class AbilityDisplay : MonoBehaviour
    {
        [SerializeField] private Image abilityIconImage;
        [SerializeField] private TextMeshProUGUI abilityNameText;
        [SerializeField] private TextMeshProUGUI abilityDescriptionText;
        
        public void Initialize(EpochLegends.Core.Ability.AbilityDefinition ability)
        {
            if (ability == null) return;
            
            // Set icon
            if (abilityIconImage != null && ability.AbilityIcon != null)
            {
                abilityIconImage.sprite = ability.AbilityIcon;
            }
            
            // Set name and description
            if (abilityNameText != null)
            {
                abilityNameText.text = ability.DisplayName;
            }
            
            if (abilityDescriptionText != null)
            {
                abilityDescriptionText.text = ability.Description;
            }
        }
    }
    
    // Component for player selection display in team panels
    public class PlayerSelectionDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image heroIconImage;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private GameObject localPlayerIndicator;
        [SerializeField] private GameObject noSelectionIndicator;
        
        public void Initialize(string playerName, bool isLocalPlayer, HeroDefinition selectedHero)
        {
            // Set player name
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }
            
            // Show/hide local player indicator
            if (localPlayerIndicator != null)
            {
                localPlayerIndicator.SetActive(isLocalPlayer);
            }
            
            // Set hero info if selected
            if (selectedHero != null)
            {
                if (heroIconImage != null && selectedHero.HeroIcon != null)
                {
                    heroIconImage.sprite = selectedHero.HeroIcon;
                    heroIconImage.gameObject.SetActive(true);
                }
                
                if (heroNameText != null)
                {
                    heroNameText.text = selectedHero.DisplayName;
                }
                
                if (noSelectionIndicator != null)
                {
                    noSelectionIndicator.SetActive(false);
                }
            }
            else
            {
                // No hero selected yet
                if (heroIconImage != null)
                {
                    heroIconImage.gameObject.SetActive(false);
                }
                
                if (heroNameText != null)
                {
                    heroNameText.text = "";
                }
                
                if (noSelectionIndicator != null)
                {
                    noSelectionIndicator.SetActive(true);
                }
            }
        }
    }
    
    #endregion
}