using UnityEngine;
using System.Collections.Generic;
using EpochLegends.Core.Ability;
using EpochLegends.Core.Hero;
using TMPro;
using UnityEngine.UI;

namespace EpochLegends.Core.UI.Game
{
    /// <summary>
    /// Administra la UI de habilidades de manera dinámica para adaptarse a diferentes héroes
    /// </summary>
    public class AbilityUIManager : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private Transform abilitiesContainer;
        [SerializeField] private GameObject abilitySlotPrefab;
        [SerializeField] private float slotSpacing = 5f;
        [SerializeField] private Vector2 slotSize = new Vector2(60, 60);
        
        [Header("Mapeo de Teclas")]
        [SerializeField] private KeyCode[] defaultHotkeys = new KeyCode[] { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.D, KeyCode.F };
        
        [Header("Personalización")]
        [SerializeField] private bool useHorizontalLayout = true;
        [SerializeField] private int maxSlotsPerRow = 6;
        [SerializeField] private float rowSpacing = 5f;
        
        // Referencias a los slots creados
        private List<AbilitySlot> abilitySlots = new List<AbilitySlot>();
        
        // Referencia al héroe actual
        private Hero.Hero currentHero;
        
        /// <summary>
        /// Configura la UI de habilidades para un héroe específico
        /// </summary>
        public void SetupForHero(Hero.Hero hero)
        {
            if (hero == null)
            {
                Debug.LogError("AbilityUIManager: Intento de configurar UI para un héroe nulo");
                return;
            }
            
            // Limpiar primero cualquier configuración anterior
            ClearAbilitySlots();
            
            currentHero = hero;
            List<BaseAbility> abilities = hero.Abilities;
            
            if (abilities == null || abilities.Count == 0)
            {
                Debug.LogWarning($"AbilityUIManager: El héroe {hero.name} no tiene habilidades");
                return;
            }
            
            Debug.Log($"AbilityUIManager: Configurando UI para {abilities.Count} habilidades");
            
            // Crear slots para cada habilidad
            CreateAbilitySlots(abilities);
            
            // Suscribirse a eventos del héroe para mantener la UI actualizada
            hero.OnHeroLevelUp += OnHeroLevelUp;
        }
        
        /// <summary>
        /// Limpia todos los slots de habilidades
        /// </summary>
        public void ClearAbilitySlots()
        {
            // Eliminar suscripciones previas
            if (currentHero != null)
            {
                currentHero.OnHeroLevelUp -= OnHeroLevelUp;
                currentHero = null;
            }
            
            // Destruir slots existentes
            foreach (Transform child in abilitiesContainer)
            {
                Destroy(child.gameObject);
            }
            
            abilitySlots.Clear();
        }
        
