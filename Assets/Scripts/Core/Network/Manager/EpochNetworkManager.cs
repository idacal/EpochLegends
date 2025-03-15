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

        public string ServerName => serverName;
        public bool HasPassword => !string.IsNullOrEmpty(serverPassword);
        public int MaxPlayers => maxPlayers;

        // Track last state refresh
        private float lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 1.0f;

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
            DontDestroyOnLoad(gameObject);
        }

        public void StartHost(string name, string password, int maxConnections)
        {
            serverName = name;
            serverPassword = password;
            maxPlayers = maxConnections;
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Starting host: {name}, Password: {!string.IsNullOrEmpty(password)}, Max Players: {maxConnections}");
            
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
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Joining game at: {address}");
                
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
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Player added to server. Connection ID: {conn.connectionId}");
                
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
            
            if (debugNetwork)
                Debug.Log($"[NetworkManager] Server started: {serverName}");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (debugNetwork)
                Debug.Log("[NetworkManager] Client started - registering message handlers");
            
            // Register client-side message handlers
            NetworkClient.RegisterHandler<GameStateResponseMessage>(OnGameStateResponseMessage);
            
            // Force refresh of server list for all connected clients
            Invoke(nameof(RequestFullStateUpdate), 1.0f);
        }
        
        // Override keyword to properly extend the base class method
        public override void Update()
        {
            base.Update(); // Call base class Update first
            
            // Periodically refresh for clients
            if (NetworkClient.active && !NetworkServer.active)
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
            if (NetworkClient.active && !NetworkServer.active)
            {
                if (debugNetwork)
                    Debug.Log("[NetworkManager] Requesting full state update");
                    
                NetworkClient.Send(new GameStateRequestMessage());
            }
        }
        
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            
            if (!NetworkServer.active) // Pure client only (not host)
            {
                if (debugNetwork)
                    Debug.Log("[NetworkManager] Client connected - requesting game state");
                    
                NetworkClient.Send(new GameStateRequestMessage());
                
                // Force refresh of server list for the newly connected client
                Invoke(nameof(RequestFullStateUpdate), 1.0f);
            }
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
            
            // Request updated state for UI
            if (NetworkClient.active && !NetworkServer.active)
            {
                if (debugNetwork)
                    Debug.Log("[NetworkManager] Client scene changed - requesting state update");
                    
                NetworkClient.Send(new GameStateRequestMessage());
                
                // Force refresh after a short delay
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
            
            // Process the game state update
            // Update any UI elements that need this information
            RefreshClientUI();
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
        
        [Client]
        private void RefreshClientUI()
        {
            // Find any LobbyController or LobbyUI instances and refresh them
            EpochLegends.UI.Lobby.LobbyController lobbyController = FindObjectOfType<EpochLegends.UI.Lobby.LobbyController>();
            if (lobbyController != null)
            {
                // If LobbyController has a public RefreshUI method, call it
                if (typeof(EpochLegends.UI.Lobby.LobbyController).GetMethod("RefreshUI") != null)
                {
                    lobbyController.SendMessage("RefreshUI");
                }
            }
            
            EpochLegends.Core.UI.Lobby.LobbyUI lobbyUI = FindObjectOfType<EpochLegends.Core.UI.Lobby.LobbyUI>();
            if (lobbyUI != null)
            {
                lobbyUI.RefreshUI();
            }
        }
    }
}