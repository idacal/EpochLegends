using UnityEngine;
using System.Collections.Generic;

namespace EpochLegends.Core.Combat
{
    // Este es un sistema básico para manejar el combate
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Método básico para calcular daño
        public float CalculateDamage(float baseDamage, float attackerPower, float targetResistance)
        {
            // Fórmula simple de daño
            float damage = baseDamage * (attackerPower / (attackerPower + targetResistance));
            return Mathf.Max(1, damage); // Mínimo 1 de daño
        }

        // Método para aplicar daño
        public void ApplyDamage(GameObject attacker, GameObject target, float amount, DamageType damageType)
        {
            if (target == null) return;

            // Buscar componente de héroe en el objetivo
            EpochLegends.Core.Hero.Hero targetHero = target.GetComponent<EpochLegends.Core.Hero.Hero>();
            if (targetHero != null)
            {
                // Aplicar daño al héroe
                targetHero.TakeDamage(amount, attacker?.GetComponent<EpochLegends.Core.Hero.Hero>());
                
                // Puedes implementar lógica adicional de combate aquí
                Debug.Log($"Daño aplicado: {amount} a {target.name}");
            }
        }

        // Método para curación
        public void ApplyHealing(GameObject healer, GameObject target, float amount)
        {
            if (target == null) return;

            // Buscar componente de héroe en el objetivo
            EpochLegends.Core.Hero.Hero targetHero = target.GetComponent<EpochLegends.Core.Hero.Hero>();
            if (targetHero != null)
            {
                // Aplicar curación al héroe
                targetHero.Heal(amount);
                
                Debug.Log($"Curación aplicada: {amount} a {target.name}");
            }
        }

        // Método para verificar si dos entidades son aliados
        public bool AreAllies(GameObject entity1, GameObject entity2)
        {
            if (entity1 == null || entity2 == null) return false;

            EpochLegends.Core.Hero.Hero hero1 = entity1.GetComponent<EpochLegends.Core.Hero.Hero>();
            EpochLegends.Core.Hero.Hero hero2 = entity2.GetComponent<EpochLegends.Core.Hero.Hero>();

            if (hero1 != null && hero2 != null)
            {
                return hero1.TeamId == hero2.TeamId;
            }

            return false;
        }

        // Método para verificar si dos entidades son enemigos
        public bool AreEnemies(GameObject entity1, GameObject entity2)
        {
            return !AreAllies(entity1, entity2);
        }
    }

    // Enumeración para tipos de daño
    public enum DamageType
    {
        Physical,
        Magical,
        True   // Daño verdadero que ignora resistencias
    }
}