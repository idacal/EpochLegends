using UnityEngine;
using Mirror;
using System.Collections.Generic;
using EpochLegends.Core.Ability;
using EpochLegends.Core.Combat;

namespace EpochLegends.Core.Hero
{
    public class Hero : NetworkBehaviour
    {
        [Header("Hero Configuration")]
        [SerializeField] private string heroDefinitionId;
        
        [Header("Components")]
        [SerializeField] private HeroStats heroStats;
        [SerializeField] private HeroMovement heroMovement;
        [SerializeField] private Animator animator;
        
        // Synchronized properties
        [SyncVar(hook = nameof(OnLevelChanged))]
        private int level = 1;
        
        [SyncVar(hook = nameof(OnCurrentHealthChanged))]
        private float currentHealth;
        
        [SyncVar(hook = nameof(OnCurrentManaChanged))]
        private float currentMana;
        
        [SyncVar(hook = nameof(OnTeamIdChanged))]
        private int teamId;
        
        // Non-serialized cached values
        private HeroDefinition heroDefinition;
        private List<BaseAbility> abilities = new List<BaseAbility>();
        private bool isAlive = true;
        
        // Properties
        public HeroDefinition HeroDefinition => heroDefinition;
        public HeroStats Stats => heroStats;
        public HeroMovement Movement => heroMovement;
        public int Level => level;
        public float CurrentHealth => currentHealth;
        public float CurrentMana => currentMana;
        public int TeamId => teamId;
        public bool IsAlive => isAlive;
        public List<BaseAbility> Abilities => abilities;
        
        // Events
        public delegate void HeroEvent(Hero hero);
        public event HeroEvent OnHeroDeath;
        public event HeroEvent OnHeroRespawn;
        public event HeroEvent OnHeroLevelUp;
        
        #region Lifecycle Methods
        
        private void Awake()
        {
            // Initialize components if not set in inspector
            if (heroStats == null)
                heroStats = GetComponent<HeroStats>();
                
            if (heroMovement == null)
                heroMovement = GetComponent<HeroMovement>();
                
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Load hero definition
            LoadHeroDefinition();
            
            // Initialize hero
            InitializeHero();
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Load hero definition on client
            LoadHeroDefinition();
            
            // Apply visual aspects of the hero
            ApplyHeroVisuals();
        }
        
        private void Update()
        {
            if (!isAlive || !isServer) return;
            
            // Server-side hero update logic
            if (heroStats != null)
            {
                // Apply regeneration
                RegenerateResources(Time.deltaTime);
                
                // Update abilities cooldowns
                UpdateAbilities(Time.deltaTime);
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void LoadHeroDefinition()
        {
            if (string.IsNullOrEmpty(heroDefinitionId))
            {
                Debug.LogError("Hero Definition ID is not set!");
                return;
            }
            
            // In a real implementation, this would load from a registry or resource system
            // For now, we'll use Resources.Load as a placeholder
            heroDefinition = Resources.Load<HeroDefinition>($"ScriptableObjects/Heroes/{heroDefinitionId}");
            
            if (heroDefinition == null)
            {
                Debug.LogError($"Failed to load Hero Definition with ID: {heroDefinitionId}");
            }
        }
        
        private void InitializeHero()
        {
            if (heroDefinition == null) return;
            
            // Initialize stats based on hero definition
            heroStats.Initialize(heroDefinition, level);
            
            // Set initial resources
            currentHealth = heroStats.MaxHealth;
            currentMana = heroStats.MaxMana;
            
            // Initialize abilities
            InitializeAbilities();
            
            // Set initial state
            isAlive = true;
        }
        
        private void InitializeAbilities()
        {
            abilities.Clear();
            
            if (heroDefinition == null || heroDefinition.Abilities == null) return;
            
            // Create ability instances from definitions
            foreach (var abilityDef in heroDefinition.Abilities)
            {
                // In a real implementation, this would use a factory or more sophisticated instantiation
                // For now, we'll create a placeholder instance
                BaseAbility ability = new BaseAbility(abilityDef, this);
                abilities.Add(ability);
            }
        }
        
        private void ApplyHeroVisuals()
        {
            if (heroDefinition == null) return;
            
            // Apply visual elements from the hero definition
            // This might include setting materials, swapping models, etc.
            // Implementation will depend on your specific visual setup
        }
        
        #endregion
        
        #region Combat & Resources
        
        private void RegenerateResources(float deltaTime)
        {
            // Health regeneration
            if (currentHealth < heroStats.MaxHealth)
            {
                float healthRegen = heroStats.HealthRegeneration * deltaTime;
                SetCurrentHealth(currentHealth + healthRegen);
            }
            
            // Mana regeneration
            if (currentMana < heroStats.MaxMana)
            {
                float manaRegen = heroStats.ManaRegeneration * deltaTime;
                SetCurrentMana(currentMana + manaRegen);
            }
        }
        
        public void TakeDamage(float amount, Hero attacker = null)
        {
            if (!isServer || !isAlive) return;
            
            // Apply damage reduction from stats
            float mitigatedDamage = heroStats.CalculateDamageTaken(amount);
            
            // Apply damage
            SetCurrentHealth(currentHealth - mitigatedDamage);
            
            // Check for death
            if (currentHealth <= 0 && isAlive)
            {
                Die(attacker);
            }
            
            // Trigger animation
            if (animator != null)
            {
                // RpcPlayHitAnimation could be a ClientRpc method to play hit animation
                RpcPlayHitAnimation();
            }
        }
        
        [ClientRpc]
        private void RpcPlayHitAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger("Hit");
            }
        }
        
