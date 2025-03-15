using UnityEngine;
using Mirror;

namespace EpochLegends.Utils
{
    public static class NetworkUtils
    {
        // Método de utilidad para obtener un objeto spawneado por NetworkIdentity
        public static GameObject GetSpawnedObject(uint netId)
        {
            // Intenta obtener el objeto de NetworkServer primero (funciona en el servidor)
            if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity serverIdentity))
            {
                return serverIdentity.gameObject;
            }
            
            // Si no estamos en el servidor, intentar con objetos de cliente
            // Nota: Dependiendo de la versión de Mirror, puede ser necesario usar otra API
            foreach (var identity in Object.FindObjectsOfType<NetworkIdentity>())
            {
                if (identity.netId == netId)
                {
                    return identity.gameObject;
                }
            }
            
            return null;
        }
        
        // Método de utilidad para obtener una identidad de red por su netId
        public static NetworkIdentity GetNetworkIdentity(uint netId)
        {
            // Intenta obtener el objeto de NetworkServer primero (funciona en el servidor)
            if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity serverIdentity))
            {
                return serverIdentity;
            }
            
            // Si no estamos en el servidor, intentar con objetos de cliente
            foreach (var identity in Object.FindObjectsOfType<NetworkIdentity>())
            {
                if (identity.netId == netId)
                {
                    return identity;
                }
            }
            
            return null;
        }
        
        // Método para verificar si un objeto tiene autoridad del cliente local
        public static bool HasLocalAuthority(GameObject obj)
        {
            if (obj == null) return false;
            
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            return identity != null && identity.isLocalPlayer;
        }
    }
}