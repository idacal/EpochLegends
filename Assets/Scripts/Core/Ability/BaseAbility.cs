using UnityEngine;
using System.Collections;
using EpochLegends.Core.Hero;

namespace EpochLegends.Core.Ability
{
    public class BaseAbility
    {
        // References
        protected AbilityDefinition definition;
        protected Hero.Hero owner;
        
        // State tracking
        protected float currentCooldown = 0f;
        protected int currentLevel = 1;
        protected bool isActive = false;
        
        // Cached values
        protected float damage;
        protected float healing;
        protected float effectDuration;
        protected float manaCost;
        protected float cooldownDuration;
        
        // Properties
        public AbilityDefinition Definition => definition;
        public int Level => currentLevel;
        public bool IsOnCooldown => currentCooldown > 0f;
        public float CurrentCooldown => currentCooldown;
        public float MaxCooldown => cooldownDuration;
        public bool IsActive => isActive;
        
        // Events
        public delegate void AbilityEvent(BaseAbility ability);
        public event AbilityEvent OnAbilityUsed;
        public event AbilityEvent OnCooldownComplete;
        public event AbilityEvent OnAbilityLevelUp;
        
        // Constructor
        public BaseAbility(AbilityDefinition definition, Hero.Hero owner)
        {
            this.definition = definition;
            this.owner = owner;
            
            // Initialize with level 1 values
            UpdateForLevel(1);
        }
        
        // Update ability values for a specific level
        public virtual void UpdateForLevel(int level)
        {
            currentLevel = Mathf.Clamp(level, 1, 5); // Usually max 5 levels for abilities
            
            // Cache scaled values for current level
            damage = definition.GetDamageForLevel(currentLevel);
            healing = definition.GetHealingForLevel(currentLevel);
            effectDuration = definition.GetDurationForLevel(currentLevel);
            manaCost = definition.GetManaCostForLevel(currentLevel);
            cooldownDuration = definition.GetCooldownForLevel(currentLevel);
            
            // Raise event if this is not initial setup
            if (level > 1)
            {
                OnAbilityLevelUp?.Invoke(this);
            }
        }
        
        // Update cooldown timer
        public virtual void UpdateCooldown(float deltaTime)
        {
            if (currentCooldown > 0f)
            {
                currentCooldown -= deltaTime;
                
                // Check if cooldown just completed
                if (currentCooldown <= 0f)
                {
                    currentCooldown = 0f;
                    OnCooldownComplete?.Invoke(this);
                }
            }
        }
        
        // Check if the ability can be used
        public virtual bool CanUse()
        {
            // Check cooldown
            if (IsOnCooldown)
                return false;
                
            // Check if owner is valid and alive
            if (owner == null || !owner.IsAlive)
                return false;
                
            // Check mana cost
            if (owner.CurrentMana < manaCost)
                return false;
                
            // By default, active abilities can't be used while another is active
            if (isActive && definition.AbilityType == AbilityType.Active)
                return false;
                
            // Additional validation can be added in derived classes
            
            return true;
        }
        
        // Use the ability
        public virtual bool Use(Vector3 targetPosition = default, GameObject targetObject = null)
        {
            if (!CanUse())
                return false;
                
            // Consume mana
            if (!owner.UseMana(manaCost))
                return false;
                
            // Start cooldown
            StartCooldown();
            
            // Perform ability-specific activation
            bool success = ActivateAbility(targetPosition, targetObject);
            
            if (success)
            {
                // Play effects
                PlayAbilityEffects();
                
                // Notify listeners
                OnAbilityUsed?.Invoke(this);
            }
            
            return success;
        }
        
        // Activate the specific ability behavior (to be overridden)
        protected virtual bool ActivateAbility(Vector3 targetPosition, GameObject targetObject)
        {
            // Base implementation is a placeholder
            // Derived classes should override this with specific functionality
            
            Debug.Log($"Ability {definition.DisplayName} activated by {owner.name}");
            
            // Set ability as active if it has duration
            if (effectDuration > 0f)
            {
                isActive = true;
                
                // Start coroutine to deactivate after duration
                // In a real implementation, this would use a server coroutine handler
                // For this example, we'll just simulate the behavior
                SimulateDelayedDeactivation();
            }
            
            return true;
        }
        
