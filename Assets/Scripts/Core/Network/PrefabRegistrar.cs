using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace EpochLegends.Core.Network
{
    /// <summary>
    /// Registra prefabs para sincronización en Mirror.
    /// Debe añadirse al mismo GameObject que tiene el NetworkManager.
    /// </summary>
    public class PrefabRegistrar : MonoBehaviour
    {
        [System.Serializable]
        public class NetworkPrefabRegistration
        {
            public string prefabName;
            public GameObject prefab;
            [Tooltip("Si está marcado, se añade a la lista de Spawnable Prefabs")]
            public bool addToSpawnList = true;
        }
        
        [Header("Prefabs para registro")]
        [SerializeField] private List<NetworkPrefabRegistration> networkPrefabs = new List<NetworkPrefabRegistration>();
        
        [Header("Debug")]
        [SerializeField] private bool debugEnabled = true;
        
        private void Awake()
        {
            if (debugEnabled)
                Debug.Log("[PrefabRegistrar] Initialized");
        }
        
        private void Start()
        {
            // Registrar prefabs al iniciar
            RegisterAllPrefabs();
        }
        
        /// <summary>
        /// Registra todos los prefabs configurados
        /// </summary>
        public void RegisterAllPrefabs()
        {
            NetworkManager networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                if (debugEnabled)
                    Debug.LogError("[PrefabRegistrar] Cannot register prefabs - NetworkManager not found on the same GameObject!");
                return;
            }
            
            if (debugEnabled)
                Debug.Log($"[PrefabRegistrar] Registering {networkPrefabs.Count} network prefabs...");
            
            foreach (var prefabEntry in networkPrefabs)
            {
                if (prefabEntry.prefab == null)
                {
                    if (debugEnabled)
                        Debug.LogWarning($"[PrefabRegistrar] Prefab '{prefabEntry.prefabName}' is null, skipping.");
                    continue;
                }
                
                // Verificar que el prefab tiene NetworkIdentity
                if (prefabEntry.prefab.GetComponent<NetworkIdentity>() == null)
                {
                    if (debugEnabled)
                        Debug.LogError($"[PrefabRegistrar] Prefab '{prefabEntry.prefabName}' doesn't have a NetworkIdentity component!");
                    continue;
                }
                
                // Añadir a la lista de spawnables si corresponde
                if (prefabEntry.addToSpawnList)
                {
                    // Verificar si ya está en la lista
                    bool alreadyInList = false;
                    foreach (var existing in networkManager.spawnPrefabs)
                    {
                        if (existing == prefabEntry.prefab)
                        {
                            alreadyInList = true;
                            break;
                        }
                    }
                    
                    if (!alreadyInList)
                    {
                        networkManager.spawnPrefabs.Add(prefabEntry.prefab);
                        
                        if (debugEnabled)
                            Debug.Log($"[PrefabRegistrar] Added '{prefabEntry.prefabName}' to spawnable prefabs list.");
                    }
                    else
                    {
                        if (debugEnabled)
                            Debug.Log($"[PrefabRegistrar] Prefab '{prefabEntry.prefabName}' already in spawnable list.");
                    }
                }
            }
            
            if (debugEnabled)
                Debug.Log("[PrefabRegistrar] Prefab registration completed.");
        }
        
        /// <summary>
        /// Verifica que los prefabs necesarios estén registrados
        /// </summary>
        [ContextMenu("Verify Prefab Registration")]
        public void VerifyPrefabRegistration()
        {
            NetworkManager networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[PrefabRegistrar] Cannot verify prefabs - NetworkManager not found on the same GameObject!");
                return;
            }
            
            Debug.Log($"=== NETWORK PREFABS ({networkManager.spawnPrefabs.Count}) ===");
            foreach (var prefab in networkManager.spawnPrefabs)
            {
                string prefabName = prefab != null ? prefab.name : "NULL";
                Debug.Log($"- {prefabName}");
            }
            
            // Verificar si faltan prefabs configurados
            foreach (var prefabEntry in networkPrefabs)
            {
                if (prefabEntry.prefab != null && prefabEntry.addToSpawnList)
                {
                    bool found = false;
                    foreach (var registeredPrefab in networkManager.spawnPrefabs)
                    {
                        if (registeredPrefab == prefabEntry.prefab)
                        {
                            found = true;
                            break;
                        }
                    }
                    
                    if (!found)
                    {
                        Debug.LogWarning($"[PrefabRegistrar] Prefab '{prefabEntry.prefabName}' is not registered but should be!");
                    }
                }
            }
        }
    }
}