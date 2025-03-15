using UnityEngine;
using Mirror;

namespace EpochLegends.Core.Network.Manager
{
    public class EpochNetworkManager : NetworkManager
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

            // Desactivar autoCreatePlayer para controlar manualmente la creación del jugador.
            autoCreatePlayer = false;
        }

        [Server]
        public void PrepareForSceneChange()
        {
            Debug.Log("Preparando para cambio de escena");
            clientLoadedScene = false;
        }

        public void StartHost(string name, string password, int maxConnections)
        {
            serverName = name;
            serverPassword = password;
            maxPlayers = maxConnections;
            
            NetworkServer.RegisterHandler<ServerPasswordMessage>(OnServerPasswordMessage);
            
            clientLoadedScene = false;
            
            StartHost();

            if (NetworkServer.active && isNetworkActive)
            {
                PrepareForSceneChange();
                ServerChangeScene(lobbyScene);
            }
        }

        public void JoinGame(string address, string password)
        {
            networkAddress = address;
            serverPassword = password;
            
            clientLoadedScene = false;
            
            StartClient();
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
            Debug.Log($"OnServerAddPlayer invocado para la conexión: {conn}");
            if (conn.identity != null)
            {
                Debug.Log($"Eliminando jugador existente para la conexión {conn}");
                // Usamos un cast para remover el jugador (1 equivale a destruir el objeto en esta versión)
                NetworkServer.RemovePlayerForConnection(conn, (RemovePlayerOptions)1);
            }
            
            base.OnServerAddPlayer(conn);
            GameManager.Instance?.OnPlayerJoined(conn);
            Debug.Log($"Jugador creado para la conexión {conn}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerLeft(conn);
            }
            
            base.OnServerDisconnect(conn);
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            Debug.Log($"Server changed to scene: {sceneName}");

            if (sceneName == lobbyScene)
            {
                Debug.Log("Lobby scene loaded.");
            }
            else if (sceneName == gameplayScene) 
            {
                Debug.Log("Gameplay scene loaded.");
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            Debug.Log($"Cliente escena cambiada: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"Server started: {serverName}");
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

            // Al conectarse, preparar al cliente y crear el jugador manualmente.
            if (!NetworkClient.ready)
            {
                NetworkClient.Ready();
            }
            // Llamamos a AddPlayer solo si aún no existe un jugador local.
            if (NetworkClient.localPlayer == null)
            {
                NetworkClient.AddPlayer();
            }
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!isNetworkActive)
            {
                ClientChangeScene(mainMenuScene, false);
            }
        }

        // Manejo de mensajes de contraseña
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
        
        // Método auxiliar para cambiar de escena en el cliente
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
