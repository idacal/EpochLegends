using UnityEngine;
using EpochLegends.Core.Hero;
using EpochLegends.Core.Player.Controller;

namespace EpochLegends.Systems.Feedback.Camera
{
    public class GameCameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private float angle = 45f;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector2 zoomRange = new Vector2(10f, 25f);
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float panSpeed = 30f;
        [SerializeField] private float panBorderThickness = 10f;
        [SerializeField] private Vector2 xBounds = new Vector2(-50f, 50f);
        [SerializeField] private Vector2 zBounds = new Vector2(-50f, 50f);
        
        [Header("Edge Panning")]
        [SerializeField] private bool useEdgePanning = true;
        [SerializeField] private bool useDragging = true;
        [SerializeField] private bool lockCameraToHero = false;
        
        // Referencias
        private UnityEngine.Camera mainCamera;
        private Hero targetHero;
        private Transform cameraTransform;
        
        // State tracking
        private Vector3 targetPosition;
        private float currentZoom;
        private bool isDragging = false;
        private Vector3 dragStartPosition;
        private Vector3 dragOrigin;
        
        private void Awake()
        {
            mainCamera = GetComponent<UnityEngine.Camera>();
            cameraTransform = transform;
            
            if (mainCamera == null)
            {
                Debug.LogError("Camera component not found on GameCameraController!");
            }
            
            // Set initial zoom to the middle of the range
            currentZoom = Mathf.Lerp(zoomRange.x, zoomRange.y, 0.5f);
            
            // Set initial position
            targetPosition = new Vector3(0f, 0f, 0f);
        }
        
        private void Start()
        {
            // Find local player's hero
            StartCoroutine(FindLocalHero());
            
            // Set initial camera position and rotation
            UpdateCameraTransform();
        }
        
        private void LateUpdate()
        {
            HandleInput();
            
            if (lockCameraToHero && targetHero != null)
            {
                targetPosition = targetHero.transform.position;
            }
            
            ClampTargetPosition();
            UpdateCameraTransform();
        }
        
        #region Initialization
        
