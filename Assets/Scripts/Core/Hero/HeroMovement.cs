using UnityEngine;
using UnityEngine.AI;
using Mirror;
using System.Collections;
using EpochLegends.Utils;

namespace EpochLegends.Core.Hero
{
    public enum MovementState
    {
        Idle,
        Walking,
        Running,
        Stunned,
        Rooted,
        Casting
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class HeroMovement : NetworkBehaviour
    {
        [Header("Movement Configuration")]
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float stoppingDistance = 0.1f;
        
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string walkParameterName = "IsWalking";
        [SerializeField] private string runParameterName = "IsRunning";
        
        // Movement state
        [SyncVar(hook = nameof(OnMovementStateChanged))]
        private MovementState currentState = MovementState.Idle;
        
        // Destination syncing
        [SyncVar]
        private Vector3 serverDestination;
        
        // Movement status
        private bool isMovementRequested = false;
        private Vector3 targetDestination;
        private NavMeshAgent navAgent;
        private HeroStats heroStats;
        private float baseSpeed;
        
        // Property accessors
        public MovementState CurrentState => currentState;
        public Vector3 CurrentDestination => navAgent.destination;
        public bool IsMoving => navAgent != null && navAgent.velocity.magnitude > 0.1f;
        
        #region Initialization
        
        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            
            if (navAgent == null)
            {
                Debug.LogError("NavMeshAgent component missing on HeroMovement!");
            }
            
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            
            heroStats = GetComponent<HeroStats>();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize server-side movement
            if (navAgent != null)
            {
                baseSpeed = navAgent.speed;
                UpdateMovementSpeed();
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Disable NavMeshAgent on non-authority clients to prevent conflicts
            if (!isLocalPlayer && navAgent != null)
            {
                navAgent.enabled = false;
            }
        }
        
        #endregion
        
        #region Update Loops
        
        private void Update()
        {
            if (isServer)
            {
                ServerMovementUpdate();
            }
            
            if (isClient && isLocalPlayer)
            {
                ClientMovementUpdate();
            }
            
            // Update animation states based on current velocity
            UpdateAnimationState();
        }
        
        private void ServerMovementUpdate()
        {
            if (navAgent == null || currentState == MovementState.Stunned) return;
            
            // Handle rooted state - can look but not move
            if (currentState == MovementState.Rooted)
            {
                navAgent.isStopped = true;
                return;
            }
            
            // Check if movement is allowed
            if (currentState == MovementState.Casting && !CanMoveWhileCasting())
            {
                navAgent.isStopped = true;
                return;
            }
            
            // Update agent speed based on hero stats
            UpdateMovementSpeed();
            
            // Update state based on movement
            if (navAgent.velocity.magnitude > 0.1f)
            {
                SetMovementState(navAgent.speed > baseSpeed ? MovementState.Running : MovementState.Walking);
            }
            else if (currentState != MovementState.Casting)
            {
                SetMovementState(MovementState.Idle);
            }
            
            // Handle pathfinding completion
            if (isMovementRequested && !navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                isMovementRequested = false;
                
                // Only set to idle if not in another overriding state
                if (currentState == MovementState.Walking || currentState == MovementState.Running)
                {
                    SetMovementState(MovementState.Idle);
                }
            }
        }
        
        private void ClientMovementUpdate()
        {
            // For client prediction, we could add client-side movement prediction here
            // This would be especially important in high-latency scenarios
        }
        
        private void UpdateAnimationState()
        {
            if (animator == null) return;
            
            // Update animation parameters based on movement state
            switch (currentState)
            {
                case MovementState.Walking:
                    animator.SetBool(walkParameterName, true);
                    animator.SetBool(runParameterName, false);
                    break;
                    
                case MovementState.Running:
                    animator.SetBool(walkParameterName, false);
                    animator.SetBool(runParameterName, true);
                    break;
                    
                default:
                    animator.SetBool(walkParameterName, false);
                    animator.SetBool(runParameterName, false);
                    break;
            }
        }
        
        #endregion
        
        #region Movement Commands
        
        [Command]
        public void CmdMoveToPosition(Vector3 position)
        {
            // Validate the target position if needed
            if (!IsValidDestination(position)) return;
            
            // Set movement target
            MoveToPosition(position);
            
            // Update synced destination 
            serverDestination = position;
        }
        
        [Server]
        public void MoveToPosition(Vector3 position)
        {
            // If stunned or otherwise immobilized, ignore movement requests
            if (currentState == MovementState.Stunned || currentState == MovementState.Rooted)
                return;
                
            // If casting and we can't move while casting, ignore movement
            if (currentState == MovementState.Casting && !CanMoveWhileCasting())
                return;
            
            if (navAgent == null || !navAgent.isOnNavMesh) return;
            
            targetDestination = position;
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
            
            isMovementRequested = true;
            
            // Update movement state
            SetMovementState(MovementState.Walking);
        }
        
        [Command]
        public void CmdStopMovement()
        {
            StopMovement();
        }
        
        [Server]
        public void StopMovement()
        {
            if (navAgent == null) return;
            
            isMovementRequested = false;
            navAgent.isStopped = true;
            
            // Only set to idle if not in another overriding state
            if (currentState == MovementState.Walking || currentState == MovementState.Running)
            {
                SetMovementState(MovementState.Idle);
            }
        }
        
        [Server]
        public void LookAt(Vector3 targetPosition)
        {
            Vector3 directionToTarget = targetPosition - transform.position;
            directionToTarget.y = 0;
            
            if (directionToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        [Server]
        public void SetMovementState(MovementState newState)
        {
            // Don't override stunned state except with intentional state changes
            if (currentState == MovementState.Stunned && newState != MovementState.Stunned) 
                return;
                
            currentState = newState;
            
            // Apply state-specific logic
            switch (newState)
            {
                case MovementState.Stunned:
                    navAgent.isStopped = true;
                    break;
                    
                case MovementState.Rooted:
                    navAgent.isStopped = true;
                    break;
                    
                case MovementState.Casting:
                    // If we can't move while casting, stop movement
                    if (!CanMoveWhileCasting())
                    {
                        navAgent.isStopped = true;
                    }
                    break;
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void UpdateMovementSpeed()
        {
            if (navAgent == null || heroStats == null) return;
            
            // Get movement speed from stats
            float movementSpeed = heroStats.MovementSpeed;
            
            // Apply state-specific modifiers
            switch (currentState)
            {
                case MovementState.Running:
                    // Running speed might be faster
                    movementSpeed *= 1.5f;
                    break;
                    
                case MovementState.Casting:
                    // Might move slower while casting
                    if (CanMoveWhileCasting())
                    {
                        movementSpeed *= 0.7f;
                    }
                    break;
            }
            
            // Apply the calculated speed
            navAgent.speed = movementSpeed;
        }
        
        private bool CanMoveWhileCasting()
        {
            // This would check if the current ability being cast allows movement
            // For now, we'll assume all casting prevents movement
            return false;
        }
        
        private bool IsValidDestination(Vector3 position)
        {
            // Check if the position is on the navmesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 1.0f, NavMesh.AllAreas))
            {
                return true;
            }
            
            return false;
        }
        
        // Called when the movement state syncs to clients
        private void OnMovementStateChanged(MovementState oldState, MovementState newState)
        {
            // Handle visual/audio effects for state transitions
            switch (newState)
            {
                case MovementState.Stunned:
                    // Play stunned effect/animation
                    break;
                    
                case MovementState.Rooted:
                    // Play rooted effect/animation
                    break;
            }
            
            // Update animation state
            UpdateAnimationState();
        }
        
        #endregion
        
        #region Status Effects
        
        [Server]
        public void ApplyStun(float duration)
        {
            SetMovementState(MovementState.Stunned);
            StartCoroutine(RemoveStatusAfterDuration(MovementState.Stunned, duration));
        }
        
        [Server]
        public void ApplyRoot(float duration)
        {
            SetMovementState(MovementState.Rooted);
            StartCoroutine(RemoveStatusAfterDuration(MovementState.Rooted, duration));
        }
        
        [Server]
        public void SetCastingState(bool isCasting)
        {
            if (isCasting)
            {
                SetMovementState(MovementState.Casting);
            }
            else if (currentState == MovementState.Casting)
            {
                // Return to appropriate state based on movement
                if (IsMoving)
                {
                    SetMovementState(MovementState.Walking);
                }
                else
                {
                    SetMovementState(MovementState.Idle);
                }
            }
        }
        
        private IEnumerator RemoveStatusAfterDuration(MovementState status, float duration)
        {
            yield return new WaitForSeconds(duration);
            
            // Only remove if we're still in the same status
            if (currentState == status)
            {
                // Return to appropriate state based on movement
                if (isMovementRequested && navAgent != null && !navAgent.isStopped)
                {
                    SetMovementState(MovementState.Walking);
                    navAgent.isStopped = false;
                }
                else
                {
                    SetMovementState(MovementState.Idle);
                }
            }
        }
        
        #endregion
    }
}