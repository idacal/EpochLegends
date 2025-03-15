using UnityEngine;
using System.Collections.Generic;
using EpochLegends.Core.Ability;

namespace EpochLegends.Core.Hero
{
    public enum HeroArchetype
    {
        Tank,
        Fighter,
        Assassin,
        Mage,
        Support,
        Marksman
    }

    [CreateAssetMenu(fileName = "NewHero", menuName = "Epoch Legends/Hero Definition")]
    public class HeroDefinition : ScriptableObject
    {
        [Header("Hero Information")]
        [SerializeField] private string heroId = "hero_id";
        [SerializeField] private string displayName = "Hero Name";
        [SerializeField] private HeroArchetype archetype = HeroArchetype.Fighter;
        [SerializeField, TextArea] private string description = "Hero description";
        
        [Header("Visual")]
        [SerializeField] private GameObject heroPrefab;
        [SerializeField] private Sprite heroPortrait;
        [SerializeField] private Sprite heroIcon;
        
        [Header("Base Stats")]
        [SerializeField] private float baseHealth = 100f;
        [SerializeField] private float baseMana = 100f;
        [SerializeField] private float baseAttackDamage = 10f;
        [SerializeField] private float baseAttackSpeed = 1f;
        [SerializeField] private float baseMovementSpeed = 5f;
        [SerializeField] private float baseHealthRegen = 1f;
        [SerializeField] private float baseManaRegen = 1f;
        
        [Header("Stat Growth")]
        [SerializeField] private float healthPerLevel = 10f;
        [SerializeField] private float manaPerLevel = 10f;
        [SerializeField] private float attackDamagePerLevel = 1f;
        [SerializeField] private float attackSpeedPerLevel = 0.02f;
        
        [Header("Abilities")]
        [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();
        
        // Properties
        public string HeroId => heroId;
        public string DisplayName => displayName;
        public HeroArchetype Archetype => archetype;
        public string Description => description;
        public GameObject HeroPrefab => heroPrefab;
        public Sprite HeroPortrait => heroPortrait;
        public Sprite HeroIcon => heroIcon;
        
        // Base Stats
        public float BaseHealth => baseHealth;
        public float BaseMana => baseMana;
        public float BaseAttackDamage => baseAttackDamage;
        public float BaseAttackSpeed => baseAttackSpeed;
        public float BaseMovementSpeed => baseMovementSpeed;
        public float BaseHealthRegen => baseHealthRegen;
        public float BaseManaRegen => baseManaRegen;
        
        // Stat Growth
        public float HealthPerLevel => healthPerLevel;
        public float ManaPerLevel => manaPerLevel;
        public float AttackDamagePerLevel => attackDamagePerLevel;
        public float AttackSpeedPerLevel => attackSpeedPerLevel;
        
        // Abilities
        public List<AbilityDefinition> Abilities => abilities;
        
        // Methods to calculate stats at specific level
        public float GetHealthForLevel(int level)
        {
            return baseHealth + (healthPerLevel * (level - 1));
        }
        
        public float GetManaForLevel(int level)
        {
            return baseMana + (manaPerLevel * (level - 1));
        }
        
        public float GetAttackDamageForLevel(int level)
        {
            return baseAttackDamage + (attackDamagePerLevel * (level - 1));
        }
        
        public float GetAttackSpeedForLevel(int level)
        {
            return baseAttackSpeed + (attackSpeedPerLevel * (level - 1));
        }
        
        // Validation
        private void OnValidate()
        {
            // Ensure heroId is unique and valid format
            if (string.IsNullOrEmpty(heroId))
            {
                heroId = System.Guid.NewGuid().ToString().Substring(0, 8);
            }
            
            // Ensure base stats are positive
            baseHealth = Mathf.Max(1f, baseHealth);
            baseMana = Mathf.Max(0f, baseMana);
            baseAttackDamage = Mathf.Max(0f, baseAttackDamage);
            baseAttackSpeed = Mathf.Max(0.1f, baseAttackSpeed);
            baseMovementSpeed = Mathf.Max(0.1f, baseMovementSpeed);
            baseHealthRegen = Mathf.Max(0f, baseHealthRegen);
            baseManaRegen = Mathf.Max(0f, baseManaRegen);
        }
    }
}