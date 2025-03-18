using UnityEngine;
using Mirror;
using EpochLegends.Core.Hero;

namespace EpochLegends.Core.Player.Controller
{
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] private GameObject mobaCameraPrefab;
        [SerializeField] private float cameraCreationDelay = 0.5f;
        
        private MOBACamera playerCamera;
        private static CameraManager _instance;
        
        // Singleton para fácil acceso
        public static CameraManager Instance => _instance;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
        }
        
        private void Start()
        {
            // Solo crear cámara si estamos en un cliente (incluyendo host)
            if (NetworkClient.active)
            {
                // Pequeño delay para asegurar que todo se inicialice correctamente
                Invoke(nameof(CreatePlayerCamera), cameraCreationDelay);
            }
        }
        
        private void CreatePlayerCamera()
        {
            if (mobaCameraPrefab == null)
            {
                Debug.LogError("CameraManager: No se ha asignado un prefab de cámara MOBA");
                return;
            }
            
            // Verificar si ya existe una cámara principal
            Camera existingCamera = Camera.main;
            if (existingCamera != null && existingCamera.GetComponent<MOBACamera>() != null)
            {
                Debug.Log("CameraManager: Ya existe una cámara MOBA, usando la existente");
                playerCamera = existingCamera.GetComponent<MOBACamera>();
                return;
            }
            
            // Instanciar la cámara
            GameObject cameraObj = Instantiate(mobaCameraPrefab);
            cameraObj.name = "PlayerMOBACamera";
            
            // Guardar referencia
            playerCamera = cameraObj.GetComponent<MOBACamera>();
            
            if (playerCamera == null)
            {
                Debug.LogError("CameraManager: El prefab no contiene componente MOBACamera");
                return;
            }
            
            Debug.Log("CameraManager: Cámara del jugador creada");
            
            // La cámara encontrará automáticamente al héroe local mediante FindLocalHero()
        }
        
        // Método público para asignar manualmente un objetivo a la cámara
        public void SetCameraTarget(Transform target)
        {
            if (playerCamera != null && target != null)
            {
                playerCamera.SetTarget(target);
                Debug.Log($"CameraManager: Asignado objetivo manual a la cámara: {target.name}");
            }
            else if (playerCamera == null)
            {
                Debug.LogWarning("CameraManager: No hay una cámara MOBA activa");
            }
        }
        
        // Método público para centrar la cámara en el objetivo
        public void CenterCameraOnTarget()
        {
            if (playerCamera != null)
            {
                playerCamera.CenterOnPlayer();
                Debug.Log("CameraManager: Centrando cámara en objetivo");
            }
        }
        
        // Método para aplicar efecto de sacudida a la cámara
        public void ShakeCamera(float intensity = 0.5f, float duration = 0.5f)
        {
            if (playerCamera != null)
            {
                playerCamera.ShakeCamera(intensity, duration);
                Debug.Log($"CameraManager: Aplicando shake a cámara (intensidad: {intensity}, duración: {duration})");
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}