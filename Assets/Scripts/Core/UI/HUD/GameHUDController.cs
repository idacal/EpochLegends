using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Hero;
using EpochLegends.Core.Ability;
using EpochLegends.Core.Player.Controller;
using EpochLegends.Core.UI.Game;

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
        
        [Header("Ability System")]
        [SerializeField] private AbilityUIManager abilityUIManager;
        
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
        private EpochLegends.Core.Hero.Hero localHero;
        private PlayerController playerController;
        
        // Estado de UI
        private bool isMenuOpen = false;
        
        private void Awake()
        {
            // Set up button listeners
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
            if (disconnectButton != null) disconnectButton.onClick.AddListener(OnDisconnectClicked);
            
            // Verificar que tenemos el AbilityUIManager
            if (abilityUIManager == null)
            {
                abilityUIManager = GetComponentInChildren<AbilityUIManager>();
                if (abilityUIManager == null)
                {
                    Debug.LogError("GameHUDController: No se encontró AbilityUIManager. La UI de habilidades no funcionará.");
                }
            }
        }
        
        private void Start()
        {
            // Initially hide pause menu
            SetPauseMenuVisible(false);
            
            // Set up minimap
            if (minimapCamera != null && minimapRenderTexture != null && minimapImage != null)
            {
                minimapCamera.targetTexture = minimapRenderTexture;
                minimapImage.texture = minimapRenderTexture;
            }
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
                // Note: La actualización de habilidades ahora es manejada por AbilityUIManager
            }
            
            // Update game info
            UpdateGameInfo();
        }
        
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
                            Debug.Log($"GameHUDController: Héroe local encontrado a través del controlador: {localHero.name}");
                            InitializeHeroUI();
                            yield break;
                        }
                    }
                }
                
                // If not found directly, try to find any hero with local authority
                EpochLegends.Core.Hero.Hero[] heroes = FindObjectsOfType<EpochLegends.Core.Hero.Hero>();
                foreach (var hero in heroes)
                {
                    // Verifica si el héroe es local
                    if (IsLocalHero(hero))
                    {
                        localHero = hero;
                        
                        // Hero found, initialize HUD with hero data
                        Debug.Log($"GameHUDController: Héroe local encontrado directamente: {localHero.name}");
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
            // Por ahora, asumo que usaremos isLocalPlayer de NetworkBehaviour
            return controller.isLocalPlayer;
        }
        
        // Esta función verifica si un héroe pertenece al jugador local
        // Adaptada para versiones anteriores de Mirror
        private bool IsLocalHero(EpochLegends.Core.Hero.Hero hero)
        {
            // Verificar si el héroe tiene un NetworkIdentity con autoridad local
            NetworkIdentity identity = hero.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                return identity.isLocalPlayer;
            }
            return false;
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
            else if (heroPortrait != null)
            {
                // Usar un sprite por defecto si no hay retrato definido
                heroPortrait.sprite = null; // Reemplazar con un sprite por defecto si lo tienes
            }
            
            // Configurar UI de habilidades usando AbilityUIManager
            if (abilityUIManager != null)
            {
                abilityUIManager.SetupForHero(localHero);
                Debug.Log("GameHUDController: Configuración de habilidades delegada a AbilityUIManager");
            }
            else
            {
                Debug.LogError("GameHUDController: AbilityUIManager es null, no se puede configurar la UI de habilidades");
            }
            
            // Configurar barras de recursos
            UpdateHeroStatus();
            
            // Suscribirse a eventos del héroe
            if (localHero != null)
            {
                // Registrar para eventos de cambio de vida/mana/nivel
                localHero.OnHeroLevelUp += OnHeroLevelUp;
                
                // Aquí podrías añadir más suscripciones a eventos según las necesidades
            }
            
            Debug.Log($"GameHUDController: HUD initialized for hero: {localHero.name}");
        }
        
        // Método para manejar el evento de subida de nivel
        private void OnHeroLevelUp(EpochLegends.Core.Hero.Hero hero)
        {
            Debug.Log($"GameHUDController: Hero leveled up to {hero.Level}");
            
            // Actualizar UI de nivel
            if (levelText != null)
            {
                levelText.text = $"Lvl {hero.Level}";
            }
            
            // Actualizar barras de recursos (pueden haber cambiado por el nivel)
            UpdateHeroStatus();
            
            // Nota: La actualización de habilidades es manejada por AbilityUIManager
            // que ya está suscrito a este mismo evento
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
                // Placeholder scores - En una implementación real obtendrías estos valores del GameManager o TeamManager
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
            if (pauseMenuPanel != null)
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
        
        private void OnDestroy()
        {
            // Limpiar eventos para evitar memory leaks
            if (localHero != null)
            {
                // Eliminar suscripciones para evitar memory leaks
                localHero.OnHeroLevelUp -= OnHeroLevelUp;
            }
            
            // Limpiar botones UI
            if (resumeButton != null) resumeButton.onClick.RemoveAllListeners();
            if (optionsButton != null) optionsButton.onClick.RemoveAllListeners();
            if (disconnectButton != null) disconnectButton.onClick.RemoveAllListeners();
        }
    }
}