using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EpochLegends.Core.Ability;

namespace EpochLegends.UI.HeroSelection
{
    public class AbilityDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image abilityIconImage;
        [SerializeField] private TextMeshProUGUI abilityNameText;
        [SerializeField] private TextMeshProUGUI abilityDescriptionText;
        [SerializeField] private TextMeshProUGUI abilityTypeText;
        
        private AbilityDefinition abilityDefinition;
        
        private void Awake()
        {
            // Find components if not assigned
            if (abilityIconImage == null)
                abilityIconImage = transform.Find("AbilityIcon")?.GetComponent<Image>();
                
            if (abilityNameText == null)
                abilityNameText = transform.Find("AbilityNameText")?.GetComponent<TextMeshProUGUI>();
                
            if (abilityDescriptionText == null)
                abilityDescriptionText = transform.Find("AbilityDescriptionText")?.GetComponent<TextMeshProUGUI>();
                
            if (abilityTypeText == null)
                abilityTypeText = transform.Find("AbilityTypeText")?.GetComponent<TextMeshProUGUI>();
        }
        
        public void Initialize(AbilityDefinition ability)
        {
            abilityDefinition = ability;
            
            // Set icon
            if (abilityIconImage != null && ability.AbilityIcon != null)
            {
                abilityIconImage.sprite = ability.AbilityIcon;
                abilityIconImage.preserveAspect = true;
            }
            
            // Set name and description
            if (abilityNameText != null)
            {
                abilityNameText.text = ability.DisplayName;
            }
            
            if (abilityDescriptionText != null)
            {
                abilityDescriptionText.text = ability.Description;
            }
            
            // Set ability type if available
            if (abilityTypeText != null)
            {
                abilityTypeText.text = ability.AbilityType.ToString();
                
                // Set color based on ability type
                switch (ability.AbilityType)
                {
                    case AbilityType.Passive:
                        abilityTypeText.color = new Color(0.2f, 0.6f, 0.2f); // Green
                        break;
                    case AbilityType.Active:
                        abilityTypeText.color = new Color(0.2f, 0.4f, 0.8f); // Blue
                        break;
                    case AbilityType.Ultimate:
                        abilityTypeText.color = new Color(0.8f, 0.6f, 0.2f); // Gold
                        break;
                    default:
                        abilityTypeText.color = Color.white;
                        break;
                }
            }
        }
        
        // Optional: Add methods for interactivity if needed
        public void OnPointerEnter()
        {
            // Handle hover effect
            transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);
        }
        
        public void OnPointerExit()
        {
            // Reset hover effect
            transform.localScale = Vector3.one;
        }
    }
}