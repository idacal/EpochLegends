using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Network;
using EpochLegends.Systems.Team.Manager;
using EpochLegends.Systems.Team.Assignment;

namespace EpochLegends.UI.Lobby
{
    public class LobbyController : NetworkBehaviour
    {
        [Header("Server Info")]
        [SerializeField] private TextMeshProUGUI serverNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;

        [Header("Team Panels")]
        [SerializeField] private Transform[] teamContainers;
        [SerializeField] private TextMeshProUGUI[] teamNameTexts;
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Player Controls")]
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button teamSwitchButton;
        [SerializeField] private Button disconnectButton;

        // References
        private NetworkManager networkManager;
        private TeamManager teamManager;
        private TeamAssignment teamAssignment;
        private GameManager gameManager;

        // Player entries cache
        private Dictionary<uint, PlayerEntry> playerEntries = new Dictionary<uint, PlayerEntry>();

        private void Awake()
        {
            // Find managers
            networkManager = FindObjectOfType<NetworkManager>();
            teamManager = FindObjectOfType<TeamManager>();
            teamAssignment = FindObjectOfType<TeamAssignment>();
            gameManager = FindObjectOfType<GameManager>();

            if (networkManager == null || teamManager == null || gameManager == null)
            {
                Debug.LogError("Required managers not found in scene!");
            }

            // Setup button listeners
            readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);
            startGameButton.onClick.AddListener(OnStartGameClicked);
            teamSwitchButton.onClick.AddListener(OnTeamSwitchClicked);
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();

            // Initially hide start game button (only host should see it)
            startGameButton.gameObject.SetActive(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Setup UI for client
            if (isServer)
            {
                // Show start game button for host
                startGameButton.gameObject.SetActive(true);
            }

            // Setup server info
            // Adapta esto a cómo obtienes el nombre del servidor en tu implementación
            if (networkManager != null)
            {
                // Si tu NetworkManager no tiene la propiedad ServerName, reemplaza esta línea
                // con la forma adecuada de obtener el nombre del servidor
                serverNameText.text = $"Server: {GetServerName()}";
            }

            // Initialize team names
            UpdateTeamNames();
        }

        // Método para obtener el nombre del servidor según tu implementación
        private string GetServerName()
        {
            // Adapta este método según cómo obtienes el nombre del servidor
            // Por ejemplo, podrías tenerlo en una variable estática o en otro componente
            return "Epoch Legends Server";
        }

        private void Update()
        {
            // Update player count
            UpdatePlayerCount();
        }

        #region UI Updates

        private void UpdateTeamNames()
        {
            if (teamManager == null) return;

            // Set team names in UI
            for (int i = 0; i < teamContainers.Length && i < teamNameTexts.Length; i++)
            {
                TeamConfig config = teamManager.GetTeamConfig(i + 1); // Assuming team IDs start at 1
                if (config != null)
                {
                    teamNameTexts[i].text = config.teamName;
                    // Could also set team colors here
                    teamNameTexts[i].color = config.teamColor;
                }
            }
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && gameManager != null)
            {
                int playerCount = gameManager.ConnectedPlayerCount;
                playerCountText.text = $"Players: {playerCount}";
            }
        }

        #endregion

        #region Player Management

        [ClientRpc]
        public void RpcUpdatePlayerList(PlayerInfo[] players)
        {
            // Clear current entries if needed
            if (playerEntries.Count > 0)
            {
                HashSet<uint> currentPlayers = new HashSet<uint>();
                
                // Update existing entries and add new ones
                foreach (var playerInfo in players)
                {
                    uint netId = playerInfo.NetId;
                    currentPlayers.Add(netId);
                    
                    if (playerEntries.TryGetValue(netId, out PlayerEntry entry))
                    {
                        // Update existing entry
                        entry.UpdateInfo(playerInfo);
                    }
                    else
                    {
                        // Create new entry
                        CreatePlayerEntry(playerInfo);
                    }
                }
                
                // Remove entries for players who left
                List<uint> toRemove = new List<uint>();
                foreach (var entry in playerEntries)
                {
                    if (!currentPlayers.Contains(entry.Key))
                    {
                        toRemove.Add(entry.Key);
                    }
                }
                
                foreach (var netId in toRemove)
                {
                    RemovePlayerEntry(netId);
                }
            }
            else
            {
                // Initial population
                foreach (var playerInfo in players)
                {
                    CreatePlayerEntry(playerInfo);
                }
            }
        }

