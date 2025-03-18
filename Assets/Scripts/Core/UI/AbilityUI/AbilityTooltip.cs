using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using EpochLegends.Core.Ability;

namespace EpochLegends.Core.UI.Game
{
    /// <summary>
    /// Muestra información detallada sobre una habilidad al pasar el cursor
    /// </summary>
    public class AbilityTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Referencias")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private TextMeshProUGUI levelText;
        
        [Header("Configuración")]
        [SerializeField] private float tooltipOffset = 20f;
        [SerializeField] private float showDelay = 0.2f; // Pequeño retraso para evitar tooltips al pasar rápidamente
        [SerializeField] private bool useFixedPosition = false;
        [SerializeField] private Vector2 fixedPosition = Vector2.zero;
        [SerializeField] private int defaultMaxLevel = 5; // Nivel máximo predeterminado si no está definido en la habilidad
        
        // Estado interno
        private BaseAbility linkedAbility;
        private Canvas parentCanvas;
        private RectTransform tooltipRect;
        private float hoverStartTime;
        private bool isPointerOver = false;
        
        private void Awake()
        {
            // Ocultar tooltip inicialmente
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
                tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            }
            
            // Buscar el canvas padre para posicionamiento
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogWarning("AbilityTooltip: No se encontró un Canvas padre, el posicionamiento puede no funcionar correctamente");
            }
        }
        
        private void Update()
        {
            // Si el cursor está sobre el elemento y ha pasado el tiempo de retraso, mostrar tooltip
            if (isPointerOver && !tooltipPanel.activeSelf && Time.time >= hoverStartTime + showDelay)
            {
                ShowTooltip();
            }
            
            // Si el tooltip está activo y está siguiendo el cursor, actualizar posición
            if (tooltipPanel != null && tooltipPanel.activeSelf && !useFixedPosition)
            {
                PositionTooltipAtMouse();
            }
        }
        
        /// <summary>
        /// Asigna una habilidad a este tooltip
        /// </summary>
        public void SetAbility(BaseAbility ability)
        {
            linkedAbility = ability;
            
            if (ability != null)
            {
                Debug.Log($"AbilityTooltip: Vinculada habilidad {ability.Definition?.DisplayName}");
            }
        }
        
        /// <summary>
        /// Muestra el tooltip con la información de la habilidad
        /// </summary>
        private void ShowTooltip()
        {
            if (linkedAbility == null || tooltipPanel == null)
            {
                Debug.LogWarning("AbilityTooltip: Tooltip o habilidad no configurados");
                return;
            }
            
            Debug.Log($"AbilityTooltip: Mostrando tooltip para {linkedAbility.Definition?.DisplayName}");
            
            // Actualizar contenido del tooltip
            UpdateTooltipContent();
            
            // Posicionar el tooltip
            if (useFixedPosition)
            {
                PositionTooltipFixed();
            }
            else
            {
                PositionTooltipAtMouse();
            }
            
            // Mostrar el tooltip
            tooltipPanel.SetActive(true);
        }
        
        /// <summary>
        /// Evento cuando el puntero entra en el área del slot
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOver = true;
            hoverStartTime = Time.time;
        }
        
        /// <summary>
        /// Evento cuando el puntero sale del área del slot
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
                Debug.Log("AbilityTooltip: Ocultando tooltip");
            }
        }
        
        /// <summary>
        /// Posiciona el tooltip en la posición actual del ratón
        /// </summary>
        private void PositionTooltipAtMouse()
        {
            if (tooltipRect == null || parentCanvas == null) return;
            
            // Obtener posición del mouse
            Vector2 mousePos = Input.mousePosition;
            
            // Convertir a posición local en el canvas
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                mousePos,
                parentCanvas.worldCamera,
                out Vector2 localPoint);
                
            // Ajustar posición para que no se salga de la pantalla
            Vector2 tooltipSize = tooltipRect.sizeDelta;
            
            // Comprobaciones básicas para mantener dentro de la pantalla
            float canvasWidth = (parentCanvas.transform as RectTransform).rect.width;
            float canvasHeight = (parentCanvas.transform as RectTransform).rect.height;
            
            if (localPoint.x + tooltipSize.x > canvasWidth / 2)
            {
                localPoint.x = canvasWidth / 2 - tooltipSize.x;
            }
            
            if (localPoint.y - tooltipSize.y < -canvasHeight / 2)
            {
                localPoint.y = -canvasHeight / 2 + tooltipSize.y;
            }
            
            // Establecer posición
            tooltipRect.anchoredPosition = localPoint + new Vector2(tooltipOffset, tooltipOffset); // Offset para no tapar el cursor
        }
        
        /// <summary>
        /// Posiciona el tooltip en una posición fija relativa al slot
        /// </summary>
        private void PositionTooltipFixed()
        {
            if (tooltipRect == null) return;
            
            // Usar posición fija configurada
            tooltipRect.anchoredPosition = fixedPosition;
        }
        
        /// <summary>
        /// Actualiza el contenido del tooltip con la información de la habilidad
        /// </summary>
        public void UpdateTooltipContent()
        {
            if (linkedAbility == null || linkedAbility.Definition == null) return;
            
            // Actualizar título
            if (titleText != null)
            {
                titleText.text = linkedAbility.Definition.DisplayName;
            }
            
            // Actualizar descripción con valores actuales
            if (descriptionText != null)
            {
                string description = linkedAbility.Definition.Description;
                
                // Reemplazar placeholders con valores reales según nivel
                description = description.Replace("{damage}", linkedAbility.Definition.GetDamageForLevel(linkedAbility.Level).ToString("F0"));
                description = description.Replace("{duration}", linkedAbility.Definition.GetDurationForLevel(linkedAbility.Level).ToString("F1"));
                description = description.Replace("{level}", linkedAbility.Level.ToString());
                
                // Parsear cualquier marcador adicional que pueda tener la habilidad
                // Esto permite un sistema flexible para cualquier parámetro específico
                foreach (var prop in linkedAbility.Definition.GetType().GetProperties())
                {
                    string placeholder = $"{{{prop.Name.ToLower()}}}";
                    
                    if (description.Contains(placeholder))
                    {
                        var value = prop.GetValue(linkedAbility.Definition);
                        if (value != null)
                        {
                            description = description.Replace(placeholder, value.ToString());
                        }
                    }
                }
                
                descriptionText.text = description;
            }
            
            // Actualizar costo
            if (costText != null)
            {
                float manaCost = linkedAbility.Definition.GetManaCostForLevel(linkedAbility.Level);
                costText.text = $"Coste: {manaCost} maná";
                
                // Ocultar si no tiene costo
                costText.gameObject.SetActive(manaCost > 0);
            }
            
            // Actualizar cooldown
            if (cooldownText != null)
            {
                float cooldown = linkedAbility.Definition.GetCooldownForLevel(linkedAbility.Level);
                cooldownText.text = $"Enfriamiento: {cooldown}s";
                
                // Ocultar si no tiene cooldown
                cooldownText.gameObject.SetActive(cooldown > 0);
            }
            
            // Actualizar nivel - usando un valor hardcodeado ya que AbilityDefinition no tiene MaxLevel
            if (levelText != null)
            {
                // Intenta buscar un nivel máximo por reflexión (por si está ahí pero con otro nombre)
                int maxLevel = defaultMaxLevel;
                
                var prop = linkedAbility.Definition.GetType().GetProperty("MaxLevel");
                if (prop != null)
                {
                    var value = prop.GetValue(linkedAbility.Definition);
                    if (value != null && value is int)
                    {
                        maxLevel = (int)value;
                    }
                }
                
                levelText.text = $"Nivel: {linkedAbility.Level} / {maxLevel}";
            }
        }
    }
}