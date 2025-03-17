using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace EpochLegends.Utils.Debug
{
    [AddComponentMenu("Network/NetworkDiagnostics")]
    public class NetworkDiagnostics : MonoBehaviour
    {
        [Header("Logging Settings")]
        [SerializeField] private bool logToConsole = true;
        [SerializeField] private bool logToFile = true;
        [SerializeField] private string logFilePath = "NetworkDiagnostics.log";

        // Variables to track statistics
        private Dictionary<uint, string> spawnedObjects = new Dictionary<uint, string>();
        private Dictionary<uint, string> lastFrameSpawnedObjects = new Dictionary<uint, string>();
        private int totalSpawns = 0;
        private int failedSpawns = 0;
        private StreamWriter logWriter;
        private string lastScene = "";
        private bool fileLogEnabled = true;

        public static NetworkDiagnostics Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Generate a unique log file path for each instance to avoid sharing violations
            if (logToFile)
            {
                // Create a unique log file for each session using timestamp and process ID
                string directory = Path.GetDirectoryName(logFilePath);
                string filename = Path.GetFileNameWithoutExtension(logFilePath);
                string extension = Path.GetExtension(logFilePath);
                
                // Create a unique log file name using timestamp and a random number
                string uniqueLogFile = string.IsNullOrEmpty(directory) ?
                    $"{filename}_{System.DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{Random.Range(1000, 9999)}{extension}" :
                    Path.Combine(directory, $"{filename}_{System.DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{Random.Range(1000, 9999)}{extension}");
                
                try
                {
                    // Try opening the file - if we can't, disable file logging
                    logWriter = new StreamWriter(uniqueLogFile, true);
                    logWriter.WriteLine($"=== Network Diagnostics Started at {System.DateTime.Now} ===");
                    logWriter.WriteLine($"Active Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                    logWriter.WriteLine($"Network Mode: {(NetworkServer.active ? "Server" : "Client")}");
                    logWriter.Flush();
                    
                    UnityEngine.Debug.Log($"[NetworkDiagnostics] Logging to file: {uniqueLogFile}");
                }
                catch (System.Exception e)
                {
                    fileLogEnabled = false;
                    logWriter = null;
                    UnityEngine.Debug.LogWarning($"[NetworkDiagnostics] File logging disabled: {e.Message}");
                }
            }
            
            Log($"NetworkDiagnostics initialized at {System.DateTime.Now}");
        }
        
        private void Start()
        {
            lastScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            InvokeRepeating("MonitorNetworkObjects", 0.2f, 0.5f);
        }

        private void OnDestroy()
        {
            if (logWriter != null)
            {
                try
                {
                    logWriter.WriteLine($"=== Network Diagnostics Stopped at {System.DateTime.Now} ===");
                    logWriter.Close();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[NetworkDiagnostics] Error closing log file: {e.Message}");
                }
                logWriter = null;
            }
        }
        
        private void Update()
        {
            // Monitor scene changes
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != lastScene)
            {
                if (NetworkServer.active)
                {
                    Log($"SERVER SCENE CHANGED: {currentScene}");
                }
                
                if (NetworkClient.active && !NetworkServer.active)
                {
                    Log($"CLIENT SCENE CHANGED: {currentScene}");
                }
                
                lastScene = currentScene;
            }
        }
        
        // Monitor network objects periodically
        private void MonitorNetworkObjects()
        {
            // Update previous frame copy
            lastFrameSpawnedObjects.Clear();
            foreach (var kvp in spawnedObjects)
            {
                lastFrameSpawnedObjects[kvp.Key] = kvp.Value;
            }
            
            // Clear current list
            spawnedObjects.Clear();
            
            // On server
            if (NetworkServer.active)
            {
                // Monitor server spawns
                foreach (var kvp in NetworkServer.spawned)
                {
                    uint netId = kvp.Key;
                    NetworkIdentity identity = kvp.Value;
                    
                    if (identity != null)
                    {
                        spawnedObjects[netId] = identity.name;
                        
                        // If new object
                        if (!lastFrameSpawnedObjects.ContainsKey(netId))
                        {
                            totalSpawns++;
                            Log($"SERVER SPAWN: {identity.name}, NetID: {netId}, SceneID: {identity.sceneId.ToString("X")}, AssetID: {identity.assetId}");
                        }
                    }
                }
                
                // Detect server despawns
                foreach (var kvp in lastFrameSpawnedObjects)
                {
                    if (!spawnedObjects.ContainsKey(kvp.Key))
                    {
                        Log($"SERVER UNSPAWN: {kvp.Value}, NetID: {kvp.Key}");
                    }
                }
            }
            
            // On pure client
            if (NetworkClient.active && !NetworkServer.active)
            {
                // Monitor client spawns
                foreach (var kvp in NetworkClient.spawned)
                {
                    uint netId = kvp.Key;
                    NetworkIdentity identity = kvp.Value;
                    
                    if (identity != null)
                    {
                        spawnedObjects[netId] = identity.name;
                        
                        // If new object
                        if (!lastFrameSpawnedObjects.ContainsKey(netId))
                        {
                            Log($"CLIENT SPAWN: {identity.name}, NetID: {netId}, SceneID: {identity.sceneId.ToString("X")}, AssetID: {identity.assetId}");
                        }
                    }
                }
                
                // Detect client despawns
                foreach (var kvp in lastFrameSpawnedObjects)
                {
                    if (!spawnedObjects.ContainsKey(kvp.Key))
                    {
                        Log($"CLIENT UNSPAWN: {kvp.Value}, NetID: {kvp.Key}");
                    }
                }
            }
        }

        private void Log(string message)
        {
            if (logToConsole)
            {
                UnityEngine.Debug.Log($"[NetworkDiagnostics] {message}");
            }

            if (logToFile && fileLogEnabled && logWriter != null)
            {
                try
                {
                    logWriter.WriteLine($"[{System.DateTime.Now.ToString("HH:mm:ss.fff")}] {message}");
                    logWriter.Flush();
                }
                catch (System.Exception e)
                {
                    // If we encounter an error, disable file logging to prevent further errors
                    UnityEngine.Debug.LogWarning($"[NetworkDiagnostics] File logging error, disabling: {e.Message}");
                    fileLogEnabled = false;
                    
                    try
                    {
                        logWriter.Close();
                    }
                    catch
                    {
                        // Ignore errors on close
                    }
                    
                    logWriter = null;
                }
            }
        }

        public void LogSpawnFailure(uint netId, uint assetId, ulong sceneId)
        {
            failedSpawns++;
            Log($"SPAWN FAILURE: Could not spawn netId={netId}, assetId={assetId}, sceneId={sceneId.ToString("X")}");
        }

        public void LogPrefabDiagnostics()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== NETWORK PREFAB DIAGNOSTICS ===");
            
            if (Mirror.NetworkManager.singleton != null)
            {
                sb.AppendLine($"Network Manager: {Mirror.NetworkManager.singleton.name}");
                sb.AppendLine($"Registered Spawn Prefabs: {Mirror.NetworkManager.singleton.spawnPrefabs.Count}");
                
                sb.AppendLine("\nRegistered Prefabs:");
                foreach (var prefab in Mirror.NetworkManager.singleton.spawnPrefabs)
                {
                    if (prefab != null)
                    {
                        NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
                        string assetId = identity != null ? identity.assetId.ToString() : "None";
                        string sceneId = identity != null ? identity.sceneId.ToString("X") : "None";
                        
                        sb.AppendLine($"- {prefab.name}: assetId={assetId}, sceneId={sceneId}");
                    }
                    else
                    {
                        sb.AppendLine("- NULL PREFAB REFERENCE FOUND!");
                    }
                }
            }
            else
            {
                sb.AppendLine("ERROR: No NetworkManager found!");
            }
            
            sb.AppendLine("\nCurrent Spawned Objects:");
            foreach (var kvp in spawnedObjects)
            {
                sb.AppendLine($"- NetID: {kvp.Key}, Name: {kvp.Value}");
            }
            
            sb.AppendLine($"\nStatistics:");
            sb.AppendLine($"- Total Spawns Detected: {totalSpawns}");
            sb.AppendLine($"- Failed Spawns: {failedSpawns}");
            
            string diagnostics = sb.ToString();
            Log(diagnostics);
        }
    }
}