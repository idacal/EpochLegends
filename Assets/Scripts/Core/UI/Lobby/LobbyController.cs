using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Systems.Team.Manager;
using EpochLegends.Systems.Team.Assignment;

namespace EpochLegends.UI.Lobby
{
    // Usamos MonoBehaviour porque la UI no necesita ser un NetworkBehaviour
    public class LobbyController : MonoBehaviour
    {
        [Header("Server Info")]
        [SerializeField] private TextMeshProUGUI serverNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private GameObject waitingForPlayersText;

        [Header("Team Panels")]
        [SerializeField] private Transform[] teamContainers; // Asigna contenedores para cada equipo (por ejemplo, 2)
        [SerializeField] private TextMeshProUGUI[] teamNameTexts;
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Player Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button teamSwitchButton;
        [SerializeField] private Button disconnectButton;

        // Referencias a otros managers
        private NetworkManager networkManager;
        private TeamManager teamManager;
        private TeamAssignment teamAssignment;
        private EpochLegends.GameManager gameManager;

        // Estado local
        private bool isPlayerReady = false;
        private bool isLocalPlayerHost = false;

        // Cache de entradas de jugador en la UI
        private Dictionary<uint, PlayerInfo> currentPlayerInfos = new Dictionary<uint, PlayerInfo>();
        private Dictionary<uint, GameObject> playerEntries = new Dictionary<uint, GameObject>();

        // Definición común de PlayerInfo para la UI
        public struct PlayerInfo
        {
            public uint NetId;
            public string PlayerName;
            public int TeamId;
            public bool IsReady;
            public bool IsHost;
        }

        private void Awake()
        {
            networkManager = NetworkManager.singleton;
            teamManager = FindObjectOfType<TeamManager>();
            teamAssignment = FindObjectOfType<TeamAssignment>();
            gameManager = FindObjectOfType<EpochLegends.GameManager>();

            if (networkManager == null || teamManager == null || gameManager == null)
            {
                Debug.LogError("No se encontraron los managers requeridos en la escena.");
            }

            // Configurar listeners de botones
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);
            if (teamSwitchButton != null)
                teamSwitchButton.onClick.AddListener(OnTeamSwitchClicked);
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }

        private void Start()
        {
            // Determinar si somos host
            isLocalPlayerHost = NetworkServer.active;

            // Actualizar UI según el rol
            if (isLocalPlayerHost)
            {
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(true);
            }
            else
            {
                if (startGameButton != null)
                    startGameButton.gameObject.SetActive(false);
            }

            // Configurar información del servidor
            if (serverNameText != null)
            {
                serverNameText.text = $"Server: {GetServerName()}";
            }

            // Inicializar nombres de equipos
            UpdateTeamNames();

            // Estado inicial de botones
            UpdateReadyButtonText();
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
        }

        private void Update()
        {
            UpdatePlayerCount();
            UpdateStartButtonState();
            UpdateWaitingForPlayersText();
            RefreshPlayerList();
        }

        #region UI Updates

        private string GetServerName()
        {
            return "Epoch Legends Server";
        }