        private System.Collections.IEnumerator FindLocalHero()
        {
            while (targetHero == null)
            {
                // Try to find the local player first
                PlayerController[] controllers = FindObjectsOfType<PlayerController>();
                foreach (var controller in controllers)
                {
                    // En tu implementación, verifica si el controller es local de otra manera
                    // En vez de hasAuthority, puedes usar isLocalPlayer o alguna otra propiedad
                    // que indique si el controlador pertenece al jugador local
                    if (IsLocalController(controller))
                    {
                        var hero = controller.ControlledHero;
                        if (hero != null)
                        {
                            targetHero = hero;
                            targetPosition = targetHero.transform.position;
                            break;
                        }
                    }
                }
                
                // Si no se encuentra a través del controlador, busca directamente el héroe
                if (targetHero == null)
                {
                    Hero[] heroes = FindObjectsOfType<Hero>();
                    foreach (var hero in heroes)
                    {
                        // En tu implementación, verifica si el héroe es local
                        if (IsLocalHero(hero))
                        {
                            targetHero = hero;
                            targetPosition = targetHero.transform.position;
                            break;
                        }
                    }
                }
                
                // Si encontramos un héroe, posicionamos la cámara
                if (targetHero != null)
                {
                    targetPosition = targetHero.transform.position;
                    UpdateCameraTransform();
                }
                
                // Esperar antes de intentar de nuevo
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // Esta función verifica si un controlador pertenece al jugador local
        // Adapta esta lógica según cómo implementaste esto en tu proyecto
        private bool IsLocalController(PlayerController controller)
        {
            // Ejemplo: podría ser que tengas un campo isLocalPlayer, o podrías
            // comparar con un ID de jugador local almacenado en algún lado
            
            // Por ahora, asumo que el primer controlador que encontramos es el local
            // Pero deberías reemplazar esto con tu lógica concreta
            return true; 
        }
        
        // Esta función verifica si un héroe pertenece al jugador local
        // Adapta según tu implementación
        private bool IsLocalHero(Hero hero)
        {
            // Similar a IsLocalController, adapta según tu lógica
            // Por ahora, asumo que el primer héroe que encontramos es el local
            return true;
        }
        
        #endregion
        
        #region Input Handling
        
        private void HandleInput()
        {
            // Handle zooming
            HandleZoomInput();
            
            // Handle camera movement
            if (!lockCameraToHero)
            {
                // Edge panning
                if (useEdgePanning)
                {
                    HandleEdgePanning();
                }
                
                // Dragging with middle mouse button
                if (useDragging)
                {
                    HandleDragging();
                }
                
                // Keyboard panning
                HandleKeyboardInput();
            }
            
            // Handle camera lock toggle
            if (Input.GetKeyDown(KeyCode.Space) && targetHero != null)
            {
                lockCameraToHero = !lockCameraToHero;
                
                if (lockCameraToHero)
                {
                    // Snap to hero immediately when locking
                    targetPosition = targetHero.transform.position;
                }
            }
        }
        
        private void HandleZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            if (scroll != 0f)
            {
                // Zoom in/out
                currentZoom = Mathf.Clamp(currentZoom - scroll * zoomSpeed, zoomRange.x, zoomRange.y);
            }
        }
        
        private void HandleEdgePanning()
        {
            Vector3 moveDirection = Vector3.zero;
            
            // Check screen edges for panning
            if (Input.mousePosition.x <= panBorderThickness)
            {
                moveDirection.x -= 1f;
            }
            else if (Input.mousePosition.x >= Screen.width - panBorderThickness)
            {
                moveDirection.x += 1f;
            }
            
            if (Input.mousePosition.y <= panBorderThickness)
            {
                moveDirection.z -= 1f;
            }
            else if (Input.mousePosition.y >= Screen.height - panBorderThickness)
            {
                moveDirection.z += 1f;
            }
            
            // Normalize to prevent faster diagonal movement
            if (moveDirection.magnitude > 0.1f)
            {
                moveDirection.Normalize();
                targetPosition += moveDirection * panSpeed * currentZoom * Time.deltaTime;
            }
        }
        
        private void HandleDragging()
        {
            // Middle mouse button dragging
            if (Input.GetMouseButtonDown(2))
            {
                isDragging = true;
                dragOrigin = Input.mousePosition;
                dragStartPosition = targetPosition;
                Cursor.visible = false;
            }
            
            if (Input.GetMouseButtonUp(2))
            {
                isDragging = false;
                Cursor.visible = true;
            }
            
            if (isDragging)
            {
                Vector3 dragDelta = Input.mousePosition - dragOrigin;
                
                // Calculate movement based on screen drag
                float dragSpeed = panSpeed * 0.02f * currentZoom;
                targetPosition = dragStartPosition - new Vector3(
                    dragDelta.x * dragSpeed,
                    0f,
                    dragDelta.y * dragSpeed
                );
            }
        }
        
        private void HandleKeyboardInput()
        {
            Vector3 moveDirection = Vector3.zero;
            
            // WASD keys for panning
            if (Input.GetKey(KeyCode.A))
            {
                moveDirection.x -= 1f;
            }
            
            if (Input.GetKey(KeyCode.D))
            {
                moveDirection.x += 1f;
            }
            
            if (Input.GetKey(KeyCode.W))
            {
                moveDirection.z += 1f;
            }
            
            if (Input.GetKey(KeyCode.S))
            {
                moveDirection.z -= 1f;
            }
            
            // Apply movement
            if (moveDirection.magnitude > 0.1f)
            {
                moveDirection.Normalize();
                targetPosition += moveDirection * panSpeed * Time.deltaTime;
            }
        }
        
        #endregion
        
        #region Camera Positioning
        
        private void ClampTargetPosition()
        {
            // Clamp x and z coordinates within map bounds
            targetPosition.x = Mathf.Clamp(targetPosition.x, xBounds.x, xBounds.y);
            targetPosition.z = Mathf.Clamp(targetPosition.z, zBounds.x, zBounds.y);
        }
        
        private void UpdateCameraTransform()
        {
            // Calculate camera position based on target position, height, distance, and angle
            Vector3 targetCamPos = targetPosition;
            targetCamPos.y = 0; // Ensure target is at ground level
            
            // Move back and up based on current zoom level
            Vector3 offset = Quaternion.Euler(angle, 0, 0) * Vector3.back * currentZoom;
            Vector3 newPosition = targetCamPos + offset;
            
            // Smoothly move the camera
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, newPosition, smoothSpeed * Time.deltaTime);
            
            // Set rotation to look down at angle
            cameraTransform.rotation = Quaternion.Euler(angle, 0, 0);
        }
        
        #endregion
        
        #region Public Methods
        
        public void FocusOnPosition(Vector3 position)
        {
            lockCameraToHero = false;
            targetPosition = position;
        }
        
        public void FocusOnHero()
        {
            if (targetHero != null)
            {
                targetPosition = targetHero.transform.position;
                lockCameraToHero = true;
            }
        }
        
        public void SetZoom(float zoom)
        {
            currentZoom = Mathf.Clamp(zoom, zoomRange.x, zoomRange.y);
        }
        
        #endregion
    }
}