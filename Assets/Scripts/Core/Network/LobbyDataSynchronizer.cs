using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpochLegends.Core.Network
{
    /// <summary>
    /// Componente dedicado a sincronizar datos de lobby entre servidor y clientes
    /// que utiliza un enfoque más directo y explícito
    /// </summary>
    public class LobbyDataSynchronizer : NetworkBehaviour
    {
        public static LobbyDataSynchronizer Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugSync = true;
        
        // Estructura para enviar datos de jugador
        [System.Serializable]
        public struct SyncPlayerData
        {
            public uint netId;
            public string playerName;
            public int teamId;
            public bool isReady;
        }
        
        // Estructura del mensaje completo del lobby
        public struct LobbySyncMessage : NetworkMessage
        {
            public string serverName;
            public int playerCount;
            public int maxPlayers;
            public List<SyncPlayerData> players;
        }
        
        // Estructura para solicitar actualización
        public struct LobbyDataRequestMessage : NetworkMessage
        {
            public bool fullRefresh;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Initialized");
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Registrar handler para solicitudes de datos
            NetworkServer.RegisterHandler<LobbyDataRequestMessage>(OnLobbyDataRequest);
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Server started - registered handlers");
                
            // Programa sincronizaciones periódicas, pero con un retraso inicial
            Invoke(nameof(StartPeriodicalSync), 1.0f);
        }
        
        [Server]
        private void StartPeriodicalSync()
        {
            if (!isServer) return;
            
            // Inicia las sincronizaciones periódicas
            InvokeRepeating(nameof(BroadcastLobbyData), 0f, 1.0f);
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Started periodical sync");
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Registrar handler para mensajes de sincronización
            NetworkClient.RegisterHandler<LobbySyncMessage>(OnLobbySyncMessage);
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Client started - registered handlers");
                
            // Solicitar datos con retraso para asegurar que la conexión está establecida
            if (isClientOnly) // Solo para clientes puros, no el host
            {
                Invoke(nameof(RequestLobbyDataDelayed), 1.0f);
                Invoke(nameof(RequestLobbyDataDelayed), 2.0f);
                Invoke(nameof(RequestLobbyDataDelayed), 4.0f);
            }
        }
        
        [Client]
        private void RequestLobbyDataDelayed()
        {
            if (NetworkClient.isConnected) // Asegurarnos de que estamos conectados
            {
                RequestLobbyData();
            }
            else if (debugSync)
            {
                Debug.Log("[LobbyDataSynchronizer] Delayed request skipped - not connected yet");
            }
        }
        
        [Client]
        public void RequestLobbyData()
        {
            if (!NetworkClient.isConnected) 
            {
                if (debugSync)
                    Debug.LogWarning("[LobbyDataSynchronizer] Cannot request lobby data - not connected to server");
                return;
            }
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Requesting lobby data from server");
            
            try
            {
                NetworkClient.Send(new LobbyDataRequestMessage { fullRefresh = true });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LobbyDataSynchronizer] Failed to request lobby data: {ex.Message}");
            }
        }
        
        [Server]
        private void OnLobbyDataRequest(NetworkConnectionToClient conn, LobbyDataRequestMessage msg)
        {
            if (debugSync)
                Debug.Log($"[LobbyDataSynchronizer] Received lobby data request from client {conn.connectionId}");
                
            // Enviar datos a este cliente específico
            SendLobbyDataToClient(conn);
        }
        
        [Server]
        private void BroadcastLobbyData()
        {
            if (!isServer) return;
            
            // Comprobar si hay clientes conectados antes de enviar mensajes
            if (NetworkServer.connections.Count == 0)
            {
                if (debugSync)
                    Debug.Log("[LobbyDataSynchronizer] No clients connected, skipping broadcast");
                return;
            }
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Broadcasting lobby data to all clients");
                
            var message = CreateLobbyDataMessage();
            NetworkServer.SendToAll(message);
        }
        
        [Server]
        private void SendLobbyDataToClient(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active || conn == null) return;
            
            var message = CreateLobbyDataMessage();
            conn.Send(message);
            
            if (debugSync)
                Debug.Log($"[LobbyDataSynchronizer] Sent lobby data to client {conn.connectionId}");
        }
        
        [Server]
        private LobbySyncMessage CreateLobbyDataMessage()
        {
            string serverName = "Epoch Legends Server";
            int maxPlayers = 10;
            int playerCount = 0;
            List<SyncPlayerData> playerDataList = new List<SyncPlayerData>();
            
            // Obtener nombre de servidor y max jugadores de NetworkManager
            if (Mirror.NetworkManager.singleton is EpochLegends.Core.Network.Manager.EpochNetworkManager networkManager)
            {
                serverName = networkManager.ServerName;
                maxPlayers = networkManager.MaxPlayers;
            }
            
            // Obtener datos de jugadores de GameManager
            if (EpochLegends.GameManager.Instance != null)
            {
                var connectedPlayers = EpochLegends.GameManager.Instance.ConnectedPlayers;
                playerCount = connectedPlayers.Count;
                
                foreach (var player in connectedPlayers)
                {
                    // Obtener el nombre del jugador - primero del PlayerInfo
                    string playerName = player.Value.PlayerName;
                    
                    // Si está vacío, buscar alternativas
                    if (string.IsNullOrEmpty(playerName))
                    {
                        // Intentar obtener un mejor nombre del NetworkIdentity
                        if (NetworkServer.spawned.TryGetValue(player.Key, out NetworkIdentity identity))
                        {
                            // Intentar obtener de PlayerNetwork
                            var playerNetwork = identity.GetComponent<PlayerNetwork>();
                            if (playerNetwork != null && !string.IsNullOrEmpty(playerNetwork.playerName))
                            {
                                playerName = playerNetwork.playerName;
                                
                                // IMPORTANTE: También actualizar el nombre en GameManager
                                // para futura referencia
                                if (EpochLegends.GameManager.Instance != null)
                                {
                                    EpochLegends.GameManager.Instance.UpdatePlayerName(player.Key, playerName);
                                }
                            }
                            else
                            {
                                // Usar nombre del objeto como último recurso
                                playerName = identity.gameObject.name;
                            }
                        }
                        else
                        {
                            // Si no se puede encontrar un nombre, usar "Player + netId" como respaldo
                            playerName = "Player " + player.Key;
                        }
                    }
                    
                    if (debugSync)
                        Debug.Log($"[LobbySync] Player {player.Key} name: {playerName}, team: {player.Value.TeamId}, ready: {player.Value.IsReady}");
                    
                    playerDataList.Add(new SyncPlayerData
                    {
                        netId = player.Key,
                        playerName = playerName,
                        teamId = player.Value.TeamId,
                        isReady = player.Value.IsReady
                    });
                }
            }
            
            if (debugSync)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[LobbyDataSynchronizer] Creating lobby data message:");
                sb.AppendLine($"  Server: {serverName}");
                sb.AppendLine($"  Players: {playerCount}/{maxPlayers}");
                sb.AppendLine($"  Player list ({playerDataList.Count}):");
                foreach (var p in playerDataList)
                {
                    sb.AppendLine($"    - {p.playerName} (ID: {p.netId}, Team: {p.teamId}, Ready: {p.isReady})");
                }
                Debug.Log(sb.ToString());
            }
            
            return new LobbySyncMessage
            {
                serverName = serverName,
                playerCount = playerCount,
                maxPlayers = maxPlayers,
                players = playerDataList
            };
        }
        
        [Client]
        private void OnLobbySyncMessage(LobbySyncMessage msg)
        {
            if (debugSync)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[LobbyDataSynchronizer] Received lobby sync message:");
                sb.AppendLine($"  Server: {msg.serverName}");
                sb.AppendLine($"  Players: {msg.playerCount}/{msg.maxPlayers}");
                sb.AppendLine($"  Player list ({msg.players.Count}):");
                foreach (var p in msg.players)
                {
                    sb.AppendLine($"    - {p.playerName} (ID: {p.netId}, Team: {p.teamId}, Ready: {p.isReady})");
                }
                Debug.Log(sb.ToString());
            }
            
            // Actualizar las interfaces de usuario con estos datos
            UpdateAllLobbyUIs(msg);
        }
        
        [Client]
        private void UpdateAllLobbyUIs(LobbySyncMessage msg)
        {
            // Actualizar LobbyUI si existe
            var lobbyUI = FindObjectOfType<EpochLegends.Core.UI.Lobby.LobbyUI>();
            if (lobbyUI != null)
            {
                lobbyUI.UpdateFromSyncData(msg.serverName, msg.playerCount, msg.maxPlayers, msg.players);
            }
            
            // Actualizar LobbyController si existe
            var lobbyController = FindObjectOfType<EpochLegends.UI.Lobby.LobbyController>();
            if (lobbyController != null)
            {
                lobbyController.UpdateFromSyncData(msg.serverName, msg.playerCount, msg.maxPlayers, msg.players);
            }
        }
        
        // Método público para forzar una sincronización desde el servidor
        [Server]
        public void ForceSynchronization()
        {
            if (!isServer) return;
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Forcing synchronization");
                
            BroadcastLobbyData();
        }
        
        // Método público para solicitar datos desde el cliente
        [Client]
        public void ForceRefresh()
        {
            if (!isClient) return;
            
            if (!NetworkClient.isConnected)
            {
                if (debugSync)
                    Debug.LogWarning("[LobbyDataSynchronizer] Cannot force refresh - not connected to server");
                return;
            }
            
            if (debugSync)
                Debug.Log("[LobbyDataSynchronizer] Force requesting refresh");
            
            try
            {
                RequestLobbyData();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LobbyDataSynchronizer] Error during force refresh: {ex.Message}");
            }
        }
    }
}