using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EpochLegends.Core.Ability;

namespace EpochLegends.Core.UI.Game
{
    /// <summary>
    /// Representa un slot individual para una habilidad en la UI
    /// </summary>
    public class AbilitySlot : MonoBehaviour
    {
        [Header("Referencias UI")]
        [SerializeField] private Image abilityIcon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private TextMeshProUGUI hotkeyText;
        [SerializeField] private Image borderImage;
        [SerializeField] private GameObject levelIndicator;
        [SerializeField] private TextMeshProUGUI levelText;
        
        [Header("Visual Customization")]
        [SerializeField] private Color normalBorderColor = Color.grey;
        [SerializeField] private Color activeAbilityColor = Color.yellow;
        [SerializeField] private Color readyAbilityColor = Color.white;
        [SerializeField] private Color cooldownColor = new Color(0, 0, 0, 0.6f);
        [SerializeField] private GameObject activeIndicator;
        
        // Habilidad vinculada a este slot
        private BaseAbility linkedAbility;
        
        // Estado de cooldown anterior (para optimizar actualizaciones)
        private float lastCooldownValue = -1;
        private bool wasOnCooldown = false;
        
        /// <summary>
        /// Inicializa el slot con una etiqueta de tecla
        /// </summary>
        public void Initialize(string hotkeyLabel)
        {
            // Set hotkey label
            if (hotkeyText != null)
            {
                hotkeyText.text = hotkeyLabel;
            }
            
            // Reset cooldown display
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.gameObject.SetActive(false);
                cooldownOverlay.color = cooldownColor;
            }
            
            if (cooldownText != null)
            {
                cooldownText.text = "";
                cooldownText.gameObject.SetActive(false);
            }
            
            // Set border color
            if (borderImage != null)
            {
                borderImage.color = normalBorderColor;
            }
            
            // Hide level indicator initially
            if (levelIndicator != null)
            {
                levelIndicator.SetActive(false);
            }
            
            // Hide active indicator
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(false);
            }
        }
        
        /// <summary>
        /// Asigna una habilidad a este slot
        /// </summary>
        public void SetAbility(BaseAbility ability)
        {
            linkedAbility = ability;
            
            // Set ability icon
            if (abilityIcon != null && ability != null && ability.Definition != null && ability.Definition.AbilityIcon != null)
            {
                abilityIcon.sprite = ability.Definition.AbilityIcon;
                abilityIcon.gameObject.SetActive(true);
            }
            else if (abilityIcon != null)
            {
                abilityIcon.gameObject.SetActive(false);
            }
            
            // Set level indicator
            if (levelIndicator != null && levelText != null && ability != null)
            {
                levelIndicator.SetActive(true);
                levelText.text = ability.Level.ToString();
            }
            
            // Reset cooldown state
            lastCooldownValue = -1;
            wasOnCooldown = false;
            
            // Update visual state
            UpdateVisualState();
        }
        
        /// <summary>
        /// Limpia este slot (no hay habilidad asignada)
        /// </summary>
        public void ClearAbility()
        {
            linkedAbility = null;
            
            if (abilityIcon != null)
            {
                abilityIcon.sprite = null;
                abilityIcon.gameObject.SetActive(false);
            }
            
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(false);
            }
            
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(false);
            }
            
            if (levelIndicator != null)
            {
                levelIndicator.SetActive(false);
            }
            
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(false);
            }
        }
        
        /// <summary>
        /// Establece la tecla de acceso rápido para esta habilidad
        /// </summary>
        public void SetHotkey(string hotkey)
        {
            if (hotkeyText != null)
            {
                hotkeyText.text = hotkey;
            }
        }
        
        /// <summary>
        /// Actualiza la visualización de cooldown basada en la habilidad
        /// </summary>
        public void UpdateCooldown(BaseAbility ability)
        {
            if (ability == null) return;
            
            bool isOnCooldown = ability.IsOnCooldown;
            float cooldownValue = ability.CurrentCooldown;
            
            // Optimizar: solo actualizar si hay cambios
            if (isOnCooldown == wasOnCooldown && 
                Mathf.Approximately(cooldownValue, lastCooldownValue) && 
                lastCooldownValue >= 0)
            {
                return;
            }
            
            float cooldownPercent = ability.MaxCooldown > 0 ? 
                                   cooldownValue / ability.MaxCooldown : 0;
            
            // Update cooldown overlay
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(isOnCooldown);
                cooldownOverlay.fillAmount = cooldownPercent;
            }
            
            // Update cooldown text
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(isOnCooldown);
                if (isOnCooldown)
                {
                    cooldownText.text = Mathf.Ceil(cooldownValue).ToString();
                }
                else
                {
                    cooldownText.text = "";
                }
            }
            
            // Update active state
            if (ability.IsActive && activeIndicator != null)
            {
                activeIndicator.SetActive(true);
                
                if (borderImage != null)
                {
                    borderImage.color = activeAbilityColor;
                }
            }
            else
            {
                if (activeIndicator != null)
                {
                    activeIndicator.SetActive(false);
                }
                
                if (borderImage != null)
                {
                    borderImage.color = isOnCooldown ? normalBorderColor : readyAbilityColor;
                }
            }
            
            // Update level if changed
            if (levelIndicator != null && levelText != null)
            {
                levelIndicator.SetActive(true);
                levelText.text = ability.Level.ToString();
            }
            
            // Cache values for optimization
            wasOnCooldown = isOnCooldown;
            lastCooldownValue = cooldownValue;
        }
        
        /// <summary>
        /// Actualiza el estado visual del slot
        /// </summary>
        private void UpdateVisualState()
        {
            if (linkedAbility == null)
            {
                // No ability assigned
                if (borderImage != null)
                {
                    borderImage.color = normalBorderColor;
                }
                
                if (activeIndicator != null)
                {
                    activeIndicator.SetActive(false);
                }
                
                return;
            }
            
            // Handle active state
            if (linkedAbility.IsActive && activeIndicator != null)
            {
                activeIndicator.SetActive(true);
                
                if (borderImage != null)
                {
                    borderImage.color = activeAbilityColor;
                }
            }
            else
            {
                if (activeIndicator != null)
                {
                    activeIndicator.SetActive(false);
                }
                
                if (borderImage != null)
                {
                    borderImage.color = linkedAbility.IsOnCooldown ? normalBorderColor : readyAbilityColor;
                }
            }
        }
    }
}