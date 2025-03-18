using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Core.Hero;
using EpochLegends.Core.Network;
using EpochLegends.Core.HeroSelection.Registry;
using EpochLegends.Core.UI.Manager;
using EpochLegends.Systems.Team.Manager;

namespace EpochLegends.Core.HeroSelection.Manager
{
    public class HeroSelectionManager : NetworkBehaviour
    {
        [Header("Selection Settings")]
        [SerializeField] private float selectionTime = 60f;
        [SerializeField] private bool enforceUniquePicks = true;
        [SerializeField] private bool randomIfNotSelected = true;
        
        [Header("References")]
        [SerializeField] private HeroRegistry heroRegistry;
        [SerializeField] private TeamManager teamManager;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;
        
        // Synced state
        [SyncVar(hook = nameof(OnTimerChanged))]
        private float remainingTime = 60f;
        
        // Selection tracking using standard dictionaries for server-side logic
        private readonly Dictionary<uint, string> selectedHeroes = new Dictionary<uint, string>();
        private readonly Dictionary<uint, bool> readyPlayers = new Dictionary<uint, bool>();
        private readonly Dictionary<NetworkConnection, uint> _connectionToNetId = new Dictionary<NetworkConnection, uint>();
        
        // SyncVars for client display
        [SyncVar(hook = nameof(OnSelectedHeroesJsonChanged))]
        private string syncSelectedHeroesJson = "{}";
        
        [SyncVar(hook = nameof(OnReadyPlayersJsonChanged))]
        private string syncReadyPlayersJson = "{}";
        
        // Private state
        private bool selectionInProgress = false;
        private bool allPlayersReady = false;
        
        // Events
        public delegate void HeroSelectionEvent(uint playerNetId, string heroId);
        public static event HeroSelectionEvent OnHeroSelected;
        
        public delegate void SelectionPhaseEvent();
        public static event SelectionPhaseEvent OnSelectionComplete;
        
        #region Lifecycle
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize
            selectedHeroes.Clear();
            readyPlayers.Clear();
            _connectionToNetId.Clear();
            
            // Find references if not set
            if (heroRegistry == null) heroRegistry = FindObjectOfType<HeroRegistry>();
            if (teamManager == null) teamManager = FindObjectOfType<TeamManager>();
            
            // Register for network callbacks
            NetworkServer.RegisterHandler<HeroSelectionMessage>(OnHeroSelectionReceived);
            NetworkServer.RegisterHandler<ReadyStatusMessage>(OnReadyStatusReceived);
            
