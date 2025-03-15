using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using EpochLegends.Core.Hero;

namespace EpochLegends.Core.Player.Controller
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Control Settings")]
        [SerializeField] private float clickMovementThreshold = 0.5f;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask targetableLayer;
        [SerializeField] private KeyCode[] abilityKeys = new KeyCode[] { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
        
        [Header("References")]
        [SerializeField] private GameObject selectionIndicatorPrefab;
        
        // References - using fully qualified type names to avoid confusion with namespaces
        private EpochLegends.Core.Hero.Hero controlledHero;
        private Camera mainCamera;
        private GameObject selectionIndicator;
        
        // State tracking
        private bool isMovementPressed = false;
        private GameObject currentTargetObject = null;
        private bool isAbilityTargeting = false;
        private int currentTargetingAbilityIndex = -1;
        
        // Properties
        public EpochLegends.Core.Hero.Hero ControlledHero => controlledHero;
        
        #region Unity Lifecycle
        
        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            
            // Only setup input handling on the owner's client
            if (hasAuthority)
            {
                mainCamera = Camera.main;
                
                // Create selection indicator if prefab is assigned
                if (selectionIndicatorPrefab != null)
                {
                    selectionIndicator = Instantiate(selectionIndicatorPrefab);
                    selectionIndicator.SetActive(false);
                }
                
                // Register with player manager if needed
                // PlayerManager.Instance.RegisterLocalPlayer(this);
                
                Debug.Log("Player controller initialized with authority");
            }
        }
        
        private void Update()
        {
            // Only process input on the owner's client
            if (!hasAuthority) return;
            
            // If we don't have a hero to control yet, try to find one
            if (controlledHero == null)
            {
                FindControlledHero();
                return;
            }
            
            // Process player input
            ProcessMovementInput();
            ProcessAbilityInput();
            ProcessTargetingInput();
            ProcessUtilityInput();
        }
        
        private void OnDestroy()
        {
            if (selectionIndicator != null)
            {
                Destroy(selectionIndicator);
            }
        }
        
        #endregion
        
        #region Input Processing
        
        private void ProcessMovementInput()
        {
            // Right-click for movement
            if (Input.GetMouseButtonDown(1))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return; // Ignore clicks on UI
                
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, 100f, groundLayer))
                {
                    // Move to position
                    MoveToPosition(hit.point);
                    
                    // Show click indicator
                    if (selectionIndicator != null)
                    {
                        selectionIndicator.transform.position = hit.point;
                        selectionIndicator.SetActive(true);
                        
                        // Hide after a short time
                        Invoke(nameof(HideSelectionIndicator), 0.5f);
                    }
                    
                    isMovementPressed = true;
                    
                    // Cancel ability targeting if active
                    if (isAbilityTargeting)
                    {
                        CancelAbilityTargeting();
                    }
                }
            }
            
            if (Input.GetMouseButtonUp(1))
            {
                isMovementPressed = false;
            }
            
            // If continuously holding right-click, update destination
            if (isMovementPressed && Input.GetMouseButton(1))
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, 100f, groundLayer))
                {
                    // Only update if moved beyond threshold
                    if (Vector3.Distance(controlledHero.transform.position, hit.point) > clickMovementThreshold)
                    {
                        MoveToPosition(hit.point);
                    }
                }
            }
        }
        
        private void ProcessAbilityInput()
        {
            // Ability activation via hotkeys
            for (int i = 0; i < abilityKeys.Length; i++)
            {
                if (Input.GetKeyDown(abilityKeys[i]))
                {
                    TryActivateAbility(i);
                }
            }
            
            // Left click to confirm targeted ability
            if (isAbilityTargeting && Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return; // Ignore clicks on UI
                
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, 100f))
                {
                    // Use ability at target position or on target object
                    UseTargetedAbility(hit.point, hit.collider.gameObject);
                    
                    // Cancel targeting mode
                    CancelAbilityTargeting();
                }
            }
            
            // Right click or escape to cancel targeting
            if (isAbilityTargeting && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                CancelAbilityTargeting();
            }
        }
        
        private void ProcessTargetingInput()
        {
            // Handle highlighting of potential targets, etc.
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return; // Ignore clicks on UI
                
                // Only process targeting clicks when not in ability targeting mode
                if (!isAbilityTargeting)
                {
                    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    
                    if (Physics.Raycast(ray, out hit, 100f, targetableLayer))
                    {
                        // Set new target
                        SetTarget(hit.collider.gameObject);
                    }
                    else
                    {
                        // Clear target if click missed
                        ClearTarget();
                    }
                }
            }
        }
        
        private void ProcessUtilityInput()
        {
            // Handle other player inputs like emotes, pings, etc.
            // For example, Alt+Click for pinging
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, 100f))
                {
                    // Trigger ping at location
                    PingLocation(hit.point);
                }
            }
        }
        
        #endregion
        
        #region Movement & Navigation
        
        private void MoveToPosition(Vector3 position)
        {
            if (controlledHero == null || !controlledHero.isActiveAndEnabled) return;
            
            HeroMovement movement = controlledHero.Movement;
            if (movement != null)
            {
                // Call the Command in HeroMovement to move to position
                movement.CmdMoveToPosition(position);
            }
        }
        
        private void StopMovement()
        {
            if (controlledHero == null) return;
            
            HeroMovement movement = controlledHero.Movement;
            if (movement != null)
            {
                movement.CmdStopMovement();
            }
        }
        
        private void HideSelectionIndicator()
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }
        }
        
        #endregion
        
        #region Ability Handling
        
        private void TryActivateAbility(int abilityIndex)
        {
            if (controlledHero == null) return;
            
            // Get ability targeting type (would come from ability definition)
            // This is a placeholder - in a real implementation you'd get this from the hero's abilities
            var targetingType = GetAbilityTargetingType(abilityIndex);
            
            switch (targetingType)
            {
                case AbilityTargetingType.None:
                case AbilityTargetingType.Self:
                    // Immediate cast abilities
                    CmdUseAbility(abilityIndex, controlledHero.transform.position, null);
                    break;
                    
                case AbilityTargetingType.Target:
                    // Start targeting mode for target-based abilities
                    StartAbilityTargeting(abilityIndex, AbilityTargetingType.Target);
                    break;
                    
                case AbilityTargetingType.Direction:
                case AbilityTargetingType.Location:
                    // Start targeting mode for positional abilities
                    StartAbilityTargeting(abilityIndex, targetingType);
                    break;
            }
        }
        
        private void StartAbilityTargeting(int abilityIndex, AbilityTargetingType targetingType)
        {
            isAbilityTargeting = true;
            currentTargetingAbilityIndex = abilityIndex;
            
            // Activate targeting UI and indicators
            // For example, show range indicator, area effect preview, etc.
            
            Debug.Log($"Started targeting for ability {abilityIndex}, type: {targetingType}");
        }
        
        private void UseTargetedAbility(Vector3 position, GameObject targetObject)
        {
            if (controlledHero == null || !isAbilityTargeting) return;
            
            // Send command to use ability
            CmdUseAbility(currentTargetingAbilityIndex, position, targetObject);
        }
        
        private void CancelAbilityTargeting()
        {
            if (!isAbilityTargeting) return;
            
            isAbilityTargeting = false;
            currentTargetingAbilityIndex = -1;
            
            // Hide targeting UI and indicators
            
            Debug.Log("Cancelled ability targeting");
        }
        
        [Command]
        private void CmdUseAbility(int abilityIndex, Vector3 targetPosition, GameObject targetObject)
        {
            if (controlledHero == null) return;
            
            // Call ability use on the server
            bool success = controlledHero.UseAbility(abilityIndex, targetPosition, targetObject);
            
            Debug.Log($"Ability {abilityIndex} use attempt - Success: {success}");
        }
        
        // This is a placeholder - in a real implementation this would get targeting type from the hero's ability
        private AbilityTargetingType GetAbilityTargetingType(int abilityIndex)
        {
            // Placeholder implementation - would normally query the ability
            switch (abilityIndex)
            {
                case 0: // Q
                    return AbilityTargetingType.Direction;
                case 1: // W
                    return AbilityTargetingType.Target;
                case 2: // E
                    return AbilityTargetingType.Location;
                case 3: // R
                    return AbilityTargetingType.Self;
                default:
                    return AbilityTargetingType.None;
            }
        }
        
        #endregion
        
        #region Targeting
        
        private void SetTarget(GameObject target)
        {
            if (target == currentTargetObject) return;
            
            ClearTarget();
            
            currentTargetObject = target;
            
            // Highlight the new target
            // This would typically add a selection ring or effect
            
            // Notify UI or other systems about target change
            
            Debug.Log($"New target selected: {target.name}");
        }
        
        private void ClearTarget()
        {
            if (currentTargetObject == null) return;
            
            // Remove highlight from old target
            
            currentTargetObject = null;
            
            // Notify UI or other systems about target cleared
            
            Debug.Log("Target cleared");
        }
        
        #endregion
        
        #region Utility Functions
        
        private void FindControlledHero()
        {
            // Find the hero that this player should control
            // This could be based on player ID, connection ID, etc.
            
            // For simplicity in this example, we'll just find any hero with matching owner ID
            EpochLegends.Core.Hero.Hero[] heroes = FindObjectsOfType<EpochLegends.Core.Hero.Hero>();
            foreach (var hero in heroes)
            {
                NetworkIdentity heroNetId = hero.GetComponent<NetworkIdentity>();
                if (heroNetId != null && heroNetId.connectionToClient != null 
                    && heroNetId.connectionToClient == connectionToClient)
                {
                    controlledHero = hero;
                    Debug.Log($"Found controlled hero: {hero.name}");
                    break;
                }
            }
        }
        
        [Command]
        private void CmdAssignHero(uint heroNetId)
        {
            // Server-side hero assignment
            GameObject heroObj = NetworkIdentity.spawned[heroNetId].gameObject;
            if (heroObj != null)
            {
                EpochLegends.Core.Hero.Hero hero = heroObj.GetComponent<EpochLegends.Core.Hero.Hero>();
                if (hero != null)
                {
                    // Assign hero to this player
                    NetworkIdentity heroIdentity = hero.GetComponent<NetworkIdentity>();
                    heroIdentity.AssignClientAuthority(connectionToClient);
                    
                    // Notify the client
                    TargetHeroAssigned(heroNetId);
                }
            }
        }
        
        [TargetRpc]
        private void TargetHeroAssigned(uint heroNetId)
        {
            // Client callback when hero is assigned
            GameObject heroObj = NetworkIdentity.spawned[heroNetId].gameObject;
            if (heroObj != null)
            {
                controlledHero = heroObj.GetComponent<EpochLegends.Core.Hero.Hero>();
                Debug.Log($"Hero assigned: {controlledHero.name}");
            }
        }
        
        private void PingLocation(Vector3 position)
        {
            // Send ping command to server
            CmdPingLocation(position);
        }
        
        [Command]
        private void CmdPingLocation(Vector3 position)
        {
            // Server implementation of ping
            // In a real game, this would notify teammates about the ping
            
            // Broadcast ping to all clients
            RpcShowPing(position);
        }
        
        [ClientRpc]
        private void RpcShowPing(Vector3 position)
        {
            // Client-side display of ping
            Debug.Log($"Ping at position: {position}");
            
            // In a real implementation, this would create a visual ping indicator
            // and possibly play a sound
        }
        
        #endregion
    }
    
    // Enum to define different targeting types for abilities
    public enum AbilityTargetingType
    {
        None,       // No targeting required
        Self,       // Targets self automatically
        Target,     // Requires target selection
        Direction,  // Requires directional input
        Location    // Requires position selection
    }
}