        private void UpdateTeamNames()
        {
            if (teamManager == null) return;
            for (int i = 0; i < teamContainers.Length && i < teamNameTexts.Length; i++)
            {
                var config = teamManager.GetTeamConfig(i + 1); // Suponiendo que los IDs de equipo inician en 1
                if (config != null)
                {
                    teamNameTexts[i].text = config.teamName;
                    teamNameTexts[i].color = config.teamColor;
                }
            }
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && gameManager != null)
            {
                int count = gameManager.ConnectedPlayerCount;
                playerCountText.text = $"Players: {count}";
            }
        }

        private void UpdateReadyButtonText()
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = isPlayerReady ? "NOT READY" : "READY";
            }
        }

        private void UpdateStartButtonState()
        {
            if (startGameButton != null && isLocalPlayerHost && gameManager != null)
            {
                bool canStart = gameManager.ConnectedPlayerCount >= 2 && CheckAllPlayersReady();
                startGameButton.interactable = canStart;
            }
        }

        private void UpdateWaitingForPlayersText()
        {
            if (waitingForPlayersText != null && gameManager != null)
            {
                bool needMore = gameManager.ConnectedPlayerCount < 2;
                waitingForPlayersText.SetActive(needMore);
            }
        }

        #endregion

        #region Actualización de la Lista de Jugadores

        // Este método consulta a GameManager la lista de conexiones y reconstruye la lista de UI.
        private void RefreshPlayerList()
        {
            if (gameManager == null) return;

            // Construimos una lista de PlayerInfo "local" a partir de los jugadores conectados en GameManager
            Dictionary<uint, PlayerInfo> updatedInfos = new Dictionary<uint, PlayerInfo>();

            foreach (var kvp in gameManager.ConnectedPlayers)
            {
                uint netId = 0;
                string playerName = "Unknown";
                if (kvp.Key.identity != null)
                {
                    netId = kvp.Key.identity.netId;
                    playerName = kvp.Key.identity.gameObject.name;
                }
                bool isHost = kvp.Key == NetworkServer.localConnection;
                int teamId = kvp.Value.TeamId;
                bool ready = kvp.Value.IsReady;

                updatedInfos[netId] = new PlayerInfo
                {
                    NetId = netId,
                    PlayerName = playerName,
                    TeamId = teamId,
                    IsReady = ready,
                    IsHost = isHost
                };
            }

            // Actualizamos la UI: agregamos nuevas entradas y removemos las que ya no están
            // Agregar o actualizar entradas
            foreach (var info in updatedInfos.Values)
            {
                if (playerEntries.ContainsKey(info.NetId))
                {
                    // Aquí podrías actualizar la entrada si fuera necesario
                    // Por ejemplo, cambiar el indicador de ready, etc.
                }
                else
                {
                    CreatePlayerEntry(info);
                }
            }

            // Remover entradas que ya no están
            List<uint> toRemove = playerEntries.Keys.Except(updatedInfos.Keys).ToList();
            foreach (var netId in toRemove)
            {
                if (playerEntries.TryGetValue(netId, out GameObject entry))
                {
                    Destroy(entry);
                }
                playerEntries.Remove(netId);
            }
        }

        private void CreatePlayerEntry(PlayerInfo info)
        {
            // Asigna el contenedor según el equipo (suponiendo que teamContainers[0] es equipo 1 y [1] equipo 2)
            int teamIndex = info.TeamId - 1;
            if (teamIndex < 0 || teamIndex >= teamContainers.Length)
                teamIndex = 0;

            if (playerEntryPrefab != null && teamContainers[teamIndex] != null)
            {
                GameObject entry = Instantiate(playerEntryPrefab, teamContainers[teamIndex]);
                // Suponiendo que el prefab tiene un componente que muestra el nombre y estado
                Text entryText = entry.GetComponentInChildren<Text>();
                if (entryText != null)
                {
                    entryText.text = info.PlayerName + (info.IsHost ? " (Host)" : "");
                }
                // Si el prefab tiene un indicador de "ready", puedes actualizarlo aquí.
                playerEntries[info.NetId] = entry;
            }
        }

        private bool CheckAllPlayersReady()
        {
            if (gameManager == null) return false;
            foreach (var kvp in gameManager.ConnectedPlayers)
            {
                if (!kvp.Value.IsReady)
                    return false;
            }
            return true;
        }

        #endregion

        #region Handlers de Botones

        private void OnReadyButtonClicked()
        {
            isPlayerReady = !isPlayerReady;
            UpdateReadyButtonText();

            // Enviar el estado "ready" al servidor
            if (NetworkClient.active && NetworkClient.connection != null)
            {
                NetworkClient.connection.Send(new ReadyStateMessage
                {
                    isReady = isPlayerReady
                });
            }
        }

        private void OnStartGameClicked()
        {
            if (!isLocalPlayerHost) return;

            if (gameManager != null && gameManager.ConnectedPlayerCount >= 2)
            {
                if (CheckAllPlayersReady())
                {
                    gameManager.StartHeroSelection();
                }
                else
                {
                    Debug.Log("No se puede iniciar el juego: no todos los jugadores están listos.");
                }
            }
            else
            {
                Debug.Log("No se puede iniciar el juego: se necesitan al menos 2 jugadores.");
            }
        }

        private void OnTeamSwitchClicked()
        {
            // Aquí se podría solicitar un cambio de equipo mediante TeamAssignment
            if (teamAssignment != null)
            {
                int currentTeam = GetLocalPlayerTeam();
                int newTeam = (currentTeam == 1) ? 2 : 1;
                teamAssignment.RequestTeamChange(newTeam);
            }
            else
            {
                Debug.Log("No se encontró el TeamAssignment.");
            }
        }

        private void OnDisconnectClicked()
        {
            if (NetworkClient.active)
            {
                if (NetworkServer.active)
                {
                    NetworkManager.singleton.StopHost();
                }
                else
                {
                    NetworkManager.singleton.StopClient();
                }
            }
        }

        private int GetLocalPlayerTeam()
        {
            if (gameManager == null) return 1;
            foreach (var kvp in gameManager.ConnectedPlayers)
            {
                if (kvp.Key.identity != null &&
                    kvp.Key.identity.netId == NetworkClient.localPlayer.netId)
                {
                    return kvp.Value.TeamId;
                }
            }
            return 1;
        }

        #endregion
    }

    // Mensajes para la comunicación de estado "ready" y cambio de equipo
    public struct ReadyStateMessage : NetworkMessage
    {
        public bool isReady;
    }
    public struct TeamChangeMessage : NetworkMessage
    {
        public int teamId;
    }
}