            // Initialize connection to netId mapping
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    _connectionToNetId[conn] = conn.identity.netId;
                }
            }
            
            // Delay initialization to let clients finish loading
            StartCoroutine(DelayedInitialization(1.0f));
        }
        
        private System.Collections.IEnumerator DelayedInitialization(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Now perform any server-side spawning
            Debug.Log("[HeroSelectionManager] Performing delayed initialization");
            
            // Start the hero selection automatically after initialization
            StartHeroSelection();
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            Debug.Log("[HeroSelectionManager] Cliente iniciado en escena " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            
            // Verificar que estamos en la escena correcta
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("HeroSelection"))
            {
                Debug.Log("[HeroSelectionManager] En escena de selección de héroe");
            }
            else
            {
                Debug.LogWarning("[HeroSelectionManager] No estamos en la escena de selección de héroe!");
            }
            
            // Initial deserialization of dictionaries
            DeserializeSelectedHeroes();
            DeserializeReadyPlayers();
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
        }
        
        private void Update()
        {
            if (isServer && selectionInProgress)
            {
                // Debug para confirmar que el timer se está actualizando en el servidor
                if (debugMode && Time.frameCount % 60 == 0) // Limitar logs para 1 vez por segundo aprox.
                {
                    Debug.Log($"[HeroSelectionManager] Server timer: {remainingTime:F1}");
                }
                
                // Update timer
                remainingTime -= Time.deltaTime;
                
                // Check if time expired
                if (remainingTime <= 0f)
                {
                    HandleTimeExpired();
                }
                
                // Check if all players are ready
                if (!allPlayersReady && AreAllPlayersReady())
                {
                    allPlayersReady = true;
                    StartSelectionCountdown();
                }
            }
        }
        
        #endregion
        
        #region Server Selection Management
        
        [Server]
        public void StartHeroSelection()
        {
            // Reset state
            selectedHeroes.Clear();
            readyPlayers.Clear();
            syncSelectedHeroesJson = "{}";
            syncReadyPlayersJson = "{}";
            remainingTime = selectionTime;
            allPlayersReady = false;
            selectionInProgress = true;
            
            // Notify clients
            RpcHeroSelectionStarted(selectionTime);
            
            Debug.Log("[HeroSelectionManager] Hero selection phase started with time: " + selectionTime);
        }
        
        [Server]
        private void HandleTimeExpired()
        {
            Debug.LogError("=== TIEMPO EXPIRADO ===");
            Debug.LogError($"Jugadores seleccionados: {selectedHeroes.Count}, Jugadores listos: {readyPlayers.Count}");
            
            // Añadir más información de diagnóstico
            foreach (var conn in NetworkServer.connections)
            {
                if (conn.Value != null && conn.Value.identity != null)
                {
                    uint netId = conn.Value.identity.netId;
                    bool hasSelectedHero = selectedHeroes.ContainsKey(netId);
                    bool isReady = readyPlayers.ContainsKey(netId) && readyPlayers[netId];
                    
                    Debug.LogError($"Jugador {netId}: Seleccionó héroe: {hasSelectedHero}, Está listo: {isReady}");
                }
            }
            
            // Time's up, ensure all players have selections
            if (randomIfNotSelected)
            {
                AssignRandomHeroesToNonReadyPlayers();
            }
            
            // Complete selection phase
            CompleteHeroSelection();
        }
        
        [Server]
        private void StartSelectionCountdown()
        {
            // All players ready, start a short countdown before completing
            float countdownTime = Mathf.Min(5f, remainingTime);
            RpcStartCountdown(countdownTime);
            
            // Set timer to countdown time
            remainingTime = countdownTime;
        }
        
        [Server]
        private void CompleteHeroSelection()
        {
            Debug.LogError("=== COMPLETANDO SELECCIÓN DE HÉROES ===");
            selectionInProgress = false;
            
            // Notify clients
            RpcHeroSelectionComplete();
            
            // Trigger event
            if (OnSelectionComplete != null)
            {
                Debug.LogError("Invocando evento OnSelectionComplete");
                OnSelectionComplete.Invoke();
            }
            else
            {
                Debug.LogError("ALERTA: OnSelectionComplete es null!");
            }
            
            // Notify game manager to transition to next phase
            EpochLegends.GameManager gameManager = FindObjectOfType<EpochLegends.GameManager>();
            if (gameManager != null)
            {
                Debug.LogError("Llamando a GameManager.OnHeroSelectionComplete");
                gameManager.OnHeroSelectionComplete(GetSelectionResults());
            }
            else
            {
                Debug.LogError("ALERTA: GameManager no encontrado!");
                
                // Intentar cargar la escena directamente como último recurso
                if (NetworkManager.singleton != null)
                {
                    Debug.LogError("Intentando cambiar la escena directamente...");
                    NetworkManager.singleton.ServerChangeScene("Gameplay");
                }
            }
            
            Debug.LogError("Hero selection phase completed");
        }
        
        [Server]
        private void AssignRandomHeroesToNonReadyPlayers()
        {
            // Get all connected players
            foreach (NetworkConnection conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    uint netId = conn.identity.netId;
                    
                    // If player hasn't selected a hero or isn't ready
                    if (!selectedHeroes.ContainsKey(netId) || !readyPlayers.ContainsKey(netId) || !readyPlayers[netId])
                    {
                        // Assign random hero
                        string randomHeroId = GetRandomAvailableHero();
                        if (!string.IsNullOrEmpty(randomHeroId))
                        {
                            SelectHero(netId, randomHeroId);
                            SetPlayerReady(netId, true);
                        }
                    }
                }
            }
        }
        
        [Server]
        private void SelectHero(uint playerNetId, string heroId)
        {
            Debug.LogError($"=== SERVIDOR: Seleccionando héroe {heroId} para jugador {playerNetId} ===");
            
            // Validate hero selection
            if (!IsHeroAvailable(heroId, playerNetId))
            {
                Debug.LogError($"Héroe {heroId} no disponible para jugador {playerNetId}");
                
                // If enforcing unique picks and this hero is already taken, pick another
                if (enforceUniquePicks)
                {
                    string alternateHero = GetRandomAvailableHero();
                    if (!string.IsNullOrEmpty(alternateHero))
                    {
                        Debug.LogError($"Asignando héroe alternativo: {alternateHero}");
                        heroId = alternateHero;
                    }
                }
            }
            
            // Update selection in dictionary
            selectedHeroes[playerNetId] = heroId;
            
            // Update synced JSON
            SerializeSelectedHeroes();
            
            // Enviar RPC directamente además de usar SyncVar para mayor fiabilidad
            RpcHeroSelected(playerNetId, heroId);
            
            // Trigger event locally
            Debug.LogError($"Disparando evento OnHeroSelected para jugador {playerNetId} con héroe {heroId}");
            OnHeroSelected?.Invoke(playerNetId, heroId);
            
            Debug.LogError($"Player {playerNetId} selected hero {heroId}");
        }
        
        [Server]
        private void SetPlayerReady(uint playerNetId, bool isReady)
        {
            Debug.LogError($"=== SERVIDOR: Estableciendo ready={isReady} para jugador {playerNetId} ===");
            
            // Update ready status
            readyPlayers[playerNetId] = isReady;
            
            // Update synced JSON
            SerializeReadyPlayers();
            
            // Enviar RPC directamente además de usar SyncVar para mayor fiabilidad
            RpcPlayerReadyChanged(playerNetId, isReady);
            
            Debug.LogError($"Player {playerNetId} ready status set to {isReady}");
            
            // Depuración adicional
            string jsonReady = "{}";
            if (readyPlayers.Count > 0)
            {
                jsonReady = "{";
                bool first = true;
                foreach (var kvp in readyPlayers)
                {
                    if (!first) jsonReady += ",";
                    jsonReady += $"\"{kvp.Key}\":{kvp.Value.ToString().ToLower()}";
                    first = false;
                }
                jsonReady += "}";
            }
            Debug.LogError($"Ready players JSON actual: {jsonReady}");
            Debug.LogError($"syncReadyPlayersJson: {syncReadyPlayersJson}");
            
            // Check if all players are now ready
            if (isReady && AreAllPlayersReady())
            {
                Debug.LogError("Todos los jugadores están listos");
                allPlayersReady = true;
                StartSelectionCountdown();
            }
        }
        
        [Server]
        private bool AreAllPlayersReady()
        {
            // Check if we have any players
            if (NetworkServer.connections.Count == 0 || selectedHeroes.Count == 0)
                return false;
                
            // Check that all players have selected and are ready
            foreach (NetworkConnection conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    uint netId = conn.identity.netId;
                    
                    // If player is connected but not ready or hasn't selected
                    if (!readyPlayers.ContainsKey(netId) || !readyPlayers[netId] || 
                        !selectedHeroes.ContainsKey(netId))
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        [Server]
        private bool IsHeroAvailable(string heroId, uint playerNetId)
        {
            // Validate hero exists
            if (heroRegistry == null || !heroRegistry.DoesHeroExist(heroId))
                return false;
                
            // Check if enforcing unique picks
            if (enforceUniquePicks)
            {
                // Check if another player has already picked this hero
                foreach (var entry in selectedHeroes)
                {
                    if (entry.Key != playerNetId && entry.Value == heroId)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        [Server]
        private string GetRandomAvailableHero()
        {
            if (heroRegistry == null) return string.Empty;
            
            // Get all heroes
            List<string> allHeroIds = heroRegistry.GetAllHeroIds();
            
            // Shuffle the list
            allHeroIds = allHeroIds.OrderBy(x => Random.value).ToList();
            
            // Find first available hero
            foreach (string heroId in allHeroIds)
            {
                bool isAvailable = true;
                
                // Check if enforcing unique picks
                if (enforceUniquePicks)
                {
                    foreach (var entry in selectedHeroes)
                    {
                        if (entry.Value == heroId)
                        {
                            isAvailable = false;
                            break;
                        }
                    }
                }
                
                if (isAvailable)
                {
                    return heroId;
                }
            }
            
            // If we couldn't find an available hero, just return the first one
            if (allHeroIds.Count > 0)
            {
                return allHeroIds[0];
            }
            
            return string.Empty;
        }
        
        [Server]
        private Dictionary<uint, string> GetSelectionResults()
        {
            // Return a copy of the selected heroes dictionary
            return new Dictionary<uint, string>(selectedHeroes);
        }
        
        [Server]
        public void OnPlayerDisconnected(NetworkConnection conn)
        {
            if (!selectionInProgress) return;
            
            if (_connectionToNetId.TryGetValue(conn, out uint netId))
            {
                Debug.Log($"[HeroSelectionManager] Player {netId} disconnected during hero selection");
                
                // Si la selección está en progreso y el jugador estaba listo,
                // podríamos considerar mantener su selección
                if (readyPlayers.TryGetValue(netId, out bool wasReady) && wasReady)
                {
                    Debug.Log($"[HeroSelectionManager] Keeping hero selection for disconnected player {netId}");
                    // Mantener la selección
                }
                else
                {
                    // Sino, remover su selección y estado
                    if (selectedHeroes.ContainsKey(netId))
                        selectedHeroes.Remove(netId);
                        
                    if (readyPlayers.ContainsKey(netId))
                        readyPlayers.Remove(netId);
                        
                    // Actualizar JSON sincronizado
                    SerializeSelectedHeroes();
                    SerializeReadyPlayers();
                    
                    Debug.Log($"[HeroSelectionManager] Removed hero selection for disconnected player {netId}");
                }
                
                // Eliminar de la lista de conexiones
                _connectionToNetId.Remove(conn);
                
                // Notificar a otros clientes sobre el cambio
                RpcPlayerDisconnected(netId);
                
                // Verificar si aún podemos continuar la partida
                CheckGameCanProceed();
            }
        }
        
        [Server]
        private void CheckGameCanProceed()
        {
            // Verificar si aún hay suficientes jugadores para iniciar el juego
            int playerCount = 0;
            
            foreach (NetworkConnection conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    playerCount++;
                }
            }
            
            if (playerCount < 2)
            {
                Debug.Log("[HeroSelectionManager] Not enough players to continue. Returning to lobby.");
                // Regresar al lobby si no hay suficientes jugadores
                CancelHeroSelection();
            }
        }
        
        [Server]
        private void CancelHeroSelection()
        {
            selectionInProgress = false;
            
            // Notificar a los clientes
            RpcHeroSelectionCancelled();
            
            // Notificar al GameManager para volver al lobby
            EpochLegends.GameManager gameManager = FindObjectOfType<EpochLegends.GameManager>();
            if (gameManager != null)
            {
                gameManager.ReturnToLobby();
            }
        }
        
       // No añadas un nuevo método ForceSceneChange, en su lugar modifica el existente
// Busca en el archivo HeroSelectionManager.cs el método ForceSceneChange existente
// y reemplázalo con esta implementación:

[Server]
public void ForceSceneChange()
{
    Debug.LogError("=== FORZANDO CAMBIO DE ESCENA DESDE HERO SELECTION ===");
    
    // Obtener la referencia al NetworkManager y usar el método existente ServerChangeScene
    if (NetworkManager.singleton != null)
    {
        Debug.LogError($"NetworkManager encontrado: {NetworkManager.singleton.GetType().Name}");
        
        // Obtener referencia al GameManager
        EpochLegends.GameManager gameManager = FindObjectOfType<EpochLegends.GameManager>();
        if (gameManager != null)
        {
            // Verificar si podemos usar StartGame para una transición más limpia
            Debug.LogError("Llamando a GameManager.StartGame() para una transición adecuada");
            gameManager.StartGame();
        }
        else
        {
            // Si no encontramos el GameManager, forzar cambio directamente
            Debug.LogError("GameManager no encontrado. Cambiando escena directamente.");
            string gameplayScene = "Gameplay";
            NetworkManager.singleton.ServerChangeScene(gameplayScene);
        }
    }
    else
    {
        Debug.LogError("NetworkManager.singleton es null");
    }
}
        
        #endregion
        
        #region SyncVar Hooks
        
        private void OnSelectedHeroesJsonChanged(string oldValue, string newValue)
        {
            Debug.LogError($"SyncVar selectedHeroes cambió: {newValue}");
            
            // Notificar a los clientes para que actualicen la UI
            if (isClient && !isServer)
            {
                Dictionary<uint, string> deserializedSelections = DeserializeSelectedHeroes();
                
                foreach (var entry in deserializedSelections)
                {
                    Debug.LogError($"Jugador {entry.Key} seleccionó héroe {entry.Value}");
                    OnHeroSelected?.Invoke(entry.Key, entry.Value);
                }
            }
        }
        
        private void OnReadyPlayersJsonChanged(string oldValue, string newValue)
        {
            Debug.LogError($"SyncVar readyPlayers cambió: {newValue}");
            
            // Notificar a los clientes para que actualicen la UI
            if (isClient && !isServer)
            {
                Dictionary<uint, bool> deserializedReady = DeserializeReadyPlayers();
                
                foreach (var entry in deserializedReady)
                {
                    Debug.LogError($"Jugador {entry.Key} ready: {entry.Value}");
                    RpcPlayerReadyChanged(entry.Key, entry.Value);
                }
            }
        }
        
        #endregion
        
        #region Dictionary Serialization
        
        // Serialize the selected heroes dictionary to JSON
        [Server]
        private void SerializeSelectedHeroes()
        {
            // Simple JSON format: {"key1":"value1","key2":"value2"}
            string json = "{";
            bool first = true;
            
            foreach (var kvp in selectedHeroes)
            {
                if (!first)
                    json += ",";
                    
                json += $"\"{kvp.Key}\":\"{kvp.Value}\"";
                first = false;
            }
            
            json += "}";
            syncSelectedHeroesJson = json;
        }
        
        // Serialize the ready players dictionary to JSON
        [Server]
        private void SerializeReadyPlayers()
        {
            // Simple JSON format: {"key1":true,"key2":false}
            string json = "{";
            bool first = true;
            
            foreach (var kvp in readyPlayers)
            {
                if (!first)
                    json += ",";
                    
                json += $"\"{kvp.Key}\":{kvp.Value.ToString().ToLower()}";
                first = false;
            }
            
            json += "}";
            syncReadyPlayersJson = json;
        }
        
        // Deserialize the selected heroes JSON to a dictionary
        [Client]
        private Dictionary<uint, string> DeserializeSelectedHeroes()
        {
            Dictionary<uint, string> result = new Dictionary<uint, string>();
            
            // Skip if JSON is empty or invalid
            if (string.IsNullOrEmpty(syncSelectedHeroesJson) || syncSelectedHeroesJson == "{}")
                return result;
                
            try
            {
                // Parse the JSON manually for simple format
                string content = syncSelectedHeroesJson.Trim('{', '}');
                string[] pairs = content.Split(',');
                
                foreach (string pair in pairs)
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    
                    string[] keyValue = pair.Split(':');
                    if (keyValue.Length != 2) continue;
                    
                    string key = keyValue[0].Trim('"');
                    string value = keyValue[1].Trim('"');
                    
                    if (uint.TryParse(key, out uint netId))
                    {
                        result[netId] = value;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error deserializing selected heroes: {e.Message}");
            }
            
            return result;
        }
        
        // Deserialize the ready players JSON to a dictionary
        [Client]
        private Dictionary<uint, bool> DeserializeReadyPlayers()
        {
            Dictionary<uint, bool> result = new Dictionary<uint, bool>();
            
            // Skip if JSON is empty or invalid
            if (string.IsNullOrEmpty(syncReadyPlayersJson) || syncReadyPlayersJson == "{}")
                return result;
                
            try
            {
                // Parse the JSON manually for simple format
                string content = syncReadyPlayersJson.Trim('{', '}');
                string[] pairs = content.Split(',');
                
                foreach (string pair in pairs)
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    
                    string[] keyValue = pair.Split(':');
                    if (keyValue.Length != 2) continue;
                    
                    string key = keyValue[0].Trim('"');
                    string value = keyValue[1].Trim();
                    
                    if (uint.TryParse(key, out uint netId) && bool.TryParse(value, out bool isReady))
                    {
                        result[netId] = isReady;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error deserializing ready players: {e.Message}");
            }
            
            return result;
        }
        
        #endregion
        
        #region Network Messages
        
        // Hero selection message from client
        private struct HeroSelectionMessage : NetworkMessage
        {
            public string heroId;
        }
        
        [Server]
        private void OnHeroSelectionReceived(NetworkConnection conn, HeroSelectionMessage msg)
        {
            if (!selectionInProgress || conn.identity == null) return;
            
            uint playerNetId = conn.identity.netId;
            
            Debug.Log($"[HeroSelectionManager] Hero selection request received: Player {playerNetId} selected {msg.heroId}");
            
            // Validate and process hero selection
            if (IsHeroAvailable(msg.heroId, playerNetId))
            {
                SelectHero(playerNetId, msg.heroId);
            }
            else
            {
                Debug.Log($"[HeroSelectionManager] Hero {msg.heroId} is not available for player {playerNetId}");
                // Notify client about invalid selection
                TargetInvalidSelection(conn, msg.heroId);
            }
        }
        
        // Ready status message from client
        private struct ReadyStatusMessage : NetworkMessage
        {
            public bool isReady;
        }
        
        [Server]
        private void OnReadyStatusReceived(NetworkConnection conn, ReadyStatusMessage msg)
        {
            if (!selectionInProgress || conn.identity == null) return;
            
            uint playerNetId = conn.identity.netId;
            
            // Only allow ready if a hero is selected
            if (msg.isReady && !selectedHeroes.ContainsKey(playerNetId))
            {
                TargetNotifyNoHeroSelected(conn);
                return;
            }
            
            // Update ready status
            SetPlayerReady(playerNetId, msg.isReady);
            
            // Check if all players are now ready
            if (msg.isReady && AreAllPlayersReady())
            {
                allPlayersReady = true;
                StartSelectionCountdown();
            }
        }
        
        #endregion
        
        #region Client RPCs
        
        [ClientRpc]
        private void RpcHeroSelectionStarted(float time)
        {
            // Show hero selection UI
            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                uiManager.ShowPanel(UIPanel.HeroSelection);
            }
            
            // Inform UI about time
            remainingTime = time;
            
            Debug.Log($"[HeroSelectionManager] Hero selection started with time: {time}");
        }
        
        [ClientRpc]
        private void RpcStartCountdown(float countdownTime)
        {
            Debug.Log($"All players ready. Selection completing in {countdownTime} seconds.");
            
            // UI could display countdown animation
        }
        
        [ClientRpc]
        private void RpcHeroSelectionComplete()
        {
            Debug.LogError("=== CLIENTE: Selección de héroes completada ===");
            
            // Verificar escena actual
            Debug.LogError($"Escena actual: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            
            // Show loading UI
            UIManager uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                Debug.LogError("Mostrando panel de carga");
                uiManager.ShowPanel(UIPanel.Loading);
            }
            else
            {
                Debug.LogError("ALERTA: UIManager no encontrado!");
            }
        }
        
        [ClientRpc]
        private void RpcHeroSelected(uint playerNetId, string heroId)
        {
            Debug.LogError($"CLIENTE: RpcHeroSelected recibido para jugador {playerNetId} con héroe {heroId}");
            // Trigger hero selected event for UI updates
            OnHeroSelected?.Invoke(playerNetId, heroId);
        }
        
        [ClientRpc]
        private void RpcPlayerReadyChanged(uint playerNetId, bool isReady)
        {
            // UI would update ready status indicators
            Debug.LogError($"CLIENTE: Player {playerNetId} ready status changed to {isReady}");
            
            // Buscar un UIController y forzar actualización directa
            var uiController = FindObjectOfType<EpochLegends.UI.HeroSelection.HeroSelectionUIController>();
            if (uiController != null)
            {
                Debug.LogError("UIController encontrado, forzando actualización");
                var method = uiController.GetType().GetMethod("ForceRefreshAllUI");
                if (method != null)
                {
                    method.Invoke(uiController, null);
                }
            }
        }
        
        [ClientRpc]
        private void RpcPlayerDisconnected(uint playerNetId)
        {
            Debug.Log($"[HeroSelectionManager] Player {playerNetId} disconnected from hero selection");
            // La UI debería actualizarse automáticamente con los datos sincronizados
        }
        
        [ClientRpc]
        private void RpcHeroSelectionCancelled()
        {
            Debug.Log("Hero selection has been cancelled due to insufficient players.");
            // La UI puede mostrar un mensaje aquí
        }
        
        [TargetRpc]
        private void TargetInvalidSelection(NetworkConnection target, string heroId)
        {
            Debug.LogWarning($"Invalid hero selection: {heroId}");
            
            // UI could display error message
        }
        
        [TargetRpc]
        private void TargetNotifyNoHeroSelected(NetworkConnection target)
        {
            Debug.LogWarning("Cannot ready up without selecting a hero");
            
            // UI could display error message
        }
        
        [TargetRpc]
        private void TargetUpdateHeroSelection(NetworkConnection target, uint playerNetId, string heroId)
        {
            Debug.LogError($"Recibiendo actualización directa: Jugador {playerNetId} seleccionó héroe {heroId}");
            OnHeroSelected?.Invoke(playerNetId, heroId);
        }

        [TargetRpc]
        private void TargetUpdateReadyState(NetworkConnection target, uint playerNetId, bool isReady)
        {
            Debug.LogError($"Recibiendo actualización directa: Jugador {playerNetId} ready={isReady}");
            // Buscar la UI y actualizar directamente
            var uiController = FindObjectOfType<EpochLegends.UI.HeroSelection.HeroSelectionUIController>();
            if (uiController != null)
            {
                uiController.ForceRefreshAllUI();
            }
        }
        
        #endregion
        
        #region Sync Hooks
        
        private void OnTimerChanged(float oldValue, float newValue)
        {
            // Log solo si hay un cambio significativo o modo debug activado
            if (debugMode || Mathf.Abs(oldValue - newValue) > 1.0f)
            {
                Debug.Log($"[HeroSelectionManager] Selection timer sync: {newValue:F1} seconds");
            }
        }
        
        #endregion
        
        #region Client Methods
        
        [Client]
        public void SelectHeroLocally(string heroId)
        {
            Debug.LogError($"Cliente solicitando selección de héroe: {heroId}");
            
            // Send selection to server
            HeroSelectionMessage msg = new HeroSelectionMessage
            {
                heroId = heroId
            };
            
            NetworkClient.Send(msg);
        }
        
        [Client]
        public void SetReadyStatus(bool isReady)
        {
            Debug.LogError($"Cliente estableciendo estado ready: {isReady}");
            
            // Send ready status to server
            ReadyStatusMessage msg = new ReadyStatusMessage
            {
                isReady = isReady
            };
            
            NetworkClient.Send(msg);
        }
        
        [Client]
        public float GetRemainingTime()
        {
            // Añadir un log para depurar el valor del tiempo solo si el modo debug está activado
            if (debugMode)
            {
                Debug.Log($"[HeroSelectionManager] GetRemainingTime called, returning: {remainingTime:F1}");
            }
            return remainingTime;
        }
        
        [Client]
        public bool IsHeroSelectedByAnyPlayer(string heroId)
        {
            // Get the current selected heroes from the synced JSON
            Dictionary<uint, string> currentSelections = DeserializeSelectedHeroes();
            
            // Check if any player has selected this hero
            foreach (var selection in currentSelections)
            {
                if (selection.Value == heroId)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        [Client]
        public string GetSelectedHero(uint playerNetId)
        {
            // Get the current selected heroes from the synced JSON
            Dictionary<uint, string> currentSelections = DeserializeSelectedHeroes();
            
            // Check if the player has selected a hero
            if (currentSelections.TryGetValue(playerNetId, out string heroId))
            {
                return heroId;
            }
            
            return string.Empty;
        }
        
        [Client]
        public bool IsPlayerReady(uint playerNetId)
        {
            // Get the current ready players from the synced JSON
            Dictionary<uint, bool> currentReadyPlayers = DeserializeReadyPlayers();
            
            // Check if the player is ready
            if (currentReadyPlayers.TryGetValue(playerNetId, out bool isReady))
            {
                return isReady;
            }
            
            return false;
        }
        
        [Command(requiresAuthority = false)]
        public void CmdForceRefreshClientData(NetworkConnectionToClient sender = null)
        {
            Debug.LogError($"Cliente {sender.connectionId} solicitó actualización de datos");
            
            if (sender != null && sender.identity != null)
            {
                uint netId = sender.identity.netId;
                
                // Enviar todos los datos actuales al cliente
                foreach (var selection in selectedHeroes)
                {
                    TargetUpdateHeroSelection(sender, selection.Key, selection.Value);
                }
                
                foreach (var readyState in readyPlayers)
                {
                    TargetUpdateReadyState(sender, readyState.Key, readyState.Value);
                }
                
                Debug.LogError($"Datos enviados al cliente {netId}");
            }
        }
        
        // Método de utilidad para forzar el inicio de la selección (para pruebas)
        [Client]
        public void RequestStartHeroSelection()
        {
            if (isServer)
            {
                Debug.Log("[HeroSelectionManager] Server starting hero selection (requested by client)");
                StartHeroSelection();
            }
            else
            {
                Debug.Log("[HeroSelectionManager] Client requesting hero selection start - feature not implemented");
                // Aquí se podría implementar un mensaje al servidor para iniciar la selección
            }
        }

        // Método para iniciar manualmente el temporizador (para debugging)
        [Client]
        public void ForceTimerStart(float initialTime = 60f)
        {
            Debug.Log($"[HeroSelectionManager] Forzando inicio de temporizador: {initialTime}s");
            if (isServer)
            {
                remainingTime = initialTime;
                selectionInProgress = true;
            }
        }
        
        #endregion
    }  
}