        public void Heal(float amount)
        {
            if (!isServer || !isAlive) return;
            
            SetCurrentHealth(Mathf.Min(currentHealth + amount, heroStats.MaxHealth));
        }
        
        public bool UseMana(float amount)
        {
            if (!isServer || !isAlive) return false;
            
            if (currentMana >= amount)
            {
                SetCurrentMana(currentMana - amount);
                return true;
            }
            
            return false;
        }
        
        private void Die(Hero killer = null)
        {
            if (!isServer || !isAlive) return;
            
            isAlive = false;
            
            // Stop movement and abilities
            heroMovement.StopMovement();
            
            // Trigger death animation
            RpcPlayDeathAnimation();
            
            // Invoke death event
            OnHeroDeath?.Invoke(this);
            
            // Note: In a real implementation, you'd initiate respawn timer here
            // For simplicity, we'll use a fixed respawn time
            Invoke(nameof(Respawn), 5f);
        }
        
        [ClientRpc]
        private void RpcPlayDeathAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger("Death");
            }
        }
        
        private void Respawn()
        {
            if (!isServer) return;
            
            // Reset resources
            SetCurrentHealth(heroStats.MaxHealth);
            SetCurrentMana(heroStats.MaxMana);
            
            // Reset state
            isAlive = true;
            
            // Play respawn animation/effects
            RpcPlayRespawnAnimation();
            
            // Invoke respawn event
            OnHeroRespawn?.Invoke(this);
            
            // In a real implementation, you'd also teleport to spawn point
            // For now, we'll use a placeholder position 
            // Would normally use RespawnSystem for this
            transform.position = Vector3.zero;
        }
        
        [ClientRpc]
        private void RpcPlayRespawnAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger("Respawn");
            }
        }
        
        #endregion
        
        #region Abilities
        
        private void UpdateAbilities(float deltaTime)
        {
            foreach (var ability in abilities)
            {
                ability.UpdateCooldown(deltaTime);
            }
        }
        
        public bool UseAbility(int abilityIndex, Vector3 targetPosition = default, GameObject targetObject = null)
        {
            if (!isServer || !isAlive || abilityIndex < 0 || abilityIndex >= abilities.Count)
                return false;
                
            BaseAbility ability = abilities[abilityIndex];
            
            // Check if ability can be used
            if (!ability.CanUse())
                return false;
                
            // Try to use the ability
            return ability.Use(targetPosition, targetObject);
        }
        
        #endregion
        
        #region Level & Progression
        
        public void GainExperience(float xpAmount)
        {
            if (!isServer) return;
            
            // In a real implementation, this would calculate if level up is needed
            // and increase level appropriately
            // For simplicity, we'll just provide a direct method to level up
        }
        
        public void LevelUp()
        {
            if (!isServer) return;
            
            level++;
            
            // Update stats for new level
            heroStats.UpdateForLevel(level);
            
            // Reset resources to max when leveling up
            SetCurrentHealth(heroStats.MaxHealth);
            SetCurrentMana(heroStats.MaxMana);
            
            // Update abilities for new level
            foreach (var ability in abilities)
            {
                ability.UpdateForLevel(level);
            }
            
            // Trigger level up effects
            RpcPlayLevelUpEffect();
            
            // Invoke level up event
            OnHeroLevelUp?.Invoke(this);
        }
        
        [ClientRpc]
        private void RpcPlayLevelUpEffect()
        {
            // Play level up effect or animation
            if (animator != null)
            {
                animator.SetTrigger("LevelUp");
            }
            
            // Could also instantiate particle effect here
        }
        
        #endregion
        
        #region Sync Var Hooks
        
        private void OnLevelChanged(int oldLevel, int newLevel)
        {
            // Called on clients when level syncs
            if (isServer) return; // Server already processed this
            
            // Update client-side logic for level changes
            if (newLevel > oldLevel && heroStats != null)
            {
                heroStats.UpdateForLevel(newLevel);
                
                // Update abilities for new level
                foreach (var ability in abilities)
                {
                    ability.UpdateForLevel(newLevel);
                }
            }
        }
        
        private void OnCurrentHealthChanged(float oldHealth, float newHealth)
        {
            // Called on clients when health syncs
            // Update UI or other client-side health representations
        }
        
        private void OnCurrentManaChanged(float oldMana, float newMana)
        {
            // Called on clients when mana syncs
            // Update UI or other client-side mana representations
        }
        
        private void OnTeamIdChanged(int oldTeamId, int newTeamId)
        {
            // Called on clients when team syncs
            // Update team indicators, colors, etc.
        }
        
        #endregion
        
        #region Server Methods
        
        [Server]
        public void SetTeamId(int newTeamId)
        {
            teamId = newTeamId;
        }
        
        [Server]
        private void SetCurrentHealth(float value)
        {
            currentHealth = Mathf.Clamp(value, 0, heroStats.MaxHealth);
        }
        
        [Server]
        private void SetCurrentMana(float value)
        {
            currentMana = Mathf.Clamp(value, 0, heroStats.MaxMana);
        }
        
        #endregion
    }
}