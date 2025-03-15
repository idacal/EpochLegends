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

        // No necesitamos redefinir playerPrefab ya que ya existe en NetworkManager base
        // Si necesitas acceder a él, usa la propiedad 'playerPrefab' de NetworkManager directamente

        public string ServerName => serverName;
        public bool HasPassword => !string.IsNullOrEmpty(serverPassword);
        public int MaxPlayers => maxPlayers;

        public override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        public void StartHost(string name, string password, int maxConnections)
        {
            serverName = name;
            serverPassword = password;
            maxPlayers = maxConnections;
            
            NetworkServer.RegisterHandler<ServerPasswordMessage>(OnServerPasswordMessage);
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
            EpochLegends.GameManager.Instance?.OnPlayerJoined(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Notify GameManager about player disconnection
            EpochLegends.GameManager.Instance?.OnPlayerLeft(conn);
            
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
            Debug.Log("Client started");
        }

        // Password handling (simplified for this example)
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
    }
}