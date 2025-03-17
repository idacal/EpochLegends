using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Text;

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
            [Tooltip("Prioridad de este prefab (los críticos deben tener mayor prioridad)")]
            public int priority = 0;
        }
        
        [Header("Prefabs críticos para el sistema")]
        [SerializeField] private List<NetworkPrefabRegistration> criticalPrefabs = new List<NetworkPrefabRegistration>();
        
        [Header("Prefabs para registro")]
        [SerializeField] private List<NetworkPrefabRegistration> networkPrefabs = new List<NetworkPrefabRegistration>();
        
        [Header("Auto-búsqueda y verificación")]
        [SerializeField] private bool autoFindNetworkPrefabs = true;
        [SerializeField] private bool verifyOnStart = true;
        [SerializeField] private string[] prefabFolders = new string[] { "Prefabs", "Resources/Prefabs" };
        
        [Header("Debug")]
        [SerializeField] private bool debugEnabled = true;
        
        // Referencia al NetworkManager
        private NetworkManager networkManager;
        
        private void Awake()
        {
            if (debugEnabled)
                Debug.Log("[PrefabRegistrar] Initialized");
                
            // Buscar el NetworkManager (debe estar en el mismo GameObject)
            networkManager = GetComponent<NetworkManager>();
            
            if (networkManager == null)
            {
                Debug.LogError("[PrefabRegistrar] No NetworkManager found on the same GameObject!");
            }
        }
        
        private void Start()
        {
            // Registrar prefabs al iniciar
            RegisterAllPrefabs();
            
            // Verificar integridad
            if (verifyOnStart)
            {
                Invoke("VerifyPrefabRegistry", 1f);
            }
        }
        
        /// <summary>
        /// Registra todos los prefabs configurados
        /// </summary>
        public void RegisterAllPrefabs()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
                
                if (networkManager == null)
                {
                    Debug.LogError("[PrefabRegistrar] Cannot register prefabs - NetworkManager not found on the same GameObject!");
                    return;
                }
            }
            
            if (debugEnabled)
                Debug.Log($"[PrefabRegistrar] Registering network prefabs...");
            
            int registeredCount = 0;
            
            // Registrar primero prefabs críticos
            foreach (var prefabEntry in criticalPrefabs)
            {
                if (RegisterPrefab(prefabEntry))
                    registeredCount++;
            }
            
            // Luego registrar prefabs normales
            foreach (var prefabEntry in networkPrefabs)
            {
                if (RegisterPrefab(prefabEntry))
                    registeredCount++;
            }
            
            // Auto-buscar prefabs adicionales si está habilitado
            if (autoFindNetworkPrefabs)
            {
                int autoFoundCount = AutoFindAndRegisterPrefabs();
                registeredCount += autoFoundCount;
                
                if (debugEnabled)
                    Debug.Log($"[PrefabRegistrar] Auto-found and registered {autoFoundCount} additional prefabs");
            }
            
            if (debugEnabled)
                Debug.Log($"[PrefabRegistrar] Prefab registration completed. Total: {registeredCount} prefabs registered.");
        }
        
        private bool RegisterPrefab(NetworkPrefabRegistration prefabEntry)
{
    // Verificar si el prefab es nulo o tiene nombre vacío
    if (prefabEntry.prefab == null)
    {
        if (debugEnabled)
            Debug.LogWarning($"[PrefabRegistrar] Prefab '{prefabEntry.prefabName ?? "unnamed"}' is null, skipping.");
        return false;
    }
    
    // Verificar que el prefab tiene NetworkIdentity
    NetworkIdentity identity = prefabEntry.prefab.GetComponent<NetworkIdentity>();
    if (identity == null)
    {
        Debug.LogError($"[PrefabRegistrar] Prefab '{prefabEntry.prefab.name}' doesn't have a NetworkIdentity component!");
        return false;
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
                Debug.Log($"[PrefabRegistrar] Added '{prefabEntry.prefab.name}' to spawnable prefabs list.");
                
            // Registrar el prefab en PrefabHandler (para algunos tipos de Mirror)
            NetworkClient.RegisterPrefab(prefabEntry.prefab);
            
            return true;
        }
        else
        {
            if (debugEnabled)
                Debug.Log($"[PrefabRegistrar] Prefab '{prefabEntry.prefab.name}' already in spawnable list.");
        }
    }
    
    return false;
}
        
        private int AutoFindAndRegisterPrefabs()
        {
            int count = 0;
            HashSet<GameObject> alreadyRegistered = new HashSet<GameObject>();
            
            // Agregar los prefabs ya registrados al conjunto
            foreach (var prefab in networkManager.spawnPrefabs)
            {
                alreadyRegistered.Add(prefab);
            }
            
            // Buscar prefabs en la carpeta Resources
            foreach (string folder in prefabFolders)
            {
                GameObject[] prefabs = Resources.LoadAll<GameObject>(folder);
                
                foreach (var prefab in prefabs)
                {
                    // Verificar si ya está registrado
                    if (alreadyRegistered.Contains(prefab))
                        continue;
                        
                    // Verificar si tiene NetworkIdentity
                    NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
                    if (identity != null)
                    {
                        networkManager.spawnPrefabs.Add(prefab);
                        alreadyRegistered.Add(prefab);
                        count++;
                        
                        if (debugEnabled)
                            Debug.Log($"[PrefabRegistrar] Auto-registered prefab '{prefab.name}' from Resources/{folder}");
                            
                        // Registrar el prefab en PrefabHandler (para algunos tipos de Mirror)
                        NetworkClient.RegisterPrefab(prefab);
                    }
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// Verifica que los prefabs necesarios estén registrados
        /// </summary>
        [ContextMenu("Verify Prefab Registration")]
        public void VerifyPrefabRegistry()
        {
            if (networkManager == null)
            {
                Debug.LogError("[PrefabRegistrar] Cannot verify prefabs - NetworkManager not found on the same GameObject!");
                return;
            }
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== NETWORK PREFABS ({networkManager.spawnPrefabs.Count}) ===");
            
            // Verificar prefabs registrados
            foreach (var prefab in networkManager.spawnPrefabs)
            {
                string prefabName = prefab != null ? prefab.name : "NULL";
                
                // Obtener assetId y verificar que sea válido
                NetworkIdentity identity = prefab?.GetComponent<NetworkIdentity>();
                string assetId = identity != null ? identity.assetId.ToString() : "MISSING_IDENTITY";
                
                sb.AppendLine($"- {prefabName} (AssetId: {assetId})");
                
                // Verificar problemas potenciales
                if (identity == null)
                {
                    sb.AppendLine($"  ERROR: Missing NetworkIdentity component!");
                }
                else if (string.IsNullOrEmpty(identity.assetId.ToString()) || identity.assetId.ToString() == "00000000-0000-0000-0000-000000000000")
                {
                    sb.AppendLine($"  WARNING: Empty assetId - may not spawn correctly!");
                }
            }
            
            // Verificar si faltan prefabs configurados
            sb.AppendLine("\nVerifying critical prefabs:");
            foreach (var prefabEntry in criticalPrefabs)
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
                        sb.AppendLine($"  CRITICAL ERROR: Prefab '{prefabEntry.prefabName}' is not registered but should be!");
                    }
                    else
                    {
                        sb.AppendLine($"  OK: '{prefabEntry.prefabName}' is properly registered");
                    }
                }
            }
            
            Debug.Log(sb.ToString());
            
            // Notificar al diagnóstico de red si existe
            EpochLegends.Utils.Debug.NetworkDiagnostics diagnostics = 
                FindObjectOfType<EpochLegends.Utils.Debug.NetworkDiagnostics>();
                
            if (diagnostics != null)
            {
                diagnostics.LogPrefabDiagnostics();
            }
        }
        
        /// <summary>
        /// Fuerza un registro completo de todos los prefabs, limpiando antes la lista
        /// </summary>
        [ContextMenu("Force Full Registration")]
        public void ForceFullRegistration()
        {
            if (networkManager == null)
            {
                networkManager = GetComponent<NetworkManager>();
                
                if (networkManager == null)
                {
                    Debug.LogError("[PrefabRegistrar] Cannot force registration - NetworkManager not found!");
                    return;
                }
            }
            
            // Limpiar lista actual
            networkManager.spawnPrefabs.Clear();
            
            // Registrar todos los prefabs
            RegisterAllPrefabs();
            
            // Verificar registro
            VerifyPrefabRegistry();
            
            Debug.Log("[PrefabRegistrar] Forced full prefab registration completed.");
        }
    }
}