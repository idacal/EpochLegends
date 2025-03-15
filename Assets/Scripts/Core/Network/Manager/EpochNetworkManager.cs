using UnityEngine;
using Mirror;

namespace EpochLegends.Core.Network.Manager
{
    public class EpochNetworkManager : Mirror.NetworkManager
    {
        public static EpochNetworkManager Instance { get; private set; }

        [Header("Server Configuration")]
        [SerializeField] private string serverName = "Epoch Legends Server";
        [SerializeField] private string serverPassword = "";
        [SerializeField] private int maxPlayers = 10;

        [Header("Scene References")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string lobbyScene = "Lobby";
        [SerializeField] private string heroSelectionScene = "HeroSelection";
        [SerializeField] private string gameplayScene = "Gameplay";

        public string ServerName => serverName;
        public bool HasPassword => !string.IsNullOrEmpty(serverPassword);
        public int MaxPlayers => maxPlayers;

        public override void Awake()
        {
            base.Awake();
            
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [Server]
        public void PrepareForSceneChange()
        {
            // Método alternativo para preparar el cambio de escena
            // En lugar de intentar desasignar identidades, usamos las 
            // funcionalidades de Mirror para manejar jugadores
            Debug.Log("Preparando para cambio de escena");
            
            // Establecer clientLoadedScene a false para que Mirror maneje correctamente
            // la creación de jugadores después del cambio de escena
            clientLoadedScene = false;
            
            // Opcionalmente, podemos hacer limpieza adicional aquí si es necesario
        }

        public void StartHost(string name, string password, int maxConnections)
        {
            serverName = name;
            serverPassword = password;
            maxPlayers = maxConnections;
            
            NetworkServer.RegisterHandler<ServerPasswordMessage>(OnServerPasswordMessage);
            
            // Configuración crucial para evitar problemas con jugadores duplicados
            clientLoadedScene = false;
            
            // Iniciar el host
            StartHost();

            // After successfully starting the host, load the lobby scene
            if (NetworkServer.active && isNetworkActive)
            {
                // Preparar para cambio de escena
                PrepareForSceneChange();
                
                // Cambiar a la escena del lobby
                ServerChangeScene(lobbyScene);
            }
        }

        public void JoinGame(string address, string password)
        {
            networkAddress = address;
            serverPassword = password;
            
            // Configuración crucial para evitar problemas con jugadores duplicados
            clientLoadedScene = false;
            
            StartClient();
            // Scene change will happen when the server tells all clients
        }

        public void DisconnectGame()
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                StopHost();
                ServerChangeScene(mainMenuScene);
            }
            else if (NetworkClient.isConnected)
            {
                StopClient();
                ClientChangeScene(mainMenuScene, false);
            }
        }

        // Sobrescribir la adición de jugadores para tener más control
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Verificar si ya existe un jugador para esta conexión
            if (conn.identity != null)
            {
                Debug.Log($"Ya existe un jugador para esta conexión: {conn.identity.netId}");
                
                // Notificar al GameManager sobre el jugador existente
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnPlayerJoined(conn);
                }
                
                return;
            }
            
            // Si no existe, añadir el jugador normalmente
            Debug.Log($"Añadiendo nuevo jugador para la conexión: {conn.connectionId}");
            base.OnServerAddPlayer(conn);
            
            // Notificar al GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerJoined(conn);
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Notificar al GameManager sobre la desconexión del jugador
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerLeft(conn);
            }
            
            base.OnServerDisconnect(conn);
        }

        // Manejo específico de escenas en el servidor
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            
            Debug.Log($"Server escena cambiada a: {sceneName}");
            
            // Lógica específica según la escena
            if (sceneName == lobbyScene)
            {
                Debug.Log("Servidor en escena de Lobby");
            }
            else if (sceneName == heroSelectionScene)
            {
                Debug.Log("Servidor en escena de Selección de Héroe");
            }
            else if (sceneName == gameplayScene)
            {
                Debug.Log("Servidor en escena de Juego");
            }
        }

        // Manejo específico de escenas en el cliente
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            
            Debug.Log($"Cliente escena cambiada: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"Server started: {serverName}");
            
            // Make sure GameManager is initialized
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnServerStarted();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log("Client started");
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("Client connected to server");
            
            // If we're in the main menu scene and we connect, wait for the server to change our scene
            // The server will handle scene transitions
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            // Return to main menu when client disconnects
            if (!isNetworkActive)
            {
                ClientChangeScene(mainMenuScene, false);
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
        
        // Helper method for client-only scene changes (not over network)
        public void ClientChangeScene(string sceneName, bool additive)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != sceneName)
            {
                if (additive)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
                }
            }
        }
    }
}