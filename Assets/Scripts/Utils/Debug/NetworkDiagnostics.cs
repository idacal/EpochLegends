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
            
            Log($"NetworkDiagnostics initialized at {System.DateTime.Now}");
            
            if (logToFile)
            {
                try
                {
                    logWriter = new StreamWriter(logFilePath, true);
                    logWriter.WriteLine($"=== Network Diagnostics Started at {System.DateTime.Now} ===");
                    logWriter.Flush();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to open log file: {e.Message}");
                }
            }
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
                logWriter.WriteLine($"=== Network Diagnostics Stopped at {System.DateTime.Now} ===");
                logWriter.Close();
                logWriter = null;
            }
        }
        
        private void Update()
        {
            // Monitorear cambios de escena
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
        
        // Monitorear objetos de red periódicamente
        private void MonitorNetworkObjects()
        {
            // Actualizar copia del frame anterior
            lastFrameSpawnedObjects.Clear();
            foreach (var kvp in spawnedObjects)
            {
                lastFrameSpawnedObjects[kvp.Key] = kvp.Value;
            }
            
            // Limpiar la lista actual
            spawnedObjects.Clear();
            
            // En el servidor
            if (NetworkServer.active)
            {
                // Monitorear spawns en el servidor
                foreach (var kvp in NetworkServer.spawned)
                {
                    uint netId = kvp.Key;
                    NetworkIdentity identity = kvp.Value;
                    
                    if (identity != null)
                    {
                        spawnedObjects[netId] = identity.name;
                        
                        // Si es un objeto nuevo
                        if (!lastFrameSpawnedObjects.ContainsKey(netId))
                        {
                            totalSpawns++;
                            Log($"SERVER SPAWN: {identity.name}, NetID: {netId}, SceneID: {identity.sceneId.ToString("X")}, AssetID: {identity.assetId}");
                        }
                    }
                }
                
                // Detectar despawns en el servidor
                foreach (var kvp in lastFrameSpawnedObjects)
                {
                    if (!spawnedObjects.ContainsKey(kvp.Key))
                    {
                        Log($"SERVER UNSPAWN: {kvp.Value}, NetID: {kvp.Key}");
                    }
                }
            }
            
            // En el cliente puro
            if (NetworkClient.active && !NetworkServer.active)
            {
                // Monitorear spawns en el cliente
                foreach (var kvp in NetworkClient.spawned)
                {
                    uint netId = kvp.Key;
                    NetworkIdentity identity = kvp.Value;
                    
                    if (identity != null)
                    {
                        spawnedObjects[netId] = identity.name;
                        
                        // Si es un objeto nuevo
                        if (!lastFrameSpawnedObjects.ContainsKey(netId))
                        {
                            Log($"CLIENT SPAWN: {identity.name}, NetID: {netId}, SceneID: {identity.sceneId.ToString("X")}, AssetID: {identity.assetId}");
                        }
                    }
                }
                
                // Detectar despawns en el cliente
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

            if (logToFile && logWriter != null)
            {
                logWriter.WriteLine($"[{System.DateTime.Now.ToString("HH:mm:ss.fff")}] {message}");
                logWriter.Flush();
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