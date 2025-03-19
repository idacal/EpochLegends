using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using EpochLegends.Core.Network; // Add this import to access the message types

namespace EpochLegends.Core.Network.Manager
{
    public class EpochNetworkManager : Mirror.NetworkManager
    {
        public static EpochNetworkManager Instance { get; private set; }

        [Header("Server Configuration")]
        [SerializeField] private string serverName = "Epoch Legends Server";
        [SerializeField] private string serverPassword = "";
        [SerializeField] private int maxPlayers = 10;
        [SerializeField] private bool debugNetwork = true;

        // Añadir campo para almacenar el nombre del jugador
        [HideInInspector] public string playerName = "";

        // Evento para notificar actualizaciones del estado del juego
        // Este es un cambio clave para un enfoque dirigido por eventos
        public delegate void GameStateUpdateEvent();
        public static event GameStateUpdateEvent OnGameStateUpdated;

        public string ServerName => serverName;
        public bool HasPassword => !string.IsNullOrEmpty(serverPassword);
        public int MaxPlayers => maxPlayers;

        // Track last state refresh
        private float lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 1.0f;
        private bool isConnectedToServer = false;

        public override void Awake()
        {
            base.Awake();
            
            // Set these correctly for your project
            offlineScene = "MainMenu"; // Your starting scene
            onlineScene = "Lobby";     // Your lobby scene
            
            // These settings are crucial for proper synchronization:
            clientLoadedScene = true;  // This is important!
            
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // El DontDestroyOnLoad está ocurriendo en ManagersController, así que no lo necesitamos aquí
            // DontDestroyOnLoad(gameObject);
        }

        public void StartHost(string name, string password, int maxConnections)
        {
            serverName = name;
            serverPassword = password;
            maxPlayers = maxConnections;
            
            // Obtener el nombre del jugador de PlayerPrefs
            playerName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Starting host: {name}, Player: {playerName}, Password: {!string.IsNullOrEmpty(password)}, Max Players: {maxConnections}");
            
            // Limpiar cualquier manejador existente para prevenir duplicados
            NetworkServer.UnregisterHandler<ServerPasswordMessage>();
            NetworkServer.UnregisterHandler<GameStateRequestMessage>();
            NetworkServer.UnregisterHandler<EpochLegends.ReadyStateMessage>();
            
            // Register message handlers
            NetworkServer.RegisterHandler<ServerPasswordMessage>(OnServerPasswordMessage);
            NetworkServer.RegisterHandler<GameStateRequestMessage>(OnGameStateRequestMessage);
            NetworkServer.RegisterHandler<EpochLegends.ReadyStateMessage>(OnReadyStateMessage);
            
            StartHost();
        }

        public void JoinGame(string address, string password)
        {
            networkAddress = address;
            serverPassword = password;
            
            // Obtener el nombre del jugador de PlayerPrefs
            playerName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Joining game at: {address}, Player: {playerName}");
                
            StartClient();
        }

        public void DisconnectGame()
        {
            if (debugNetwork)
                Debug.Log("[NetworkManager] Disconnecting from game");
                
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                StopClient();
            }
            
            isConnectedToServer = false;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Player added to server. Connection ID: {conn.connectionId}");
            
            // Encontrar el componente PlayerNetwork para establecer el nombre
            if (conn.identity != null)
            {
                var playerNetwork = conn.identity.GetComponent<PlayerNetwork>();
                if (playerNetwork != null && playerNetwork.playerName == "")
                {
                    // Si el componente existe pero el nombre está vacío, establecerlo desde PlayerPrefs
                    // Esto cubre el caso del host, que ya tiene el nombre establecido
                    playerNetwork.playerName = playerName;
                }
            }
                
