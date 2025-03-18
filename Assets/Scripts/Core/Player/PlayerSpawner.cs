using UnityEngine;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Hero;
using EpochLegends.Systems.Team.Manager;

namespace EpochLegends.Core.Player
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private float respawnDelay = 5f;
        
        // Referencia a otros sistemas
        private HeroFactory heroFactory;
        private TeamManager teamManager;
        private EpochLegends.GameManager gameManager;
        
        private Dictionary<uint, string> playerHeroSelections = new Dictionary<uint, string>();
        private Dictionary<uint, Hero.Hero> spawnedHeroes = new Dictionary<uint, Hero.Hero>();
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            Debug.Log("PlayerSpawner: OnStartServer");
            
            // Obtener referencias
            heroFactory = FindObjectOfType<HeroFactory>();
            teamManager = FindObjectOfType<TeamManager>();
            gameManager = FindObjectOfType<EpochLegends.GameManager>();
            
            if (heroFactory == null)
                Debug.LogError("PlayerSpawner: No se encontró HeroFactory");
                
            if (teamManager == null)
                Debug.LogError("PlayerSpawner: No se encontró TeamManager");
                
            // Cargar selecciones de héroe desde el GameManager
            if (gameManager != null)
            {
                foreach (var player in gameManager.ConnectedPlayers)
                {
                    if (!string.IsNullOrEmpty(player.Value.SelectedHeroId))
                    {
                        playerHeroSelections[player.Key] = player.Value.SelectedHeroId;
                        Debug.Log($"PlayerSpawner: Cargada selección para jugador {player.Key}: {player.Value.SelectedHeroId}");
                    }
                }
                
                // Iniciar spawn con delay para asegurar que todo esté listo
                Invoke(nameof(SpawnAllPlayers), 1.0f);
            }
            else
            {
                Debug.LogError("PlayerSpawner: No se encontró GameManager");
            }
        }
        
        [Server]
        private void SpawnAllPlayers()
        {
            Debug.Log($"PlayerSpawner: Iniciando spawn de jugadores, conexiones: {NetworkServer.connections.Count}");
            
            foreach (var player in NetworkServer.connections)
            {
                if (player.Value != null && player.Value.identity != null)
                {
                    SpawnPlayerHero(player.Value);
                }
                else
                {
                    Debug.LogWarning($"PlayerSpawner: Conexión {player.Key} no tiene identity válida");
                }
            }
            
            Debug.Log($"PlayerSpawner: Spawned heroes for {spawnedHeroes.Count} players");
        }
        
        [Server]
        private void SpawnPlayerHero(NetworkConnection conn)
        {
            if (conn == null || conn.identity == null) 
            {
                Debug.LogError("PlayerSpawner: Conexión o identity es null");
                return;
            }
            
            uint playerNetId = conn.identity.netId;
            Debug.Log($"PlayerSpawner: Intentando spawn para jugador {playerNetId}");
            
            // Obtener equipo asignado al jugador
            int teamId = teamManager.GetPlayerTeam(conn);
            if (teamId == -1)
            {
                Debug.LogError($"PlayerSpawner: Player {playerNetId} no tiene equipo asignado, asignando equipo 1 por defecto");
                teamId = 1; // Asignar equipo por defecto como fallback
            }
            
            // Obtener ID del héroe seleccionado
            string heroId = "";
            if (playerHeroSelections.TryGetValue(playerNetId, out heroId) && !string.IsNullOrEmpty(heroId))
            {
                Debug.Log($"PlayerSpawner: Encontrada selección de héroe {heroId} para jugador {playerNetId}");
            }
            else
            {
                // Fallback: usar un héroe aleatorio
                var allHeroes = heroFactory.GetAllHeroDefinitions();
                if (allHeroes != null && allHeroes.Count > 0)
                {
                    heroId = allHeroes[0].HeroId; // Primer héroe como fallback
                    Debug.LogWarning($"PlayerSpawner: No hero selection found for player {playerNetId}, using fallback hero: {heroId}");
                }
                else
                {
                    Debug.LogError("PlayerSpawner: No hay definiciones de héroe disponibles");
                    return;
                }
            }
            
            // Obtener punto de spawn para el equipo
            Transform spawnPoint = teamManager.GetRandomTeamSpawnPoint(teamId);
            if (spawnPoint == null)
            {
                Debug.LogError($"PlayerSpawner: No spawn point found for team {teamId}, using default position");
                // Fallback a posición por defecto
                spawnPoint = transform;
            }
            
            // Crear héroe usando la fábrica
            Hero.Hero hero = heroFactory.CreateHeroInstance(
                heroId, 
                spawnPoint.position, 
                spawnPoint.rotation, 
                teamId, 
                conn);
                
            if (hero != null)
            {
                // Si ya había un héroe para este jugador, destruirlo
                if (spawnedHeroes.TryGetValue(playerNetId, out Hero.Hero oldHero) && oldHero != null)
                {
                    oldHero.OnHeroDeath -= OnHeroDeath; // Eliminar listener
                    NetworkServer.Destroy(oldHero.gameObject);
                }
                
                spawnedHeroes[playerNetId] = hero;
                Debug.Log($"PlayerSpawner: Spawned hero {heroId} for player {playerNetId} on team {teamId}");
                
                // Registrar para evento de muerte para manejar respawn
                hero.OnHeroDeath += OnHeroDeath;
                
                // Notificar al cliente que su héroe ha sido spawneado
                TargetNotifyHeroSpawned(conn, hero.netId);
            }
            else
            {
                Debug.LogError($"PlayerSpawner: Failed to spawn hero {heroId} for player {playerNetId}");
            }
        }
        
        [Server]
        private void OnHeroDeath(Hero.Hero hero)
        {
            if (hero == null) return;
            
            Debug.Log($"PlayerSpawner: Detectada muerte de héroe {hero.name}");
            
            // Encontrar el netId del jugador
            uint playerNetId = 0;
            foreach (var entry in spawnedHeroes)
            {
                if (entry.Value == hero)
                {
                    playerNetId = entry.Key;
                    break;
                }
            }
            
            if (playerNetId != 0)
            {
                Debug.Log($"PlayerSpawner: Programando respawn para jugador {playerNetId} en {respawnDelay} segundos");
                
                // Programar respawn
                StartCoroutine(RespawnHero(playerNetId, respawnDelay));
            }
            else
            {
                Debug.LogWarning($"PlayerSpawner: No se pudo encontrar el jugador asociado al héroe {hero.name}");
            }
        }
        
        [Server]
        private System.Collections.IEnumerator RespawnHero(uint playerNetId, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            Debug.Log($"PlayerSpawner: Ejecutando respawn para jugador {playerNetId}");
            
            // Obtener conexión del jugador
            NetworkConnection conn = null;
            foreach (var connection in NetworkServer.connections)
            {
                if (connection.Value != null && connection.Value.identity != null && 
                    connection.Value.identity.netId == playerNetId)
                {
                    conn = connection.Value;
                    break;
                }
            }
            
            if (conn != null)
            {
                // Re-spawn el héroe
                SpawnPlayerHero(conn);
            }
            else
            {
                Debug.LogWarning($"PlayerSpawner: No se encontró conexión activa para el jugador {playerNetId}");
            }
        }
        
        [TargetRpc]
        private void TargetNotifyHeroSpawned(NetworkConnection target, uint heroNetId)
        {
            Debug.Log($"PlayerSpawner: Tu héroe ha sido spawneado con ID {heroNetId}");
            
            // Aquí podrías añadir código para actualizar la UI o realizar otras acciones en el cliente
        }
        
        // Método público para respawnear manualmente a un jugador (útil para debugging)
        [Server]
        public void RespawnPlayer(uint playerNetId)
        {
            NetworkConnection conn = null;
            foreach (var connection in NetworkServer.connections)
            {
                if (connection.Value != null && connection.Value.identity != null && 
                    connection.Value.identity.netId == playerNetId)
                {
                    conn = connection.Value;
                    break;
                }
            }
            
            if (conn != null)
            {
                SpawnPlayerHero(conn);
            }
        }
    }
}