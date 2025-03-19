using UnityEngine;
using Mirror;

namespace EpochLegends.Core.Network
{
    /// <summary>
    /// Componente que maneja la sincronización del nombre del jugador y otras funcionalidades
    /// de red específicas del jugador.
    /// </summary>
    public class PlayerNetwork : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "";

        [Header("Debug Settings")]
        [SerializeField] private bool debugNetwork = true;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            
            // Establecer el nombre del jugador local cuando comienza
            string savedName = PlayerPrefs.GetString("PlayerName", "Player" + Random.Range(1000, 9999));
            
            if (debugNetwork)
                Debug.Log($"[PlayerNetwork] Setting local player name: {savedName}");
                
            CmdSetPlayerName(savedName);
        }

        // Este método es llamado en el servidor cuando el cliente solicita cambiar su nombre
        [Command]
        public void CmdSetPlayerName(string newName)
        {
            // Validación básica del nombre
            if (string.IsNullOrWhiteSpace(newName))
                newName = "Player" + Random.Range(1000, 9999);
                
            // Limitar longitud del nombre
            if (newName.Length > 20)
                newName = newName.Substring(0, 20);
                
            if (debugNetwork)
                Debug.Log($"[PlayerNetwork] Command received to set player name to: {newName}");
                
            // Actualizar el nombre
            playerName = newName;
            
            // Actualizar el nombre en GameManager si es necesario
            if (EpochLegends.GameManager.Instance != null)
            {
                EpochLegends.GameManager.Instance.UpdatePlayerName(netId, newName);
            }
        }
        
        // Este método se llama cuando el SyncVar 'playerName' cambia
        void OnPlayerNameChanged(string oldName, string newName)
        {
            // Actualizar el nombre del GameObject para facilitar depuración
            gameObject.name = $"Player [{newName}]";
            
            if (debugNetwork)
                Debug.Log($"[PlayerNetwork] Player name changed from '{oldName}' to '{newName}'");
        }
        
        // Método para actualizar el nombre del jugador (puede ser llamado desde la UI)
        public void UpdatePlayerName(string newName)
        {
            if (isLocalPlayer)
            {
                if (debugNetwork)
                    Debug.Log($"[PlayerNetwork] Local player requesting name update to: {newName}");
                    
                // Guardar en PlayerPrefs
                PlayerPrefs.SetString("PlayerName", newName);
                PlayerPrefs.Save();
                
                // Enviar al servidor
                CmdSetPlayerName(newName);
            }
        }
    }
}