            // Notify GameManager about new player
            EpochLegends.GameManager.Instance?.OnPlayerJoined(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Player disconnected from server. Connection ID: {conn.connectionId}");
                
            // Notify GameManager about player disconnection
            EpochLegends.GameManager.Instance?.OnPlayerLeft(conn);
            
            base.OnServerDisconnect(conn);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Verificar prefabs registrados
            Debug.Log($"Server starting with {spawnPrefabs.Count} registered prefabs:");
            foreach (var prefab in spawnPrefabs)
            {
                NetworkIdentity identity = prefab?.GetComponent<NetworkIdentity>();
                if (identity != null)
                {
                    Debug.Log($"- {prefab.name} (assetId: {identity.assetId})");
                }
                else
                {
                    Debug.LogError($"- ERROR: {prefab.name} does not have NetworkIdentity component!");
                }
            }
            
            // Buscar componente de diagnóstico de red
            EpochLegends.Utils.Debug.NetworkDiagnostics diagnostics = 
                FindObjectOfType<EpochLegends.Utils.Debug.NetworkDiagnostics>();
                
            if (diagnostics == null)
            {
                // Crear una instancia del diagnóstico si no existe
                GameObject diagnosticsObj = new GameObject("NetworkDiagnostics");
                diagnosticsObj.AddComponent<EpochLegends.Utils.Debug.NetworkDiagnostics>();
                DontDestroyOnLoad(diagnosticsObj);
                Debug.Log("Created NetworkDiagnostics component for monitoring");
            }
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Server started: {serverName}");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Verificar prefabs registrados en el cliente
            Debug.Log($"[EpochNetworkManager] Cliente iniciado con {spawnPrefabs.Count} prefabs registrados:");
            foreach (var prefab in spawnPrefabs)
            {
                if (prefab != null)
                {
                    NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
                    if (identity != null)
                    {
                        Debug.Log($"- {prefab.name} (assetId: {identity.assetId})");
                    }
                    else
                    {
                        Debug.LogError($"- ERROR: {prefab.name} no tiene componente NetworkIdentity!");
                    }
                }
                else
                {
                    Debug.LogError("- ERROR: Prefab NULO en la lista de spawnPrefabs!");
                }
            }
            
            // Buscar componente de diagnóstico de red
            EpochLegends.Utils.Debug.NetworkDiagnostics diagnostics = 
                FindObjectOfType<EpochLegends.Utils.Debug.NetworkDiagnostics>();
                
            if (diagnostics == null && !NetworkServer.active) // Solo crear en cliente puro
            {
                // Crear una instancia del diagnóstico si no existe
                GameObject diagnosticsObj = new GameObject("NetworkDiagnostics");
                diagnosticsObj.AddComponent<EpochLegends.Utils.Debug.NetworkDiagnostics>();
                DontDestroyOnLoad(diagnosticsObj);
                Debug.Log("Created NetworkDiagnostics component for monitoring");
            }
            
            if (debugNetwork)
                Debug.Log("[NetworkManager] Client started - registering message handlers");
            
            // Limpiar manejadores existentes para prevenir duplicados
            NetworkClient.UnregisterHandler<GameStateResponseMessage>();
            
            // Register client-side message handlers
            NetworkClient.RegisterHandler<GameStateResponseMessage>(OnGameStateResponseMessage);
            
            // Wait a bit before requesting state updates to ensure connection is established
            Invoke(nameof(DelayedRequestFullStateUpdate), 1.5f);
        }
        
        private void DelayedRequestFullStateUpdate()
        {
            // Force refresh of server list for all connected clients
            RequestFullStateUpdate();
        }
        
        // Override keyword to properly extend the base class method
        public override void Update()
        {
            base.Update(); // Call base class Update first
            
            // Periodically refresh for all clients (host y pure clients)
            if (NetworkClient.active && NetworkClient.isConnected && isConnectedToServer)
            {
                if (Time.time - lastRefreshTime >= REFRESH_INTERVAL)
                {
                    RequestFullStateUpdate();
                    lastRefreshTime = Time.time;
                }
            }
        }
        
        private void RequestFullStateUpdate()
        {
            if (NetworkClient.active && NetworkClient.isConnected)
            {
                if (debugNetwork)
                    Debug.Log("[NetworkManager] Requesting full state update");
                    
                try
                {
                    NetworkClient.Send(new GameStateRequestMessage());
                }
                catch(System.Exception ex)
                {
                    Debug.LogWarning($"[NetworkManager] Failed to request state update: {ex.Message}");
                }
            }
        }
        
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            
            isConnectedToServer = true;
            
            // All clients should request game state (host and pure clients)
            if (debugNetwork)
                Debug.Log("[NetworkManager] Client connected - requesting game state");
                
            try
            {
                NetworkClient.Send(new GameStateRequestMessage());
            }
            catch(System.Exception ex)
            {
                Debug.LogWarning($"[NetworkManager] Failed to send initial state request: {ex.Message}");
            }
            
