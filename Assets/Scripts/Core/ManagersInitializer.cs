using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using EpochLegends.Core;

namespace EpochLegends
{
    /// <summary>
    /// Component to ensure all required managers are initialized before scene transitions
    /// </summary>
    public class ManagersInitializer : MonoBehaviour
    {
        [SerializeField] private GameObject managersControllerPrefab;
        
        // Required manager prefabs to ensure they are initialized
        [SerializeField] private GameObject teamManagerPrefab;
        [SerializeField] private GameObject teamAssignmentPrefab;
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject networkManagerPrefab;

        [Header("Debug")]
        [SerializeField] private bool debugInitialization = true;
        
        private void Awake()
        {
            if (debugInitialization)
                Debug.Log("[ManagersInitializer] Starting initialization...");
                
            // Ensure the ManagersController exists
            EnsureManagersController();
            
            // Subscribe to scene load event to verify managers on each scene transition
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from scene load event
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        private void EnsureManagersController()
        {
            // Check if ManagersController already exists
            ManagersController existingController = FindObjectOfType<ManagersController>();
            
            if (existingController == null)
            {
                if (managersControllerPrefab != null)
                {
                    if (debugInitialization)
                        Debug.Log("[ManagersInitializer] Creating ManagersController from prefab");
                        
                    // Instantiate ManagersController from prefab
                    Instantiate(managersControllerPrefab);
                }
                else
                {
                    if (debugInitialization)
                        Debug.LogWarning("[ManagersInitializer] No ManagersController prefab assigned!");
                }
            }
            else
            {
                if (debugInitialization)
                    Debug.Log("[ManagersInitializer] ManagersController already exists");
            }
            
            // Now force a managers check
            StartCoroutine(DelayedManagersCheck());
        }
        
        private IEnumerator DelayedManagersCheck()
        {
            // Wait a frame to ensure ManagersController has initialized
            yield return null;
            
            ManagersController controller = FindObjectOfType<ManagersController>();
            if (controller != null)
            {
                // Check for required managers and initialize if needed
                if (debugInitialization)
                    Debug.Log("[ManagersInitializer] Verifying required managers exist...");
                
                EnsureManagerExists(controller, "TeamManager", teamManagerPrefab);
                EnsureManagerExists(controller, "TeamAssignment", teamAssignmentPrefab);
                EnsureManagerExists(controller, "GameManager", gameManagerPrefab);
                
                // Log all managers
                controller.ListAllManagers();
            }
        }
        
        private void EnsureManagerExists(ManagersController controller, string managerName, GameObject prefab)
        {
            if (controller.HasManager(managerName))
            {
                if (debugInitialization)
                    Debug.Log($"[ManagersInitializer] Manager '{managerName}' already exists");
                    
                return;
            }
            
            if (prefab != null)
            {
                if (debugInitialization)
                    Debug.Log($"[ManagersInitializer] Creating missing manager '{managerName}'");
                    
                GameObject instance = Instantiate(prefab);
                instance.name = managerName;
                DontDestroyOnLoad(instance);
            }
            else
            {
                if (debugInitialization)
                    Debug.LogWarning($"[ManagersInitializer] Missing prefab for manager '{managerName}'!");
            }
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (debugInitialization)
                Debug.Log($"[ManagersInitializer] Scene loaded: {scene.name}. Verifying managers...");
                
            // Delay the check slightly to allow all objects to initialize
            StartCoroutine(DelayedManagersCheck());
        }
    }
}