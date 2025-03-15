using UnityEngine;
using System.Collections.Generic;
using Mirror;

namespace EpochLegends.Core.Hero
{
    [System.Serializable]
    public class StatModifier
    {
        public enum ModifierType
        {
            Flat,           // Adds a flat value
            PercentAdd,     // Adds percentage (additive with other percent adds)
            PercentMultiply // Multiplies by percentage (multiplicative with other percent mults)
        }
        
        public float Value;
        public ModifierType Type;
        public int Priority;
        public object Source;
        
        public StatModifier(float value, ModifierType type, int priority, object source = null)
        {
            Value = value;
            Type = type;
            Priority = priority;
            Source = source;
        }
    }
    
    public class HeroStats : NetworkBehaviour
    {
        // Base stats (from HeroDefinition)
        private float baseHealth;
        private float baseMana;
        private float baseAttackDamage;
        private float baseAttackSpeed;
        private float baseMovementSpeed;
        private float baseHealthRegen;
        private float baseManaRegen;
        
        // Stat dictionaries
        private Dictionary<StatType, float> baseStats = new Dictionary<StatType, float>();
        private Dictionary<StatType, List<StatModifier>> statModifiers = new Dictionary<StatType, List<StatModifier>>();
        private Dictionary<StatType, float> calculatedStats = new Dictionary<StatType, float>();
        
        // Reference to hero definition and current level
        private HeroDefinition heroDefinition;
        private int currentLevel = 1;
        
        // Properties for final calculated stats
        public float MaxHealth => GetCalculatedStat(StatType.Health);
        public float MaxMana => GetCalculatedStat(StatType.Mana);
        public float AttackDamage => GetCalculatedStat(StatType.AttackDamage);
        public float AttackSpeed => GetCalculatedStat(StatType.AttackSpeed);
        public float MovementSpeed => GetCalculatedStat(StatType.MovementSpeed);
        public float HealthRegeneration => GetCalculatedStat(StatType.HealthRegen);
        public float ManaRegeneration => GetCalculatedStat(StatType.ManaRegen);
        public float PhysicalResistance => GetCalculatedStat(StatType.PhysicalResistance);
        public float MagicalResistance => GetCalculatedStat(StatType.MagicalResistance);
        public float CriticalChance => GetCalculatedStat(StatType.CriticalChance);
        public float CriticalDamage => GetCalculatedStat(StatType.CriticalDamage);
        
        // Initialize stats from hero definition
        public void Initialize(HeroDefinition definition, int level)
        {
            this.heroDefinition = definition;
            this.currentLevel = level;
            
            if (definition == null)
            {
                Debug.LogError("Cannot initialize HeroStats with null definition");
                return;
            }
            
            // Initialize base stat dictionaries
            InitializeStatDictionaries();
            
            // Set base values from hero definition for the current level
            SetBaseValuesForLevel(level);
            
            // Calculate all stats
            RecalculateAllStats();
        }
        
        // Update stats for a new level
        public void UpdateForLevel(int newLevel)
        {
            if (heroDefinition == null) return;
            
            currentLevel = newLevel;
            
            // Update base values for the new level
            SetBaseValuesForLevel(newLevel);
            
            // Recalculate all stats
            RecalculateAllStats();
        }
        
        // Initialize stat dictionaries
        private void InitializeStatDictionaries()
        {
            // Clear existing dictionaries
            baseStats.Clear();
            statModifiers.Clear();
            calculatedStats.Clear();
            
            // Initialize base stats with default values
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                baseStats[statType] = 0f;
                statModifiers[statType] = new List<StatModifier>();
                calculatedStats[statType] = 0f;
            }
        }
        
        // Set base values from hero definition for the given level
        private void SetBaseValuesForLevel(int level)
        {
            if (heroDefinition == null) return;
            
            // Set primary stats based on level
            baseStats[StatType.Health] = heroDefinition.GetHealthForLevel(level);
            baseStats[StatType.Mana] = heroDefinition.GetManaForLevel(level);
            baseStats[StatType.AttackDamage] = heroDefinition.GetAttackDamageForLevel(level);
            baseStats[StatType.AttackSpeed] = heroDefinition.GetAttackSpeedForLevel(level);
            baseStats[StatType.MovementSpeed] = heroDefinition.BaseMovementSpeed;
            baseStats[StatType.HealthRegen] = heroDefinition.BaseHealthRegen;
            baseStats[StatType.ManaRegen] = heroDefinition.BaseManaRegen;
            
            // Set default values for other stats
            baseStats[StatType.PhysicalResistance] = 0f;
            baseStats[StatType.MagicalResistance] = 0f;
            baseStats[StatType.CriticalChance] = 0.05f;  // 5% base crit chance
            baseStats[StatType.CriticalDamage] = 1.5f;   // 150% base crit damage
        }
        