            // Force several refreshes to make sure data gets synced correctly
            Invoke(nameof(RequestFullStateUpdate), 0.5f);
            Invoke(nameof(RequestFullStateUpdate), 1.0f);
            Invoke(nameof(RequestFullStateUpdate), 2.0f);
        }

        public override void OnClientDisconnect()
        {
            if (debugNetwork)
                Debug.Log("[NetworkManager] Client disconnected from server");
                
            isConnectedToServer = false;
            base.OnClientDisconnect();
        }
        
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Server scene changed to: {sceneName}");
            
            // Force clients to reload the scene
            if (NetworkServer.active && sceneName == "Lobby")
            {
                // Give objects time to initialize
                Invoke("SyncClientState", 0.5f);
            }
        }
        
        private void SyncClientState()
        {
            if (debugNetwork)
                Debug.Log("[NetworkManager] Syncing client state for all connections");
                
            // Send game state to all clients
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null && EpochLegends.GameManager.Instance != null)
                {
                    EpochLegends.GameManager.Instance.SendStateToClient(conn);
                }
            }
        }
        
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Client scene changed to: {SceneManager.GetActiveScene().name}");
            
            // Asegurar que el cliente esté listo después de cambio de escena
            if (NetworkClient.active && !NetworkClient.ready)
            {
                Debug.Log("[NetworkManager] Marking client as ready after scene change");
                NetworkClient.Ready();
                
                // Si este cliente es host, no necesita solicitar estado adicional
                if (NetworkServer.active)
                    return;
            }
            
            // Request updated state for UI (for both host and pure clients)
            if (NetworkClient.active && NetworkClient.isConnected)
            {
                if (debugNetwork)
                    Debug.Log("[NetworkManager] Client scene changed - requesting state update");
                    
                try
                {
                    NetworkClient.Send(new GameStateRequestMessage());
                }
                catch(System.Exception ex)
                {
                    Debug.LogWarning($"[NetworkManager] Failed to request state after scene change: {ex.Message}");
                }
                
                // Force refresh after a short delay
                Invoke(nameof(RequestFullStateUpdate), 0.5f);
                Invoke(nameof(RequestFullStateUpdate), 1.0f);
            }
        }

        // Password handling
        private struct ServerPasswordMessage : NetworkMessage
        {
            public string password;
        }

        private void OnServerPasswordMessage(NetworkConnectionToClient conn, ServerPasswordMessage msg)
        {
            if (msg.password != serverPassword)
            {
                if (debugNetwork)
                    Debug.Log("[NetworkManager] Client disconnected: incorrect password");
                    
                conn.Disconnect();
            }
        }
        
        [Server]
        private void OnGameStateRequestMessage(NetworkConnectionToClient conn, GameStateRequestMessage msg)
        {
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Received game state request from client {conn.connectionId}");
            
            // Send current game state to the requesting client
            if (EpochLegends.GameManager.Instance != null)
            {
                var responseMsg = new GameStateResponseMessage
                {
                    connectedPlayerCount = EpochLegends.GameManager.Instance.ConnectedPlayerCount,
                    currentGameState = EpochLegends.GameManager.Instance.CurrentState
                };
                
                conn.Send(responseMsg);
                
                // Also have the GameManager send its specific state data
                EpochLegends.GameManager.Instance.SendStateToClient(conn);
            }
        }
        
        [Client]
        private void OnGameStateResponseMessage(GameStateResponseMessage msg)
        {
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Received game state response: {msg.currentGameState}, Players: {msg.connectedPlayerCount}");
            
            // Trigger the game state updated event - esta es una mejora clave
            OnGameStateUpdated?.Invoke();
        }
        
        [Server]
        private void OnReadyStateMessage(NetworkConnectionToClient conn, EpochLegends.ReadyStateMessage msg)
        {
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Server received ready state: {msg.isReady} from connection {conn.connectionId}");
            
            // Forward to game manager
            if (EpochLegends.GameManager.Instance != null)
            {
                EpochLegends.GameManager.Instance.SetPlayerReady(conn, msg.isReady);
            }
        }
        
        /// <summary>
        /// Checks if the client is connected to a server and is safe to send messages
        /// </summary>
        public bool IsClientConnected()
        {
            return NetworkClient.active && NetworkClient.isConnected && isConnectedToServer;
        }
    }
}