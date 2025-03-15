using UnityEngine;
using UnityEngine.UI;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.UI.Manager;

namespace EpochLegends.Core.UI.MainMenu
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Host Game Panel")]
        [SerializeField] private InputField serverNameInput;
        [SerializeField] private InputField serverPasswordInput;
        [SerializeField] private Slider maxPlayersSlider;
        [SerializeField] private Text maxPlayersText;
        [SerializeField] private Button hostGameButton;
        
        [Header("Join Game Panel")]
        [SerializeField] private InputField joinAddressInput;
        [SerializeField] private InputField joinPasswordInput;
        [SerializeField] private Button joinGameButton;
        
        [Header("Other Buttons")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        
        private void Start()
        {
            // Initialize UI
            if (serverNameInput != null)
                serverNameInput.text = "Epoch Legends Server";
                
            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.value = 10;
                maxPlayersSlider.onValueChanged.AddListener(OnMaxPlayersChanged);
                UpdateMaxPlayersText(10);
            }
            
            // Setup button listeners
            if (hostGameButton != null)
                hostGameButton.onClick.AddListener(OnHostGameClicked);
                
            if (joinGameButton != null)
                joinGameButton.onClick.AddListener(OnJoinGameClicked);
                
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
                
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);
        }
        
        private void OnMaxPlayersChanged(float value)
        {
            int players = Mathf.RoundToInt(value);
            UpdateMaxPlayersText(players);
        }
        
        private void UpdateMaxPlayersText(int players)
        {
            if (maxPlayersText != null)
                maxPlayersText.text = players.ToString();
        }
        
        private void OnHostGameClicked()
        {
            string serverName = serverNameInput != null ? serverNameInput.text : "Epoch Legends Server";
            string password = serverPasswordInput != null ? serverPasswordInput.text : "";
            int maxPlayers = maxPlayersSlider != null ? Mathf.RoundToInt(maxPlayersSlider.value) : 10;
            
            Debug.Log($"Starting host: {serverName}, Password: {!string.IsNullOrEmpty(password)}, Max Players: {maxPlayers}");
            
            // Start the host
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.StartHost(serverName, password, maxPlayers);
            }
            else
            {
                Debug.LogError("EpochNetworkManager instance not found!");
            }
        }
        
        private void OnJoinGameClicked()
        {
            string address = joinAddressInput != null ? joinAddressInput.text : "localhost";
            string password = joinPasswordInput != null ? joinPasswordInput.text : "";
            
            Debug.Log($"Joining game at address: {address}");
            
            // Join the game
            if (EpochNetworkManager.Instance != null)
            {
                EpochNetworkManager.Instance.JoinGame(address, password);
            }
            else
            {
                Debug.LogError("EpochNetworkManager instance not found!");
            }
        }
        
        private void OnSettingsClicked()
        {
            // Show settings panel
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPanel(UIPanel.Options);
            }
        }
        
        private void OnQuitClicked()
        {
            // Quit the application
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}