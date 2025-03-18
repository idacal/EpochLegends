using UnityEngine;
using System.Collections.Generic;
using EpochLegends.Core.Ability;
using EpochLegends.Core.Hero;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Añadida esta directiva using para EventTrigger

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
        
        [Header("Tooltip")]
        [SerializeField] private GameObject tooltipPanel; // Referencia al panel de tooltip
        [SerializeField] private TextMeshProUGUI tooltipTitleText;
        [SerializeField] private TextMeshProUGUI tooltipDescriptionText;
        [SerializeField] private TextMeshProUGUI tooltipCostText;
        [SerializeField] private TextMeshProUGUI tooltipCooldownText;
        [SerializeField] private TextMeshProUGUI tooltipLevelText;
        
        [Header("Personalización")]
        [SerializeField] private bool useHorizontalLayout = true;
        [SerializeField] private int maxSlotsPerRow = 6;
        [SerializeField] private float rowSpacing = 5f;
        
        // Referencias a los slots creados
        private List<AbilitySlot> abilitySlots = new List<AbilitySlot>();
        
        // Referencia al héroe actual
        private Hero.Hero currentHero;
        
        // Tooltip activo actualmente
        private BaseAbility currentTooltipAbility;
        private Vector2 tooltipOffset = new Vector2(20, 20);
        
        private void Awake()
        {
            // Ocultar tooltip inicialmente
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }
        
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
                
                // Configurar eventos para tooltips
                // En lugar de usar AbilityTooltip, gestionamos los tooltips desde aquí
                int abilityIndex = i; // Capturar índice para la lambda
                AddTooltipEvents(slotObj, abilities[i]);
                
                // Guardar referencia al slot
                abilitySlots.Add(slot);
                
                // Nombrar para depuración
                slotObj.name = $"AbilitySlot_{i}_{abilities[i]?.Definition?.DisplayName ?? "Empty"}";
            }
            
            Debug.Log($"AbilityUIManager: Creados {abilitySlots.Count} slots de habilidades");
        }
        
        // Añade eventos para mostrar/ocultar tooltips
        private void AddTooltipEvents(GameObject slotObj, BaseAbility ability)
        {
            // Añadir listeners de eventos de ratón
            EventTrigger trigger = slotObj.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = slotObj.AddComponent<EventTrigger>();
            }
            
            // Crear lista de triggers si no existe
            if (trigger.triggers == null)
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }
            
            // Evento de entrada del ratón
            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data) => { ShowTooltip(ability); });
            trigger.triggers.Add(entryEnter);
            
            // Evento de salida del ratón
            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data) => { HideTooltip(); });
            trigger.triggers.Add(entryExit);
        }
        
        // Muestra el tooltip con la información de la habilidad
        private void ShowTooltip(BaseAbility ability)
        {
            if (ability == null || tooltipPanel == null) return;
            
            currentTooltipAbility = ability;
            
            // Actualizar contenido del tooltip
            UpdateTooltipContent(ability);
            
            // Posicionar tooltip cerca del ratón
            PositionTooltipAtMouse();
            
            // Mostrar tooltip
            tooltipPanel.SetActive(true);
        }
        
        // Oculta el tooltip
        private void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
                currentTooltipAbility = null;
            }
        }
        
        // Actualiza el contenido del tooltip para la habilidad dada
        private void UpdateTooltipContent(BaseAbility ability)
        {
            if (ability == null || ability.Definition == null) return;
            
            // Actualizar título
            if (tooltipTitleText != null)
            {
                tooltipTitleText.text = ability.Definition.DisplayName;
            }
            
            // Actualizar descripción con valores actuales
            if (tooltipDescriptionText != null)
            {
                string description = ability.Definition.Description;
                
                // Reemplazar placeholders con valores reales según nivel
                description = description.Replace("{damage}", ability.Definition.GetDamageForLevel(ability.Level).ToString("F0"));
                description = description.Replace("{duration}", ability.Definition.GetDurationForLevel(ability.Level).ToString("F1"));
                description = description.Replace("{level}", ability.Level.ToString());
                
                tooltipDescriptionText.text = description;
            }
            
            // Actualizar costo
            if (tooltipCostText != null)
            {
                float manaCost = ability.Definition.GetManaCostForLevel(ability.Level);
                tooltipCostText.text = $"Coste: {manaCost} maná";
                tooltipCostText.gameObject.SetActive(manaCost > 0);
            }
            
            // Actualizar cooldown
            if (tooltipCooldownText != null)
            {
                float cooldown = ability.Definition.GetCooldownForLevel(ability.Level);
                tooltipCooldownText.text = $"Enfriamiento: {cooldown}s";
                tooltipCooldownText.gameObject.SetActive(cooldown > 0);
            }
            
            // Actualizar nivel
            if (tooltipLevelText != null)
            {
                tooltipLevelText.text = $"Nivel: {ability.Level} / {ability.Definition.MaxLevel}";
            }
        }
        
        // Posiciona el tooltip cerca del ratón
        private void PositionTooltipAtMouse()
        {
            if (tooltipPanel == null) return;
            
            RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            if (tooltipRect == null) return;
            
            // Obtener posición del ratón
            Vector2 mousePos = Input.mousePosition;
            
            // Convertir a posición local en el canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.transform as RectTransform,
                    mousePos,
                    canvas.worldCamera,
                    out localPoint);
                    
                // Ajustar posición para que no se salga de la pantalla
                Vector2 tooltipSize = tooltipRect.sizeDelta;
                
                if (localPoint.x + tooltipSize.x > (canvas.transform as RectTransform).rect.width / 2)
                {
                    localPoint.x = localPoint.x - tooltipSize.x - tooltipOffset.x;
                }
                else
                {
                    localPoint.x = localPoint.x + tooltipOffset.x;
                }
                
                if (localPoint.y - tooltipSize.y < -(canvas.transform as RectTransform).rect.height / 2)
                {
                    localPoint.y = localPoint.y + tooltipSize.y + tooltipOffset.y;
                }
                else
                {
                    localPoint.y = localPoint.y - tooltipOffset.y;
                }
                
                // Establecer posición
                tooltipRect.anchoredPosition = localPoint;
            }
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
                }
            }
            
            // Si el tooltip está visible, actualizar su contenido 
            if (tooltipPanel != null && tooltipPanel.activeSelf && currentTooltipAbility != null)
            {
                UpdateTooltipContent(currentTooltipAbility);
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
            
            // Si el tooltip está visible, actualizar su posición si sigue al ratón
            if (tooltipPanel != null && tooltipPanel.activeSelf)
            {
                PositionTooltipAtMouse();
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