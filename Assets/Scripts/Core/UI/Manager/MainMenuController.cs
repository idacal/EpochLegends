using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using EpochLegends.Core.Network.Manager;

namespace EpochLegends.Core.UI.Manager
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject joinGamePanel;
        [SerializeField] private GameObject settingsPanel;
        
        [Header("Main Menu Buttons")]
        [SerializeField] private Button hostGameButton;
        [SerializeField] private Button joinGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;
        
        [Header("Join Game Panel")]
        [SerializeField] private InputField ipAddressInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button backFromJoinButton;
        
        [Header("Settings Panel")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Button backFromSettingsButton;
        
        [Header("References")]
        [SerializeField] private EpochNetworkManager networkManager;
        
        // For storing settings
        private float masterVolume = 1.0f;
        private bool isFullscreen = true;
        private int qualityLevel = 3;
        
        private void Awake()
        {
            // Find NetworkManager if not assigned
            if (networkManager == null)
            {
                networkManager = FindObjectOfType<EpochNetworkManager>();
                
                if (networkManager == null)
                {
                    Debug.LogError("EpochNetworkManager not found! Make sure it exists in the scene.");
                }
            }
            
            // Initialize settings with saved values (if available)
            LoadSettings();
        }
        
        private void Start()
        {
            // Setup button listeners
            SetupButtonListeners();
            
            // Initialize UI elements with current settings
            InitializeSettingsUI();
            
            // Start with main menu panel only
            ShowMainMenu();
        }
        
        private void SetupButtonListeners()
        {
            // Main menu buttons
            if (hostGameButton != null) hostGameButton.onClick.AddListener(OnHostGameClicked);
            if (joinGameButton != null) joinGameButton.onClick.AddListener(OnJoinGameClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
            
            // Join game panel buttons
            if (connectButton != null) connectButton.onClick.AddListener(OnConnectClicked);
            if (backFromJoinButton != null) backFromJoinButton.onClick.AddListener(ShowMainMenu);
            
            // Settings panel buttons
            if (backFromSettingsButton != null) backFromSettingsButton.onClick.AddListener(ShowMainMenu);
            
            // Settings controls
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }
        
        private void InitializeSettingsUI()
        {
            if (volumeSlider != null) volumeSlider.value = masterVolume;
            if (fullscreenToggle != null) fullscreenToggle.isOn = isFullscreen;
            
            if (qualityDropdown != null)
            {
                // Clear existing options
                qualityDropdown.ClearOptions();
                
                // Add quality names from Quality Settings
                string[] qualityNames = QualitySettings.names;
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(qualityNames));
                
                // Set current value
                qualityDropdown.value = qualityLevel;
            }
        }
        
        #region Panel Management
        
        private void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (joinGamePanel != null) joinGamePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }
        
        private void ShowJoinGamePanel()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (joinGamePanel != null) joinGamePanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            
            // Focus on IP input field
            if (ipAddressInput != null) ipAddressInput.Select();
        }
        
        private void ShowSettingsPanel()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (joinGamePanel != null) joinGamePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }
        
        #endregion
        
        #region Button Event Handlers
        
        private void OnHostGameClicked()
        {
            Debug.Log("Starting as Host");
            
            if (networkManager != null)
            {
                // Use default host parameters for now
                // In a full implementation, you might want a configuration panel
                networkManager.StartHost("Epoch Legends Server", "", 8);
                
                // Load the lobby scene
                // In a real implementation, NetworkManager would handle scene changes
                SceneManager.LoadScene("Lobby");
            }
            else
            {
                Debug.LogError("EpochNetworkManager not found when trying to host");
            }
        }
        
        private void OnJoinGameClicked()
        {
            ShowJoinGamePanel();
        }
        
        private void OnSettingsClicked()
        {
            ShowSettingsPanel();
        }
        
        private void OnExitClicked()
        {
            Debug.Log("Exit requested");
            
            // Save settings before exit
            SaveSettings();
            
            // Quit application
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        private void OnConnectClicked()
        {
            if (ipAddressInput != null && !string.IsNullOrEmpty(ipAddressInput.text))
            {
                string ipAddress = ipAddressInput.text;
                Debug.Log($"Connecting to server at {ipAddress}");
                
                if (networkManager != null)
                {
                    // Connect to the specified server
                    networkManager.JoinGame(ipAddress, "");
                    
                    // NetworkManager will handle scene change upon successful connection
                }
                else
                {
                    Debug.LogError("EpochNetworkManager not found when trying to connect");
                }
            }
            else
            {
                Debug.LogWarning("No IP address entered");
                // You might want to show an error message to the user
            }
        }
        
        #endregion
        
        #region Settings Handlers
        
        private void OnVolumeChanged(float volume)
        {
            masterVolume = volume;
            
            // In a full implementation, this would update AudioManager or similar
            AudioListener.volume = volume;
            
            Debug.Log($"Volume changed to {volume}");
        }
        
        private void OnFullscreenToggled(bool isOn)
        {
            isFullscreen = isOn;
            Screen.fullScreen = isOn;
            
            Debug.Log($"Fullscreen set to {isOn}");
        }
        
        private void OnQualityChanged(int qualityIndex)
        {
            qualityLevel = qualityIndex;
            QualitySettings.SetQualityLevel(qualityIndex);
            
            Debug.Log($"Quality set to {QualitySettings.names[qualityIndex]}");
        }
        
        #endregion
        
        #region Settings Persistence
        
        private void LoadSettings()
        {
            // Load volume
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            AudioListener.volume = masterVolume;
            
            // Load fullscreen setting
            isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            Screen.fullScreen = isFullscreen;
            
            // Load quality setting
            qualityLevel = PlayerPrefs.GetInt("QualityLevel", 3);
            QualitySettings.SetQualityLevel(qualityLevel);
        }
        
        private void SaveSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt("QualityLevel", qualityLevel);
            PlayerPrefs.Save();
        }
        
        #endregion
    }
}