        /// <summary>
        /// Crea slots de habilidades según la lista de habilidades proporcionada
        /// </summary>
        private void CreateAbilitySlots(List<BaseAbility> abilities)
        {
            if (abilitySlotPrefab == null || abilitiesContainer == null)
            {
                Debug.LogError("AbilityUIManager: Falta el prefab de slot o el contenedor");
                return;
            }
            
            // Configurar el diseño
            RectTransform containerRect = abilitiesContainer as RectTransform;
            if (containerRect != null)
            {
                if (useHorizontalLayout)
                {
                    // Configuración para layout horizontal
                    HorizontalLayoutGroup layoutGroup = abilitiesContainer.GetComponent<HorizontalLayoutGroup>();
                    if (layoutGroup == null)
                    {
                        layoutGroup = abilitiesContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                    }
                    
                    layoutGroup.spacing = slotSpacing;
                    layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                    layoutGroup.childForceExpandWidth = false;
                    layoutGroup.childForceExpandHeight = false;
                    
                    // Eliminar vertical layout si existe
                    VerticalLayoutGroup verticalLayout = abilitiesContainer.GetComponent<VerticalLayoutGroup>();
                    if (verticalLayout != null)
                    {
                        Destroy(verticalLayout);
                    }
                }
                else
                {
                    // Configuración para layout vertical
                    VerticalLayoutGroup layoutGroup = abilitiesContainer.GetComponent<VerticalLayoutGroup>();
                    if (layoutGroup == null)
                    {
                        layoutGroup = abilitiesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                    }
                    
                    layoutGroup.spacing = slotSpacing;
                    layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                    layoutGroup.childForceExpandWidth = false;
                    layoutGroup.childForceExpandHeight = false;
                    
                    // Eliminar horizontal layout si existe
                    HorizontalLayoutGroup horizontalLayout = abilitiesContainer.GetComponent<HorizontalLayoutGroup>();
                    if (horizontalLayout != null)
                    {
                        Destroy(horizontalLayout);
                    }
                }
            }
            
            // Crear slots para cada habilidad
            for (int i = 0; i < abilities.Count; i++)
            {
                GameObject slotObj = Instantiate(abilitySlotPrefab, abilitiesContainer);
                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                if (slotRect != null)
                {
                    slotRect.sizeDelta = slotSize;
                }
                
                AbilitySlot slot = slotObj.GetComponent<AbilitySlot>();
                if (slot == null)
                {
                    slot = slotObj.AddComponent<AbilitySlot>();
                }
                
                // Asignar hotkey si hay disponible
                string hotkeyLabel = "";
                if (i < defaultHotkeys.Length)
                {
                    hotkeyLabel = defaultHotkeys[i].ToString();
                }
                
                // Inicializar el slot
                slot.Initialize(hotkeyLabel);
                
                // Configurar la habilidad
                if (abilities[i] != null)
                {
                    slot.SetAbility(abilities[i]);
                }
                
                // Añadir tooltip si no existe
                AbilityTooltip tooltip = slotObj.GetComponent<AbilityTooltip>();
                if (tooltip == null)
                {
                    tooltip = slotObj.AddComponent<AbilityTooltip>();
                }
                
                if (tooltip != null && abilities[i] != null)
                {
                    tooltip.SetAbility(abilities[i]);
                }
                
                // Guardar referencia al slot
                abilitySlots.Add(slot);
                
                // Nombrar para depuración
                slotObj.name = $"AbilitySlot_{i}_{abilities[i]?.Definition?.DisplayName ?? "Empty"}";
            }
            
            Debug.Log($"AbilityUIManager: Creados {abilitySlots.Count} slots de habilidades");
        }
        
        /// <summary>
        /// Actualiza la UI de habilidades para reflejar cambios
        /// </summary>
        public void UpdateAbilitySlots()
        {
            if (currentHero == null || abilitySlots.Count == 0) return;
            
            List<BaseAbility> abilities = currentHero.Abilities;
            
            // Actualizar cada slot
            for (int i = 0; i < abilities.Count && i < abilitySlots.Count; i++)
            {
                if (abilities[i] != null)
                {
                    abilitySlots[i].UpdateCooldown(abilities[i]);
                    
                    // También actualizar tooltip si ha cambiado el nivel
                    AbilityTooltip tooltip = abilitySlots[i].GetComponent<AbilityTooltip>();
                    if (tooltip != null)
                    {
                        tooltip.UpdateTooltipContent();
                    }
                }
            }
        }
        
        /// <summary>
        /// Maneja el evento de subida de nivel del héroe
        /// </summary>
        private void OnHeroLevelUp(Hero.Hero hero)
        {
            // Actualizar UI de habilidades (pueden haber cambiado con el nivel)
            UpdateAbilitySlots();
        }
        
        private void Update()
        {
            // Actualizar cooldowns y estados en tiempo real
            if (currentHero != null)
            {
                UpdateAbilitySlots();
            }
        }
        
        private void OnDestroy()
        {
            // Limpiar suscripciones
            if (currentHero != null)
            {
                currentHero.OnHeroLevelUp -= OnHeroLevelUp;
            }
        }
    }
}