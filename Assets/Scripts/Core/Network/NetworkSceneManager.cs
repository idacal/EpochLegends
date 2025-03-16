using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

namespace EpochLegends.Core.Network
{
    /// <summary>
    /// Gestiona la inicialización correcta del NetworkManager y la sincronización de escenas.
    /// </summary>
    public class NetworkSceneManager : MonoBehaviour
    {
        // Singleton para fácil acceso
        public static NetworkSceneManager Instance { get; private set; }
        
        [Header("Prefabs")]
        [SerializeField] private GameObject playerPrefab; // Asigna aquí el prefab de jugador
        
        [Header("Debug")]
        [SerializeField] private bool debugEnabled = true;
        
        // Referencia al NetworkManager principal
        private EpochLegends.Core.Network.Manager.EpochNetworkManager networkManager;
        private EpochLegends.GameManager gameManager;

        private void Awake()
        {
            // Implementación de singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (debugEnabled)
                Debug.Log("[NetworkSceneManager] Initialized");
        }
        
        private void Start()
        {
            // Buscar referencias a los managers existentes
            FindManagers();
            
            // Registrarse para eventos de carga de escena
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void OnDestroy()
        {
            // Desregistrarse de eventos
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        private void FindManagers()
        {
            networkManager = FindObjectOfType<EpochLegends.Core.Network.Manager.EpochNetworkManager>();
            gameManager = FindObjectOfType<EpochLegends.GameManager>();
            
            if (networkManager == null)
            {
                if (debugEnabled)
                    Debug.LogError("[NetworkSceneManager] EpochNetworkManager not found! Network features will not work.");
            }
            else
            {
                if (debugEnabled)
                    Debug.Log("[NetworkSceneManager] Found EpochNetworkManager instance.");
                
                // Asegurarnos de que persista entre escenas
                DontDestroyOnLoad(networkManager.gameObject);
            }
            
            if (gameManager == null)
            {
                if (debugEnabled)
                    Debug.LogError("[NetworkSceneManager] GameManager not found! Game state will not be maintained.");
            }
            else
            {
                if (debugEnabled)
                    Debug.Log("[NetworkSceneManager] Found GameManager instance.");
                
                // Asegurarnos de que persista entre escenas
                DontDestroyOnLoad(gameManager.gameObject);
            }
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (debugEnabled)
                Debug.Log($"[NetworkSceneManager] Scene loaded: {scene.name}");
            
            // Siempre asegurarse de que tenemos referencias a los managers
            if (networkManager == null || gameManager == null)
            {
                if (debugEnabled)
                    Debug.Log("[NetworkSceneManager] Managers not found, searching again...");
                FindManagers();
            }
            
            // Verificar la escena para ejecutar acciones específicas
            switch (scene.name)
            {
                case "MainMenu":
                    // Acciones específicas para MainMenu
                    break;
                    
                case "Lobby":
                    // Verificar que los componentes críticos del Lobby existan
                    VerifyLobbyRequirements();
                    break;
                    
                case "HeroSelection":
                    // Acciones específicas para selección de héroes
                    break;
                    
                case "Gameplay":
                    // Acciones específicas para gameplay
                    break;
            }
            
            // Si estamos en modo cliente, verificar que los objetos de red estén registrados
            if (NetworkClient.active && !NetworkServer.active)
            {
                if (debugEnabled)
                    Debug.Log("[NetworkSceneManager] Client active, verifying network objects...");
                    
                // Solicitar actualización de estado de juego
                RequestGameStateUpdate();
            }
        }
        
        private void VerifyLobbyRequirements()
        {
            // Verificar que exista el TeamManager en la escena Lobby
            var teamManager = FindObjectOfType<EpochLegends.Systems.Team.Manager.TeamManager>();
            if (teamManager == null)
            {
                if (debugEnabled)
                    Debug.LogWarning("[NetworkSceneManager] TeamManager not found in Lobby scene!");
            }
            
            // Verificar que exista el TeamAssignment en la escena Lobby
            var teamAssignment = FindObjectOfType<EpochLegends.Systems.Team.Assignment.TeamAssignment>();
            if (teamAssignment == null)
            {
                if (debugEnabled)
                    Debug.LogWarning("[NetworkSceneManager] TeamAssignment not found in Lobby scene!");
            }
            
            // Verificar que exista el LobbyController o LobbyUI
            var lobbyController = FindObjectOfType<EpochLegends.UI.Lobby.LobbyController>() || 
                                  FindObjectOfType<EpochLegends.Core.UI.Lobby.LobbyUI>();
            if (!lobbyController)
            {
                if (debugEnabled)
                    Debug.LogWarning("[NetworkSceneManager] No Lobby UI controller found in Lobby scene!");
            }
        }
        
        private void RequestGameStateUpdate()
        {
            if (NetworkClient.active && NetworkClient.isConnected)
            {
                if (debugEnabled)
                    Debug.Log("[NetworkSceneManager] Requesting game state from server...");
                    
                NetworkClient.Send(new GameStateRequestMessage());
                
                // También solicitar actualización del sincronizador de Lobby si existe
                var lobbySync = FindObjectOfType<LobbyDataSynchronizer>();
                if (lobbySync != null)
                {
                    if (debugEnabled)
                        Debug.Log("[NetworkSceneManager] Requesting lobby sync data...");
                        
                    lobbySync.ForceRefresh();
                }
            }
            else
            {
                if (debugEnabled)
                    Debug.LogWarning("[NetworkSceneManager] Cannot request state - not connected to server.");
            }
        }
        
        /// <summary>
        /// Registra los prefabs necesarios para la sincronización de red.
        /// Llama a este método manualmente si tienes problemas de sincronización.
        /// </summary>
        public void RegisterNetworkPrefabs()
        {
            if (networkManager == null)
            {
                if (debugEnabled)
                    Debug.LogError("[NetworkSceneManager] Cannot register prefabs - NetworkManager not found!");
                return;
            }
            
            if (playerPrefab != null)
            {
                // Asegurarse de que el prefab del jugador esté registrado
                NetworkManager singleton = NetworkManager.singleton;
                if (singleton != null)
                {
                    singleton.playerPrefab = playerPrefab;
                    
                    if (debugEnabled)
                        Debug.Log("[NetworkSceneManager] Player prefab registered with NetworkManager.");
                }
            }
            
            if (debugEnabled)
                Debug.Log("[NetworkSceneManager] Network prefabs registration completed.");
        }
        
        /// <summary>
        /// Comprueba y corrige la configuración de red.
        /// </summary>
        [ContextMenu("Fix Network Configuration")]
        public void FixNetworkConfiguration()
        {
            // Buscar todos los NetworkManager en la escena
            var networkManagers = FindObjectsOfType<NetworkManager>();
            
            if (networkManagers.Length > 1)
            {
                if (debugEnabled)
                    Debug.LogWarning($"[NetworkSceneManager] Found {networkManagers.Length} NetworkManagers in the scene!");
                
                // Encontrar nuestro EpochNetworkManager
                EpochLegends.Core.Network.Manager.EpochNetworkManager ourManager = null;
                foreach (var manager in networkManagers)
                {
                    if (manager is EpochLegends.Core.Network.Manager.EpochNetworkManager epochManager)
                    {
                        ourManager = epochManager;
                        break;
                    }
                }
                
                // Eliminar los NetworkManagers que no sean nuestro EpochNetworkManager
                foreach (var manager in networkManagers)
                {
                    if (manager != ourManager && manager != null)
                    {
                        if (debugEnabled)
                            Debug.Log($"[NetworkSceneManager] Destroying duplicate NetworkManager: {manager.name}");
                            
                        Destroy(manager.gameObject);
                    }
                }
            }
            
            // Registrar prefabs
            RegisterNetworkPrefabs();
            
            if (debugEnabled)
                Debug.Log("[NetworkSceneManager] Network configuration fixed.");
        }
    }
}