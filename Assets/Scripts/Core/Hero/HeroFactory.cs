using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;
using EpochLegends.Core.Ability;

namespace EpochLegends.Core.Hero
{
    public class HeroFactory : MonoBehaviour
    {
        [Header("Hero Configuration")]
        [SerializeField] private List<HeroDefinition> availableHeroes = new List<HeroDefinition>();
        [SerializeField] private GameObject defaultHeroPrefab;
        
        // Cached reference to hero registry
        private Dictionary<string, HeroDefinition> heroRegistry = new Dictionary<string, HeroDefinition>();
        private Dictionary<string, GameObject> heroPrototypes = new Dictionary<string, GameObject>();
        
        // Singleton pattern
        public static HeroFactory Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize hero registry
            InitializeHeroRegistry();
        }
        
        // Initialize the hero registry with available heroes
        private void InitializeHeroRegistry()
        {
            heroRegistry.Clear();
            heroPrototypes.Clear();
            
            // Add heroes from serialized list
            foreach (var hero in availableHeroes)
            {
                if (hero != null && !string.IsNullOrEmpty(hero.HeroId))
                {
                    heroRegistry[hero.HeroId] = hero;
                }
            }
            
            // Optionally load additional heroes from resources
            HeroDefinition[] resourceHeroes = Resources.LoadAll<HeroDefinition>("ScriptableObjects/Heroes");
            foreach (var hero in resourceHeroes)
            {
                if (!heroRegistry.ContainsKey(hero.HeroId))
                {
                    heroRegistry[hero.HeroId] = hero;
                }
            }
            
            Debug.Log($"Hero Registry initialized with {heroRegistry.Count} heroes");
        }
        
        // Get a hero definition by id
        public HeroDefinition GetHeroDefinition(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
                return null;
                
            if (heroRegistry.TryGetValue(heroId, out HeroDefinition definition))
            {
                return definition;
            }
            
            Debug.LogWarning($"Hero definition not found for ID: {heroId}");
            return null;
        }
        
        // Get all available hero definitions
        public List<HeroDefinition> GetAllHeroDefinitions()
        {
            return heroRegistry.Values.ToList();
        }
        
        // Create a hero instance based on hero definition
        public Hero CreateHeroInstance(string heroId, Vector3 position, Quaternion rotation, int teamId, NetworkConnection owner = null)
        {
            HeroDefinition heroDefinition = GetHeroDefinition(heroId);
            
            if (heroDefinition == null)
            {
                Debug.LogError($"Failed to create hero: Definition not found for ID {heroId}");
                return null;
            }
            
            // Determine which prefab to use
            GameObject heroPrefab = heroDefinition.HeroPrefab;
            if (heroPrefab == null)
            {
                heroPrefab = defaultHeroPrefab;
                
                if (heroPrefab == null)
                {
                    Debug.LogError("Failed to create hero: No prefab available");
                    return null;
                }
            }
            
            // Instantiate the hero prefab
            GameObject heroInstance;
            
            // Use appropriate network instantiation if connection is provided
            if (owner != null && NetworkServer.active)
            {
                heroInstance = Object.Instantiate(heroPrefab, position, rotation);
                
                // Spawn the hero on the network without owner initially
                NetworkServer.Spawn(heroInstance);
                
                // Now try to assign authority if the connection is a NetworkConnectionToClient
                NetworkIdentity netIdentity = heroInstance.GetComponent<NetworkIdentity>();
                if (netIdentity != null && owner is NetworkConnectionToClient clientConnection)
                {
                    netIdentity.AssignClientAuthority(clientConnection);
                }
                else
                {
                    Debug.LogWarning("Could not assign client authority - connection is not a NetworkConnectionToClient");
                }
            }
            else if (NetworkServer.active)
            {
                heroInstance = Object.Instantiate(heroPrefab, position, rotation);
                NetworkServer.Spawn(heroInstance);
            }
            else
            {
                // Local instantiation for non-networked scenarios
                heroInstance = Object.Instantiate(heroPrefab, position, rotation);
            }
            
            // Setup hero with definition
            Hero hero = heroInstance.GetComponent<Hero>();
            if (hero != null)
            {
                InitializeHero(hero, heroDefinition, teamId);
            }
            else
            {
                Debug.LogError("Failed to create hero: Prefab does not contain Hero component");
                Object.Destroy(heroInstance);
                return null;
            }
            
            // Notify game manager about hero creation
            EpochLegends.GameManager.Instance?.OnHeroCreated(hero);
            
            return hero;
        }
        
        // Initialize a hero with the specified definition
        private void InitializeHero(Hero hero, HeroDefinition definition, int teamId)
        {
            // Set hero properties
            hero.SetTeamId(teamId);
            
            // Additional initialization
            // This would normally include setting up abilities, configuring visuals, etc.
            SetupHeroAbilities(hero, definition);
            
            // Log creation
            Debug.Log($"Hero created: {definition.DisplayName} (Team {teamId})");
        }
        
        // Set up abilities for a hero
        private void SetupHeroAbilities(Hero hero, HeroDefinition definition)
        {
            // In a real implementation, this would create ability instances
            // and add them to the hero's ability collection
            // This is a placeholder for that process
            
            Debug.Log($"Setting up abilities for hero: {definition.DisplayName}");
            
            // Example of how ability setup might work:
            // foreach (var abilityDef in definition.Abilities)
            // {
            //     BaseAbility ability = new BaseAbility(abilityDef, hero);
            //     hero.AddAbility(ability);
            // }
        }
        
        // Additional methods for hero management could be added here:
        // - DestroyHero(Hero hero)
        // - GetHeroPrototype(string heroId) - for optimized instantiation
        // - ValidateHeroConfiguration(HeroDefinition definition) - error checking
    }
}