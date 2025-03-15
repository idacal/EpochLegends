using UnityEngine;
using System.Collections.Generic;

namespace EpochLegends.Core.Ability
{
    public enum AbilityType
    {
        Active,
        Passive,
        Ultimate
    }

    public enum TargetingType
    {
        None,
        Self,
        Target,
        Direction,
        Area,
        Line
    }

    [CreateAssetMenu(fileName = "NewAbility", menuName = "Epoch Legends/Ability Definition")]
    public class AbilityDefinition : ScriptableObject
    {
        [Header("Ability Information")]
        [SerializeField] private string abilityId = "ability_id";
        [SerializeField] private string displayName = "Ability Name";
        [SerializeField] private AbilityType abilityType = AbilityType.Active;
        [SerializeField, TextArea] private string description = "Ability description";
        [SerializeField] private string tooltip = "Quick tooltip for UI";
        
        [Header("Visual")]
        [SerializeField] private Sprite abilityIcon;
        [SerializeField] private GameObject visualEffectPrefab;
        [SerializeField] private AudioClip castSound;
        
        [Header("Targeting")]
        [SerializeField] private TargetingType targetingType = TargetingType.None;
        [SerializeField] private float range = 5f;
        [SerializeField] private float areaRadius = 0f;
        [SerializeField] private LayerMask targetLayers;
        [SerializeField] private bool requiresLineOfSight = true;
        
        [Header("Casting")]
        [SerializeField] private float castTime = 0f;
        [SerializeField] private bool canBeInterrupted = true;
        [SerializeField] private float cooldown = 5f;
        [SerializeField] private float manaCost = 40f;
        
        [Header("Effects")]
        [SerializeField] private float baseDamage = 0f;
        [SerializeField] private float baseHealing = 0f;
        [SerializeField] private float damageScaling = 0.5f; // How much ability scales with stats
        [SerializeField] private float effectDuration = 0f;
        
        [Header("Level Scaling")]
        [SerializeField] private float damagePerLevel = 20f;
        [SerializeField] private float healingPerLevel = 0f;
        [SerializeField] private float durationPerLevel = 0f;
        [SerializeField] private float cooldownReductionPerLevel = 0.5f;
        [SerializeField] private float manaCostIncreasePerLevel = 5f;
        
        [Header("Components")]
        [SerializeField] private List<string> abilityComponentTypes = new List<string>();
        [SerializeField] private string abilityImplementationClass = "";
        
        // Properties
        public string AbilityId => abilityId;
        public string DisplayName => displayName;
        public AbilityType AbilityType => abilityType;
        public string Description => description;
        public string Tooltip => tooltip;
        public Sprite AbilityIcon => abilityIcon;
        public GameObject VisualEffectPrefab => visualEffectPrefab;
        public AudioClip CastSound => castSound;
        
        // Targeting Properties
        public TargetingType TargetingType => targetingType;
        public float Range => range;
        public float AreaRadius => areaRadius;
        public LayerMask TargetLayers => targetLayers;
        public bool RequiresLineOfSight => requiresLineOfSight;
        
        // Casting Properties
        public float CastTime => castTime;
        public bool CanBeInterrupted => canBeInterrupted;
        public float Cooldown => cooldown;
        public float ManaCost => manaCost;
        
        // Effect Properties
        public float BaseDamage => baseDamage;
        public float BaseHealing => baseHealing;
        public float DamageScaling => damageScaling;
        public float EffectDuration => effectDuration;
        
        // Level Scaling Properties
        public float DamagePerLevel => damagePerLevel;
        public float HealingPerLevel => healingPerLevel;
        public float DurationPerLevel => durationPerLevel;
        public float CooldownReductionPerLevel => cooldownReductionPerLevel;
        public float ManaCostIncreasePerLevel => manaCostIncreasePerLevel;
        
        // Component Properties
        public List<string> AbilityComponentTypes => abilityComponentTypes;
        public string AbilityImplementationClass => abilityImplementationClass;
        
        // Methods to calculate values at specific level
        public float GetDamageForLevel(int level)
        {
            return baseDamage + (damagePerLevel * (level - 1));
        }
        
        public float GetHealingForLevel(int level)
        {
            return baseHealing + (healingPerLevel * (level - 1));
        }
        
        public float GetDurationForLevel(int level)
        {
            return effectDuration + (durationPerLevel * (level - 1));
        }
        
        public float GetCooldownForLevel(int level)
        {
            float reduction = cooldownReductionPerLevel * (level - 1);
            return Mathf.Max(0.5f, cooldown - reduction);
        }
        
        public float GetManaCostForLevel(int level)
        {
            return manaCost + (manaCostIncreasePerLevel * (level - 1));
        }
        
        // Validation
        private void OnValidate()
        {
            // Ensure abilityId is unique and valid format
            if (string.IsNullOrEmpty(abilityId))
            {
                abilityId = System.Guid.NewGuid().ToString().Substring(0, 8);
            }
            
            // Validate ranges based on targeting type
            if (targetingType == TargetingType.None || targetingType == TargetingType.Self)
            {
                range = 0f;
            }
            else
            {
                range = Mathf.Max(0.1f, range);
            }
            
            // Area size only matters for area targeting
            if (targetingType == TargetingType.Area)
            {
                areaRadius = Mathf.Max(0.1f, areaRadius);
            }
            
            // Ensure all values are within reasonable limits
            castTime = Mathf.Max(0f, castTime);
            cooldown = Mathf.Max(0f, cooldown);
            manaCost = Mathf.Max(0f, manaCost);
            baseDamage = Mathf.Max(0f, baseDamage);
            baseHealing = Mathf.Max(0f, baseHealing);
            effectDuration = Mathf.Max(0f, effectDuration);
        }
    }
}