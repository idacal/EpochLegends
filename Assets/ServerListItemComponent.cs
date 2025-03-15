using UnityEngine;
using EpochLegends.Core.UI.Menu;

// Este script solo sirve como puente para crear el componente real en tiempo de ejecución
public class ServerListItemComponent : MonoBehaviour
{
    private void Awake()
    {
        // Al iniciar, añadimos el componente real y destruimos este
        gameObject.AddComponent<ServerListItem>();
        Destroy(this);
    }
}