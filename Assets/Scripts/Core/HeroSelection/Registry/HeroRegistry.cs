using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Core.Hero.Definition;

namespace EpochLegends.Core.HeroSelection.Registry
{
    public class HeroRegistry : MonoBehaviour
    {
        [Header("Hero Configuration")]
        [SerializeField] private List<HeroDefinition> registeredHeroes = new List<HeroDefinition>();
        [SerializeField] private bool loadFromResources = true;
        [SerializeField] private string resourcePath = "ScriptableObjects/HeroDefinitions";
        
        // Cached hero registry
        private Dictionary<string, HeroDefinition> heroesById = new Dictionary<string, HeroDefinition>();
        private Dictionary<HeroArchetype, List<HeroDefinition>> heroesByArchetype = new Dictionary<HeroArchetype, List<HeroDefinition>>();
        
        // Singleton pattern
        public static HeroRegistry Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Keep this object between scenes if needed
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            
            // Initialize the registry
            InitializeRegistry();
        }
        
        private void InitializeRegistry()
        {
            heroesById.Clear();
            heroesByArchetype.Clear();
            
            // Initialize archetype dictionary
            foreach (HeroArchetype archetype in System.Enum.GetValues(typeof(HeroArchetype)))
            {
                heroesByArchetype[archetype] = new List<HeroDefinition>();
            }
            
            // Add heroes from serialized list
            foreach (var hero in registeredHeroes)
            {
                if (hero != null && !string.IsNullOrEmpty(hero.HeroId))
                {
                    AddHero(hero);
                }
            }
            
            // Load heroes from resources if enabled
            if (loadFromResources)
            {
                HeroDefinition[] resourceHeroes = Resources.LoadAll<HeroDefinition>(resourcePath);
                foreach (var hero in resourceHeroes)
                {
                    if (!heroesById.ContainsKey(hero.HeroId))
                    {
                        AddHero(hero);
                    }
                }
            }
            
            Debug.Log($"Hero Registry initialized with {heroesById.Count} heroes");
        }
        
        private void AddHero(HeroDefinition hero)
        {
            if (hero == null || string.IsNullOrEmpty(hero.HeroId))
                return;
                
            // Add to ID dictionary
            heroesById[hero.HeroId] = hero;
            
            // Add to archetype dictionary
            heroesByArchetype[hero.Archetype].Add(hero);
        }
        
        #region Hero Lookup
        
        public HeroDefinition GetHeroById(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
                return null;
                
            if (heroesById.TryGetValue(heroId, out HeroDefinition hero))
            {
                return hero;
            }
            
            Debug.LogWarning($"Hero not found with ID: {heroId}");
            return null;
        }
        
        public List<HeroDefinition> GetHeroesByArchetype(HeroArchetype archetype)
        {
            if (heroesByArchetype.TryGetValue(archetype, out List<HeroDefinition> heroes))
            {
                return new List<HeroDefinition>(heroes);
            }
            
            return new List<HeroDefinition>();
        }
        
        public List<HeroDefinition> GetAllHeroes()
        {
            return heroesById.Values.ToList();
        }
        
        public List<string> GetAllHeroIds()
        {
            return heroesById.Keys.ToList();
        }
        
        public bool DoesHeroExist(string heroId)
        {
            return heroesById.ContainsKey(heroId);
        }
        
        #endregion
        
        #region Hero Filtering
        
        public List<HeroDefinition> GetHeroesByFilter(System.Func<HeroDefinition, bool> filter)
        {
            if (filter == null)
                return GetAllHeroes();
                
            return heroesById.Values.Where(filter).ToList();
        }
        
        public List<HeroDefinition> SearchHeroes(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
                return GetAllHeroes();
                
            searchTerm = searchTerm.ToLower();
            
            return heroesById.Values.Where(h => 
                h.DisplayName.ToLower().Contains(searchTerm) || 
                h.Description.ToLower().Contains(searchTerm)
            ).ToList();
        }
        
        #endregion
        
        #region Hero Availability
        
        public bool IsHeroUnlocked(string heroId, string playerId)
        {
            // In a real implementation, this would check against player progression data
            // to see if the hero is unlocked for the specific player
            
            // For now, we'll just return true for all heroes
            return DoesHeroExist(heroId);
        }
        
        public List<HeroDefinition> GetUnlockedHeroes(string playerId)
        {
            // In a real implementation, this would filter heroes based on unlocks
            
            // For now, return all heroes
            return GetAllHeroes();
        }
        
        #endregion
        
        #region Editor Methods
        
        #if UNITY_EDITOR
        public void RefreshRegistry()
        {
            InitializeRegistry();
        }
        
        public void AddHeroToRegistry(HeroDefinition hero)
        {
            if (hero != null && !registeredHeroes.Contains(hero))
            {
                registeredHeroes.Add(hero);
                InitializeRegistry();
            }
        }
        #endif
        
        #endregion
    }
}