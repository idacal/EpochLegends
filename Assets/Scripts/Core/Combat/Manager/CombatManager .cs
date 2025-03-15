using UnityEngine;

namespace EpochLegends.Core.Combat.Manager
{
    public class CombatManager : MonoBehaviour
    {
        // Esta es solo una clase vacía para que exista el namespace
        public static CombatManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }
    }
}