        private void CreatePlayerEntry(PlayerInfo playerInfo)
        {
            if (teamContainers == null || teamContainers.Length < 2) return;

            // Determine which team container to use (0-based array index)
            int teamIndex = playerInfo.TeamId - 1;
            
            // Validate team index
            if (teamIndex < 0 || teamIndex >= teamContainers.Length)
            {
                teamIndex = 0;
            }

            // Create the entry
            GameObject entryObj = Instantiate(playerEntryPrefab, teamContainers[teamIndex]);
            PlayerEntry entry = entryObj.GetComponent<PlayerEntry>();
            
            if (entry != null)
            {
                entry.Initialize(playerInfo);
                playerEntries[playerInfo.NetId] = entry;
            }
        }

        private void RemovePlayerEntry(uint netId)
        {
            if (playerEntries.TryGetValue(netId, out PlayerEntry entry))
            {
                if (entry != null)
                {
                    Destroy(entry.gameObject);
                }
                playerEntries.Remove(netId);
            }
        }

        #endregion

        #region Button Handlers

        private void OnReadyToggleChanged(bool isReady)
        {
            if (!isClientOnly) return;

            // Send ready status to server
            CmdSetPlayerReady(isReady);
        }

        private void OnStartGameClicked()
        {
            if (!isServer) return;

            // Check if at least 2 players are connected
            if (gameManager != null && gameManager.ConnectedPlayerCount >= 2)
            {
                // Check if all players are ready
                // Adapta este código a tu implementación de GameManager
                // Si no tienes el método AreAllPlayersReady, deberás implementarlo o usar tu lógica
                bool allReady = CheckAllPlayersReady();
                
                if (allReady)
                {
                    // Start hero selection
                    gameManager.StartHeroSelection();
                }
                else
                {
                    // Show message that not all players are ready
                    Debug.Log("Cannot start game: Not all players are ready");
                }
            }
            else
            {
                // Show message that more players are needed
                Debug.Log("Cannot start game: Need at least 2 players");
            }
        }
        
        // Método para verificar si todos los jugadores están listos (adapta según tu implementación)
        private bool CheckAllPlayersReady()
        {
            // Implementa tu lógica para verificar si todos los jugadores están listos
            // Esto podría ser verificar un diccionario de estados de jugadores, o
            // podría estar ya implementado en tu GameManager
            
            // Por ahora asumiremos que todos están listos
            return true;
        }

        private void OnTeamSwitchClicked()
        {
            if (!isClientOnly || teamAssignment == null) return;

            // Request team switch to opposite team
            // In a real implementation, you'd have a more sophisticated UI for team selection
            int currentTeam = GetLocalPlayerTeam();
            int newTeam = (currentTeam == 1) ? 2 : 1;
            
            teamAssignment.RequestTeamChange(newTeam);
        }

        private void OnDisconnectClicked()
        {
            // Adapta este código a tu implementación de NetworkManager
            if (networkManager != null)
            {
                // Si tu NetworkManager no tiene el método DisconnectGame, reemplaza esto
                // con el método adecuado para desconectar
                Disconnect();
            }
        }
        
        // Método para desconectar según tu implementación
        private void Disconnect()
        {
            // Implementa tu lógica para desconectar
            // Por ejemplo, podrías usar:
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopClient();
            }
        }

        #endregion

        #region Server Commands

        [Command]
        private void CmdSetPlayerReady(bool isReady)
        {
            if (gameManager != null)
            {
                gameManager.SetPlayerReady(connectionToClient, isReady);
            }
        }

        #endregion

        #region Helper Methods

        private int GetLocalPlayerTeam()
        {
            if (teamManager == null) return 1;

            // Get local player's NetworkConnection
            NetworkConnection conn = connectionToClient;
            if (conn != null)
            {
                return teamManager.GetPlayerTeam(conn);
            }

            return 1; // Default to team 1
        }

        #endregion
    }

    // Helper struct to be used with RpcUpdatePlayerList
    public struct PlayerInfo
    {
        public uint NetId;
        public string PlayerName;
        public int TeamId;
        public bool IsReady;
        public bool IsHost;
    }

    // Component for individual player entries in the lobby
    public class PlayerEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image readyIndicator;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite notReadySprite;
        [SerializeField] private GameObject hostIndicator;

        private uint playerNetId;

        public void Initialize(PlayerInfo info)
        {
            playerNetId = info.NetId;
            UpdateInfo(info);
        }

        public void UpdateInfo(PlayerInfo info)
        {
            // Update player name
            if (playerNameText != null)
            {
                playerNameText.text = info.PlayerName;
            }

            // Update ready status
            if (readyIndicator != null)
            {
                readyIndicator.sprite = info.IsReady ? readySprite : notReadySprite;
            }

            // Update host indicator
            if (hostIndicator != null)
            {
                hostIndicator.SetActive(info.IsHost);
            }
        }
    }
}