        // Play visual and audio effects for ability activation
        protected virtual void PlayAbilityEffects()
        {
            // Spawn visual effect prefab if defined
            if (definition.VisualEffectPrefab != null)
            {
                // In a networked environment, this would be done with ClientRpc
                // For this example, we'll assume this happens server-side
                GameObject visualEffect = Object.Instantiate(
                    definition.VisualEffectPrefab,
                    owner.transform.position,
                    Quaternion.identity
                );
                
                // Destroy after duration
                Object.Destroy(visualEffect, effectDuration > 0f ? effectDuration : 2f);
            }
            
            // Play cast sound if defined
            if (definition.CastSound != null)
            {
                // In a networked environment, this would be done with ClientRpc
                // Would typically use an AudioManager instead of direct PlayOneShot
                AudioSource.PlayClipAtPoint(definition.CastSound, owner.transform.position);
            }
        }
        
        // Start the ability cooldown
        protected virtual void StartCooldown()
        {
            currentCooldown = cooldownDuration;
        }
        
        // Reset the ability cooldown (for special cases)
        public virtual void ResetCooldown()
        {
            currentCooldown = 0f;
            OnCooldownComplete?.Invoke(this);
        }
        
        // Deactivate the ability
        protected virtual void DeactivateAbility()
        {
            isActive = false;
        }
        
        // Simulate delayed deactivation (would be a coroutine in real implementation)
        private void SimulateDelayedDeactivation()
        {
            // In a real implementation, this would be a server-side coroutine
            // For this example, we'll just log the expected behavior
            Debug.Log($"Ability {definition.DisplayName} will deactivate after {effectDuration} seconds");
            
            // In actual implementation, you'd use a proper timing mechanism
            DeactivateAbility();
        }
        
        // Apply damage to a target
        protected virtual float ApplyDamage(GameObject target, float damageAmount)
        {
            if (target == null) return 0f;
            
            Hero.Hero targetHero = target.GetComponent<Hero.Hero>();
            if (targetHero != null)
            {
                targetHero.TakeDamage(damageAmount, owner);
                return damageAmount; // In a real implementation, would return actual damage dealt
            }
            
            // Handle non-hero targets if needed
            
            return 0f;
        }
        
        // Apply healing to a target
        protected virtual float ApplyHealing(GameObject target, float healAmount)
        {
            if (target == null) return 0f;
            
            Hero.Hero targetHero = target.GetComponent<Hero.Hero>();
            if (targetHero != null)
            {
                targetHero.Heal(healAmount);
                return healAmount; // In a real implementation, would return actual healing done
            }
            
            return 0f;
        }
        
        // Check if a target is valid for this ability
        protected virtual bool IsValidTarget(GameObject target)
        {
            if (target == null) return false;
            
            // Get target hero if applicable
            Hero.Hero targetHero = target.GetComponent<Hero.Hero>();
            
            // If no hero, check if target layer is in target layers
            if (targetHero == null)
            {
                return ((1 << target.layer) & definition.TargetLayers.value) != 0;
            }
            
            // For hero targets, check team relationship based on targeting type
            switch (definition.TargetingType)
            {
                case TargetingType.Self:
                    return targetHero == owner;
                    
                // Add cases for friendly/enemy targeting based on team relationships
                // This would require knowledge of team system
                
                default:
                    return true;
            }
        }
        
        // Check line of sight to target
        protected virtual bool HasLineOfSight(GameObject target)
        {
            if (!definition.RequiresLineOfSight) return true;
            if (target == null) return false;
            
            Vector3 directionToTarget = target.transform.position - owner.transform.position;
            float distance = directionToTarget.magnitude;
            
            RaycastHit hit;
            if (Physics.Raycast(
                owner.transform.position, 
                directionToTarget.normalized, 
                out hit, 
                distance,
                Physics.DefaultRaycastLayers, 
                QueryTriggerInteraction.Ignore))
            {
                // If we hit something that isn't the target, we don't have line of sight
                if (hit.transform.gameObject != target)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}