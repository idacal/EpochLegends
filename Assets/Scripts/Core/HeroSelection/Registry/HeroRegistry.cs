using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace EpochLegends.Core.HeroSelection.Registry
{
    public class HeroRegistry : MonoBehaviour
    {
        [Header("Hero Configuration")]
        [SerializeField] private List<Core.Hero.HeroDefinition> registeredHeroes = new List<Core.Hero.HeroDefinition>();
        [SerializeField] private bool loadFromResources = true;
        [SerializeField] private string resourcePath = "ScriptableObjects/HeroDefinitions";
        
        // Cached hero registry
        private Dictionary<string, Core.Hero.HeroDefinition> heroesById = new Dictionary<string, Core.Hero.HeroDefinition>();
        private Dictionary<Core.Hero.HeroArchetype, List<Core.Hero.HeroDefinition>> heroesByArchetype = new Dictionary<Core.Hero.HeroArchetype, List<Core.Hero.HeroDefinition>>();
        
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
            foreach (Core.Hero.HeroArchetype archetype in System.Enum.GetValues(typeof(Core.Hero.HeroArchetype)))
            {
                heroesByArchetype[archetype] = new List<Core.Hero.HeroDefinition>();
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
                Core.Hero.HeroDefinition[] resourceHeroes = Resources.LoadAll<Core.Hero.HeroDefinition>(resourcePath);
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
        
        private void AddHero(Core.Hero.HeroDefinition hero)
        {
            if (hero == null || string.IsNullOrEmpty(hero.HeroId))
                return;
                
            // Add to ID dictionary
            heroesById[hero.HeroId] = hero;
            
            // Add to archetype dictionary
            heroesByArchetype[hero.Archetype].Add(hero);
        }
        
        #region Hero Lookup
        
        public Core.Hero.HeroDefinition GetHeroById(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
                return null;
                
            if (heroesById.TryGetValue(heroId, out Core.Hero.HeroDefinition hero))
            {
                return hero;
            }
            
            Debug.LogWarning($"Hero not found with ID: {heroId}");
            return null;
        }
        
        public List<Core.Hero.HeroDefinition> GetHeroesByArchetype(Core.Hero.HeroArchetype archetype)
        {
            if (heroesByArchetype.TryGetValue(archetype, out List<Core.Hero.HeroDefinition> heroes))
            {
                return new List<Core.Hero.HeroDefinition>(heroes);
            }
            
            return new List<Core.Hero.HeroDefinition>();
        }
        
        public List<Core.Hero.HeroDefinition> GetAllHeroes()
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
        
        public List<Core.Hero.HeroDefinition> GetHeroesByFilter(System.Func<Core.Hero.HeroDefinition, bool> filter)
        {
            if (filter == null)
                return GetAllHeroes();
                
            return heroesById.Values.Where(filter).ToList();
        }
        
        public List<Core.Hero.HeroDefinition> SearchHeroes(string searchTerm)
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
        
        public List<Core.Hero.HeroDefinition> GetUnlockedHeroes(string playerId)
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
        
        public void AddHeroToRegistry(Core.Hero.HeroDefinition hero)
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