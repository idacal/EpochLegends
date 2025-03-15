using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Hero;
using EpochLegends.Core.Ability;
using EpochLegends.Core.Player.Controller;

namespace EpochLegends.UI.Game
{
    public class GameHUDController : NetworkBehaviour
    {
        [Header("Hero Status")]
        [SerializeField] private Image healthBar;
        [SerializeField] private Image manaBar;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image heroPortrait;
        
        [Header("Abilities")]
        [SerializeField] private Transform abilitiesContainer;
        [SerializeField] private GameObject abilitySlotPrefab;
        [SerializeField] private KeyCode[] abilityHotkeys = new KeyCode[] { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
        
        [Header("Game Info")]
        [SerializeField] private TextMeshProUGUI gameTimerText;
        [SerializeField] private TextMeshProUGUI scoreText;
        
        [Header("Minimap")]
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RenderTexture minimapRenderTexture;
        
        [Header("Menus")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button disconnectButton;
        
        // Referencias
        private Hero localHero;
        private PlayerController playerController;
        
        // Cached UI elements
        private List<AbilitySlot> abilitySlots = new List<AbilitySlot>();
        private bool isMenuOpen = false;
        
        private void Awake()
        {
            // Set up button listeners
            resumeButton.onClick.AddListener(OnResumeClicked);
            optionsButton.onClick.AddListener(OnOptionsClicked);
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
            
            // Initialize UI
            InitializeUI();
        }
        
        private void Start()
        {
            // Initially hide pause menu
            SetPauseMenuVisible(false);
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Start looking for local hero to track
            StartCoroutine(FindLocalHero());
        }
        
        private void Update()
        {
            // Handle pause menu toggle
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePauseMenu();
            }
            
            // Update UI elements
            if (localHero != null)
            {
                UpdateHeroStatus();
                UpdateAbilities();
            }
            
            // Update game info
            UpdateGameInfo();
        }
        
        #region UI Initialization
        
        private void InitializeUI()
        {
            // Clear abilities container
            foreach (Transform child in abilitiesContainer)
            {
                Destroy(child.gameObject);
            }
            abilitySlots.Clear();
            
            // Create ability slots based on hotkeys
            for (int i = 0; i < abilityHotkeys.Length; i++)
            {
                GameObject slotObj = Instantiate(abilitySlotPrefab, abilitiesContainer);
                AbilitySlot slot = slotObj.GetComponent<AbilitySlot>();
                
                if (slot != null)
                {
                    slot.Initialize(abilityHotkeys[i].ToString());
                    abilitySlots.Add(slot);
                }
            }
            
            // Set up minimap
            if (minimapCamera != null && minimapRenderTexture != null && minimapImage != null)
            {
                minimapCamera.targetTexture = minimapRenderTexture;
                minimapImage.texture = minimapRenderTexture;
            }
        }
        
        #endregion
        
        #region Hero Tracking
        
        private System.Collections.IEnumerator FindLocalHero()
        {
            while (localHero == null)
            {
                // Try to find the local player first
                PlayerController[] controllers = FindObjectsOfType<PlayerController>();
                foreach (var controller in controllers)
                {
                    // En tu implementación, verifica si el controller es local
                    if (IsLocalController(controller))
                    {
                        playerController = controller;
                        localHero = controller.ControlledHero;
                        
                        if (localHero != null)
                        {
                            // Hero found, initialize HUD with hero data
                            InitializeHeroUI();
                            yield break;
                        }
                    }
                }
                
                // If not found directly, try to find any hero with local authority
                Hero[] heroes = FindObjectsOfType<Hero>();
                foreach (var hero in heroes)
                {
                    // Verifica si el héroe es local
                    if (IsLocalHero(hero))
                    {
                        localHero = hero;
                        
                        // Hero found, initialize HUD with hero data
                        InitializeHeroUI();
                        yield break;
                    }
                }
                
                // Wait a short time before trying again
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // Esta función verifica si un controlador pertenece al jugador local
        // Adapta esta lógica según cómo implementaste esto en tu proyecto
        private bool IsLocalController(PlayerController controller)
        {
            // Ejemplo: podría ser que tengas un campo isLocalPlayer, o podrías
            // comparar con un ID de jugador local almacenado en algún lado
            
            // Por ahora, asumo que el primer controlador que encontramos es el local
            // Pero deberías reemplazar esto con tu lógica concreta
            return true; 
        }
        
        // Esta función verifica si un héroe pertenece al jugador local
        // Adapta según tu implementación
        private bool IsLocalHero(Hero hero)
        {
            // Similar a IsLocalController, adapta según tu lógica
            // Por ahora, asumo que el primer héroe que encontramos es el local
            return true;
        }
        
        private void InitializeHeroUI()
        {
            if (localHero == null) return;
            
            // Set hero info
            if (heroNameText != null && localHero.HeroDefinition != null)
            {
                heroNameText.text = localHero.HeroDefinition.DisplayName;
            }
            
            if (heroPortrait != null && localHero.HeroDefinition != null && localHero.HeroDefinition.HeroPortrait != null)
            {
                heroPortrait.sprite = localHero.HeroDefinition.HeroPortrait;
            }
            
            // Set up ability slots
            List<BaseAbility> abilities = localHero.Abilities;
            for (int i = 0; i < abilities.Count && i < abilitySlots.Count; i++)
            {
                abilitySlots[i].SetAbility(abilities[i]);
            }
            
            Debug.Log($"HUD initialized for hero: {localHero.name}");
        }
        
        #endregion
        
        #region UI Updates
        
        private void UpdateHeroStatus()
        {
            if (localHero == null) return;
            
            // Update health
            if (healthBar != null)
            {
                float healthPercent = localHero.CurrentHealth / localHero.Stats.MaxHealth;
                healthBar.fillAmount = Mathf.Clamp01(healthPercent);
            }
            
            if (healthText != null)
            {
                healthText.text = $"{Mathf.Floor(localHero.CurrentHealth)}/{Mathf.Floor(localHero.Stats.MaxHealth)}";
            }
            
            // Update mana
            if (manaBar != null)
            {
                float manaPercent = localHero.CurrentMana / localHero.Stats.MaxMana;
                manaBar.fillAmount = Mathf.Clamp01(manaPercent);
            }
            
            if (manaText != null)
            {
                manaText.text = $"{Mathf.Floor(localHero.CurrentMana)}/{Mathf.Floor(localHero.Stats.MaxMana)}";
            }
            
            // Update level
            if (levelText != null)
            {
                levelText.text = $"Lvl {localHero.Level}";
            }
        }
        
        private void UpdateAbilities()
        {
            if (localHero == null) return;
            
            List<BaseAbility> abilities = localHero.Abilities;
            for (int i = 0; i < abilities.Count && i < abilitySlots.Count; i++)
            {
                abilitySlots[i].UpdateCooldown(abilities[i]);
            }
        }
        
        private void UpdateGameInfo()
        {
            // Update game timer (this would come from the game manager in a real implementation)
            if (gameTimerText != null)
            {
                float gameTime = Time.time; // Placeholder, would use actual game time
                int minutes = Mathf.FloorToInt(gameTime / 60);
                int seconds = Mathf.FloorToInt(gameTime % 60);
                gameTimerText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // Update score (this would come from the game manager in a real implementation)
            if (scoreText != null)
            {
                // Placeholder scores
                int team1Score = 0;
                int team2Score = 0;
                scoreText.text = $"{team1Score} - {team2Score}";
            }
        }
        
        #endregion
        
        #region Menu Management
        
        private void TogglePauseMenu()
        {
            SetPauseMenuVisible(!isMenuOpen);
        }
        
        private void SetPauseMenuVisible(bool visible)
        {
            isMenuOpen = visible;
            pauseMenuPanel.SetActive(visible);
            
            // In a real implementation, you might also pause the game
            // when the menu is open, but we'll just toggle visibility for now
        }
        
        private void OnResumeClicked()
        {
            SetPauseMenuVisible(false);
        }
        
        private void OnOptionsClicked()
        {
            // Show options panel
            Debug.Log("Options clicked - would show options panel");
        }
        
        private void OnDisconnectClicked()
        {
            // Disconnect from server and return to main menu
            // Adapta este código a tu implementación de NetworkManager
            
            if (NetworkManager.singleton != null)
            {
                // Si tu NetworkManager tiene un método diferente para desconectar, úsalo aquí
                NetworkManager.singleton.StopClient();
            }
            // En una implementación real, usarías Scene Management para volver al menú principal
        }
        
        #endregion
    }
    
    #region Helper Classes
    
    // Component for individual ability slots in the HUD
    public class AbilitySlot : MonoBehaviour
    {
        [SerializeField] private Image abilityIcon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private TextMeshProUGUI hotkeyText;
        
        private BaseAbility linkedAbility;
        
        public void Initialize(string hotkeyLabel)
        {
            // Set hotkey label
            if (hotkeyText != null)
            {
                hotkeyText.text = hotkeyLabel;
            }
            
            // Reset cooldown display
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.gameObject.SetActive(false);
            }
            
            if (cooldownText != null)
            {
                cooldownText.text = "";
                cooldownText.gameObject.SetActive(false);
            }
        }
        
        public void SetAbility(BaseAbility ability)
        {
            linkedAbility = ability;
            
            // Set ability icon
            if (abilityIcon != null && ability.Definition != null && ability.Definition.AbilityIcon != null)
            {
                abilityIcon.sprite = ability.Definition.AbilityIcon;
                abilityIcon.gameObject.SetActive(true);
            }
            else if (abilityIcon != null)
            {
                abilityIcon.gameObject.SetActive(false);
            }
        }
        
        public void UpdateCooldown(BaseAbility ability)
        {
            if (ability == null) return;
            
            bool isOnCooldown = ability.IsOnCooldown;
            float cooldownPercent = ability.CurrentCooldown / ability.MaxCooldown;
            
            // Update cooldown overlay
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(isOnCooldown);
                cooldownOverlay.fillAmount = cooldownPercent;
            }
            
            // Update cooldown text
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(isOnCooldown);
                if (isOnCooldown)
                {
                    cooldownText.text = Mathf.Ceil(ability.CurrentCooldown).ToString();
                }
                else
                {
                    cooldownText.text = "";
                }
            }
        }
    }
    
    #endregion
}