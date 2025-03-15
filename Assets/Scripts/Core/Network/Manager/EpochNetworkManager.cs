using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

namespace EpochLegends.Core.Network.Manager
{
    // Define ReadyStateMessage here so it's available to this class
    public struct ReadyStateMessage : NetworkMessage
    {
        public bool isReady;
    }
    
    public struct GameStateRequestMessage : NetworkMessage {}
    
    public struct GameStateResponseMessage : NetworkMessage 
    {
        public int connectedPlayerCount;
        public GameState currentGameState;
    }

    public class EpochNetworkManager : Mirror.NetworkManager
    {
        public static EpochNetworkManager Instance { get; private set; }

        [Header("Server Configuration")]
        [SerializeField] private string serverName = "Epoch Legends Server";
        [SerializeField] private string serverPassword = "";
        [SerializeField] private int maxPlayers = 10;

        public string ServerName => serverName;
        public bool HasPassword => !string.IsNullOrEmpty(serverPassword);
        public int MaxPlayers => maxPlayers;

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
            
            NetworkServer.RegisterHandler<ServerPasswordMessage>(OnServerPasswordMessage);
            NetworkServer.RegisterHandler<GameStateRequestMessage>(OnGameStateRequestMessage);
            NetworkServer.RegisterHandler<ReadyStateMessage>(OnReadyStateMessage);
            StartHost();
        }

        public void JoinGame(string address, string password)
        {
            networkAddress = address;
            serverPassword = password;
            StartClient();
        }

        public void DisconnectGame()
        {
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
            
            // Notify GameManager about new player
            GameManager.Instance?.OnPlayerJoined(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Notify GameManager about player disconnection
            GameManager.Instance?.OnPlayerLeft(conn);
            
            base.OnServerDisconnect(conn);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"Server started: {serverName}");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log("Client started - registering message handlers");
            
            // Register handlers for client-specific messages
            NetworkClient.RegisterHandler<GameStateResponseMessage>(OnGameStateResponseMessage);
            NetworkClient.RegisterHandler<ReadyStateMessage>(OnReadyStateMessage);
        }
        
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            
            if (!NetworkServer.active) // Pure client only (not host)
            {
                Debug.Log("Client connected - requesting game state");
                NetworkClient.Send(new GameStateRequestMessage());
            }
        }
        
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            
            Debug.Log($"Server scene changed to: {sceneName}");
            
            // Force clients to reload the scene
            if (NetworkServer.active && sceneName == "Lobby")
            {
                // Give objects time to initialize
                Invoke("SyncClientState", 0.5f);
            }
        }
        
        private void SyncClientState()
        {
            // Send game state to all clients
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null && GameManager.Instance != null)
                {
                    GameManager.Instance.SendStateToClient(conn);
                }
            }
        }
        
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            
            Debug.Log($"Client scene changed to: {SceneManager.GetActiveScene().name}");
            
            // Request updated state for UI
            if (NetworkClient.active && !NetworkServer.active)
            {
                Debug.Log("Client scene changed - requesting state update");
                NetworkClient.Send(new GameStateRequestMessage());
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
                conn.Disconnect();
                Debug.Log("Client disconnected: incorrect password");
            }
        }
        
        [Server]
        private void OnGameStateRequestMessage(NetworkConnectionToClient conn, GameStateRequestMessage msg)
        {
            Debug.Log($"Received game state request from client {conn.connectionId}");
            
            // Send current game state to the requesting client
            if (GameManager.Instance != null)
            {
                var responseMsg = new GameStateResponseMessage
                {
                    connectedPlayerCount = GameManager.Instance.ConnectedPlayerCount,
                    currentGameState = GameManager.Instance.CurrentState
                };
                
                conn.Send(responseMsg);
                
                // Also have the GameManager send its specific state data
                GameManager.Instance.SendStateToClient(conn);
            }
        }
        
        [Server]
        private void OnReadyStateMessage(NetworkConnectionToClient conn, ReadyStateMessage msg)
        {
            Debug.Log($"Server received ready state: {msg.isReady} from connection {conn.connectionId}");
            
            // Forward to game manager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayerReady(conn, msg.isReady);
            }
        }
        
        [Client]
        private void OnGameStateResponseMessage(GameStateResponseMessage msg)
        {
            Debug.Log($"Received game state update: Players: {msg.connectedPlayerCount}, State: {msg.currentGameState}");
            
            // Update client-side display/state
            // This is basic state, GameManager will handle more specific updates
        }
        
        [Client]
        private void OnReadyStateMessage(ReadyStateMessage msg)
        {
            Debug.Log($"Client received ready state message: {msg.isReady}");
            // This is mostly for debug logging since the client doesn't need to do anything here
        }
    }
}