using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace EpochLegends.Core
{
    /// <summary>
    /// Componente que se encarga de gestionar los managers del juego,
    /// asegurando que persistan entre escenas y que se inicialicen en el orden correcto.
    /// </summary>
    public class ManagersController : MonoBehaviour
    {
        [System.Serializable]
        public class ManagerPrefab
        {
            public string managerName;
            public GameObject prefab;
            public bool isRequired = true;
        }

        [Header("Managers")]
        [SerializeField] private List<ManagerPrefab> managerPrefabs = new List<ManagerPrefab>();
        
        [Header("Debug")]
        [SerializeField] private bool debugEnabled = true;
        
        // Singleton para fácil acceso
        public static ManagersController Instance { get; private set; }
        
        // Instancias de managers creadas
        private Dictionary<string, GameObject> instantiatedManagers = new Dictionary<string, GameObject>();

private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
    
    Instance = this;
    
    // Preserva todo el contenedor y sus hijos
    DontDestroyOnLoad(gameObject);
    
    if (debugEnabled) // Usa el nombre correcto de tu variable de depuración
        Debug.Log("[ManagersController] Initialized and marked DontDestroyOnLoad");
    
    // Inicializar todos los managers necesarios
    InitializeManagers();
}
        
        private void InitializeManagers()
        {
            if (debugEnabled)
                Debug.Log($"[ManagersController] Initializing {managerPrefabs.Count} managers...");
            
            foreach (var managerEntry in managerPrefabs)
            {
                if (managerEntry.prefab == null)
                {
                    Debug.LogError($"[ManagersController] Prefab for manager '{managerEntry.managerName}' is null!");
                    continue;
                }
                
                // Verificar si ya existe una instancia
                if (instantiatedManagers.ContainsKey(managerEntry.managerName))
                {
                    if (debugEnabled)
                        Debug.Log($"[ManagersController] Manager '{managerEntry.managerName}' already instantiated, skipping.");
                    continue;
                }
                
                // Buscar si ya existe en la escena
                var existingInstance = FindManagerInScene(managerEntry.managerName);
                if (existingInstance != null)
                {
                    DontDestroyOnLoad(existingInstance);
                    instantiatedManagers[managerEntry.managerName] = existingInstance;
                    if (debugEnabled)
                        Debug.Log($"[ManagersController] Found existing manager '{managerEntry.managerName}', marking DontDestroyOnLoad");
                    continue;
                }
                
                // Crear nueva instancia
                var instance = Instantiate(managerEntry.prefab);
                instance.name = managerEntry.managerName;
                DontDestroyOnLoad(instance);
                instantiatedManagers[managerEntry.managerName] = instance;
                
                if (debugEnabled)
                    Debug.Log($"[ManagersController] Instantiated manager '{managerEntry.managerName}'");
            }
            
            if (debugEnabled)
                Debug.Log("[ManagersController] All managers initialized successfully");
        }
        
        private GameObject FindManagerInScene(string managerName)
        {
            // Buscar por nombre exacto
            var objects = FindObjectsOfType<GameObject>();
            foreach (var obj in objects)
            {
                if (obj.name == managerName)
                    return obj;
            }
            
            return null;
        }
        
        public GameObject GetManager(string managerName)
        {
            if (instantiatedManagers.TryGetValue(managerName, out GameObject manager))
            {
                return manager;
            }
            
            Debug.LogWarning($"[ManagersController] Requested manager '{managerName}' not found");
            return null;
        }
        
        public T GetManager<T>(string managerName) where T : Component
        {
            var manager = GetManager(managerName);
            if (manager != null)
            {
                return manager.GetComponent<T>();
            }
            
            return null;
        }
        
        public bool HasManager(string managerName)
        {
            return instantiatedManagers.ContainsKey(managerName);
        }
        
        [ContextMenu("List All Managers")]
        public void ListAllManagers()
        {
            Debug.Log($"=== MANAGERS ({instantiatedManagers.Count}) ===");
            foreach (var entry in instantiatedManagers)
            {
                Debug.Log($"- {entry.Key}: {(entry.Value != null ? "Active" : "NULL")}");
            }
        }
        
        [ContextMenu("Reinitialize All Managers")]
        public void ReinitializeManagers()
        {
            Debug.Log("[ManagersController] Reinitializing all managers...");
            
            // Destruir managers existentes
            foreach (var entry in instantiatedManagers)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value);
                }
            }
            
            instantiatedManagers.Clear();
            
            // Recrear managers
            InitializeManagers();
        }
    }
}