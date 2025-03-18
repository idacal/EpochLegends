using UnityEngine;
using Mirror;
using EpochLegends.Core.Hero;
using EpochLegends.Systems.Team.Manager;

namespace EpochLegends.Core.Player
{
    public class RespawnController : NetworkBehaviour
    {
        [SerializeField] private float baseRespawnTime = 5f;
        [SerializeField] private float respawnTimePerLevel = 1f; // Tiempo adicional por nivel
        [SerializeField] private bool enableLevelBasedRespawn = true;
        
        // Referencias a otros sistemas
        private TeamManager teamManager;
        
        // UI para mostrar el temporizador
        [SerializeField] private GameObject respawnUIPanel;
        [SerializeField] private TMPro.TextMeshProUGUI respawnTimerText;
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            Debug.Log("RespawnController: OnStartServer");
            
            teamManager = FindObjectOfType<TeamManager>();
            if (teamManager == null)
            {
                Debug.LogError("RespawnController: No se encontró TeamManager");
            }
        }
        
        [Server]
        public void RegisterHeroForRespawn(Hero.Hero hero)
        {
            if (hero == null) return;
            
            Debug.Log($"RespawnController: Registrando héroe {hero.name} para respawn");
            
            // Calcular tiempo de respawn basado en nivel
            float respawnTime = baseRespawnTime;
            if (enableLevelBasedRespawn)
            {
                respawnTime += (hero.Level - 1) * respawnTimePerLevel;
            }
            
            // Notificar al cliente sobre el tiempo de respawn
            NetworkIdentity identity = hero.GetComponent<NetworkIdentity>();
            if (identity != null && identity.connectionToClient != null)
            {
                TargetNotifyRespawnTime(identity.connectionToClient, respawnTime);
            }
            
            // Iniciar coroutine para respawn
            StartCoroutine(RespawnHero(hero, respawnTime));
        }
        
        [Server]
        private System.Collections.IEnumerator RespawnHero(Hero.Hero hero, float delay)
        {
            Debug.Log($"RespawnController: Iniciando conteo de respawn para {hero.name}, tiempo: {delay}s");
            
            yield return new WaitForSeconds(delay);
            
            if (hero == null)
            {
                Debug.LogWarning("RespawnController: El héroe ya no existe durante el respawn");
                yield break;
            }
            
            // Obtener punto de respawn basado en equipo
            Transform respawnPoint = teamManager.GetRandomTeamSpawnPoint(hero.TeamId);
            
            if (respawnPoint == null)
            {
                Debug.LogError($"RespawnController: No respawn point found for team {hero.TeamId}");
                // Usar posición predeterminada
                respawnPoint = transform;
            }
            
            // Mover al héroe a la posición de respawn
            hero.transform.position = respawnPoint.position;
            hero.transform.rotation = respawnPoint.rotation;
            
            // Podemos asumir que el héroe tiene un método Respawn() en su clase
            // Si no lo tiene, necesitaríamos modificar Hero.cs para añadirlo
            // hero.Respawn();
            
            Debug.Log($"RespawnController: Héroe {hero.name} reposicionado en punto de respawn");
            
            // Notificar al cliente que ha reaparecido
            NetworkIdentity identity = hero.GetComponent<NetworkIdentity>();
            if (identity != null && identity.connectionToClient != null)
            {
                TargetNotifyRespawned(identity.connectionToClient);
            }
        }
        
        [TargetRpc]
        private void TargetNotifyRespawnTime(NetworkConnection conn, float respawnTime)
        {
            // Esta función se llama en el cliente para mostrar el temporizador de respawn
            Debug.Log($"RespawnController: Respawn en {respawnTime} segundos");
            
            // Mostrar panel de UI si está disponible
            if (respawnUIPanel != null)
            {
                respawnUIPanel.SetActive(true);
                
                // Iniciar coroutine para actualizar el timer en la UI
                StartCoroutine(UpdateRespawnUI(respawnTime));
            }
        }
        
        private System.Collections.IEnumerator UpdateRespawnUI(float totalTime)
        {
            float remainingTime = totalTime;
            
            while (remainingTime > 0)
            {
                // Actualizar texto del temporizador
                if (respawnTimerText != null)
                {
                    respawnTimerText.text = $"Reapareciendo en: {Mathf.CeilToInt(remainingTime)}";
                }
                
                // Esperar un frame
                yield return null;
                
                // Actualizar tiempo restante
                remainingTime -= Time.deltaTime;
            }
            
            // Asegurar que muestre 0 al final
            if (respawnTimerText != null)
            {
                respawnTimerText.text = "Reapareciendo en: 0";
            }
            
            // El respawn ocurrirá en el servidor, así que no hacemos nada aquí
        }
        
        [TargetRpc]
        private void TargetNotifyRespawned(NetworkConnection conn)
        {
            // Esta función se llama en el cliente cuando ha reaparecido
            Debug.Log("RespawnController: Has reaparecido");
            
            // Ocultar panel de UI si está visible
            if (respawnUIPanel != null)
            {
                respawnUIPanel.SetActive(false);
            }
            
            // Aquí podrías añadir efectos visuales o sonoros de respawn
        }
    }
}