using UnityEngine;
using UnityEngine.UI;
using EpochLegends.Core.Network.Manager;
using EpochLegends.Core.UI.Manager;

namespace EpochLegends.Core.UI.MainMenu
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField] private InputField playerNameInput;

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
        
        [Header("Debug")]
        [SerializeField] private bool debugUI = false;
        
        private void Start()
        {
            // Cargar nombre guardado si existe
            string savedPlayerName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            
            // Initialize UI
            if (playerNameInput != null)
                playerNameInput.text = savedPlayerName;
            
            if (serverNameInput != null)
                serverNameInput.text = "Epoch Legends Server";
                
            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.value = 10;
                maxPlayersSlider.onValueChanged.AddListener(OnMaxPlayersChanged);
                UpdateMaxPlayersText(10);
            }
            
            if (debugUI)
                Debug.Log($"[MainMenuUI] Initialized with player name: {savedPlayerName}");
            
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
            string playerName = playerNameInput != null ? playerNameInput.text : "Player" + Random.Range(1000, 9999);
            string serverName = serverNameInput != null ? serverNameInput.text : "Epoch Legends Server";
            string password = serverPasswordInput != null ? serverPasswordInput.text : "";
            int maxPlayers = maxPlayersSlider != null ? Mathf.RoundToInt(maxPlayersSlider.value) : 10;
            
            // Validar nombre del jugador
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player" + Random.Range(1000, 9999);
                
            // Limitar longitud
            if (playerName.Length > 20)
                playerName = playerName.Substring(0, 20);
            
            // Guardar nombre para futuras sesiones
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
            
            Debug.Log($"Starting host: {serverName}, Player: {playerName}, Password: {!string.IsNullOrEmpty(password)}, Max Players: {maxPlayers}");
            
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
            string playerName = playerNameInput != null ? playerNameInput.text : "Player" + Random.Range(1000, 9999);
            string address = joinAddressInput != null ? joinAddressInput.text : "localhost";
            string password = joinPasswordInput != null ? joinPasswordInput.text : "";
            
            // Validar nombre del jugador
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player" + Random.Range(1000, 9999);
                
            // Limitar longitud
            if (playerName.Length > 20)
                playerName = playerName.Substring(0, 20);
            
            // Guardar nombre para futuras sesiones
            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.Save();
            
            Debug.Log($"Joining game at address: {address}, Player: {playerName}");
            
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