        // Add a stat modifier
        public void AddModifier(StatType statType, StatModifier modifier)
        {
            if (!statModifiers.ContainsKey(statType))
            {
                statModifiers[statType] = new List<StatModifier>();
            }
            
            statModifiers[statType].Add(modifier);
            
            // Sort modifiers by priority
            statModifiers[statType].Sort((a, b) => a.Priority.CompareTo(b.Priority));
            
            // Recalculate the affected stat
            CalculateFinalStat(statType);
        }
        
        // Remove stat modifiers from a source
        public bool RemoveModifiersFromSource(object source)
        {
            bool didRemove = false;
            
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                if (statModifiers.ContainsKey(statType))
                {
                    List<StatModifier> modifiers = statModifiers[statType];
                    int removedCount = modifiers.RemoveAll(mod => mod.Source == source);
                    
                    if (removedCount > 0)
                    {
                        CalculateFinalStat(statType);
                        didRemove = true;
                    }
                }
            }
            
            return didRemove;
        }
        
        // Remove all modifiers
        public void RemoveAllModifiers()
        {
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                if (statModifiers.ContainsKey(statType))
                {
                    statModifiers[statType].Clear();
                    CalculateFinalStat(statType);
                }
            }
        }
        
        // Recalculate all stats
        private void RecalculateAllStats()
        {
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                CalculateFinalStat(statType);
            }
        }
        
        // Calculate the final value for a stat
        private void CalculateFinalStat(StatType statType)
        {
            float baseValue = baseStats.ContainsKey(statType) ? baseStats[statType] : 0f;
            float finalValue = baseValue;
            float sumPercentAdd = 0f;
            
            // Early exit if no modifiers
            if (!statModifiers.ContainsKey(statType) || statModifiers[statType].Count == 0)
            {
                calculatedStats[statType] = finalValue;
                return;
            }
            
            // Apply modifiers in order: Flat, PercentAdd (summed), then PercentMultiply
            List<StatModifier> modifiers = statModifiers[statType];
            
            // Apply flat modifiers
            foreach (StatModifier mod in modifiers)
            {
                if (mod.Type == StatModifier.ModifierType.Flat)
                {
                    finalValue += mod.Value;
                }
            }
            
            // Sum percent adds
            foreach (StatModifier mod in modifiers)
            {
                if (mod.Type == StatModifier.ModifierType.PercentAdd)
                {
                    sumPercentAdd += mod.Value;
                }
            }
            
            // Apply percent adds as a single operation
            finalValue *= 1f + sumPercentAdd;
            
            // Apply percent multipliers
            foreach (StatModifier mod in modifiers)
            {
                if (mod.Type == StatModifier.ModifierType.PercentMultiply)
                {
                    finalValue *= 1f + mod.Value;
                }
            }
            
            // Store the calculated value
            calculatedStats[statType] = finalValue;
        }
        
        // Get the final calculated stat value
        public float GetCalculatedStat(StatType statType)
        {
            if (calculatedStats.ContainsKey(statType))
            {
                return calculatedStats[statType];
            }
            
            return 0f;
        }
        
        // Calculate damage taken based on resistances
        public float CalculateDamageTaken(float incomingDamage, DamageType damageType = DamageType.Physical)
        {
            float resistance = 0f;
            
            switch (damageType)
            {
                case DamageType.Physical:
                    resistance = PhysicalResistance;
                    break;
                case DamageType.Magical:
                    resistance = MagicalResistance;
                    break;
                case DamageType.True:
                    return incomingDamage; // True damage ignores resistance
            }
            
            // Calculate damage reduction (simplified formula)
            // Example: 100 resistance = 50% reduction
            float reduction = resistance / (100f + resistance);
            float mitigatedDamage = incomingDamage * (1f - reduction);
            
            return mitigatedDamage;
        }
    }
    
    // Enum for stat types
    public enum StatType
    {
        Health,
        Mana,
        AttackDamage,
        AttackSpeed,
        MovementSpeed,
        HealthRegen,
        ManaRegen,
        PhysicalResistance,
        MagicalResistance,
        CriticalChance,
        CriticalDamage
        // Add other stats as needed
    }
    
    // Enum for damage types
    public enum DamageType
    {
        Physical,
        Magical,
        True
    }
}