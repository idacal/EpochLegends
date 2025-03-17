using UnityEngine;
using UnityEngine.UI;
using EpochLegends.Core.Ability;

namespace EpochLegends.UI.HeroSelection
{
    public class AbilityDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image abilityIconImage;
        [SerializeField] private Text abilityNameText;
        [SerializeField] private Text abilityDescriptionText;
        [SerializeField] private Text abilityTypeText;
        
        [Header("Type Colors")]
        [SerializeField] private Color passiveColor = new Color(0.5f, 0.5f, 1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color ultimateColor = new Color(1f, 0.6f, 0.1f);

        private AbilityDefinition abilityDefinition;

        public void Initialize(AbilityDefinition ability)
        {
            abilityDefinition = ability;

            // Set icon
            if (abilityIconImage != null && ability.AbilityIcon != null)
            {
                abilityIconImage.sprite = ability.AbilityIcon;
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

            // Set ability type and color
            if (abilityTypeText != null)
            {
                abilityTypeText.text = ability.AbilityType.ToString();
                
                // Set color based on ability type
                switch(ability.AbilityType)
                {
                    case AbilityType.Passive:
                        abilityTypeText.color = passiveColor;
                        break;
                    case AbilityType.Active:
                        abilityTypeText.color = activeColor;
                        break;
                    case AbilityType.Ultimate:
                        abilityTypeText.color = ultimateColor;
                        break;
                }
            }

            // Optional: Show additional info like cooldown, mana cost, etc.
            UpdateAdditionalInfo();
        }

        private void UpdateAdditionalInfo()
        {
            // You could add additional text elements showing cooldown, mana cost, etc.
            // For example:
            // cooldownText.text = $"Cooldown: {abilityDefinition.Cooldown}s";
            // manaCostText.text = $"Mana: {abilityDefinition.ManaCost}";
        }

        // Optional tooltips or expanded info
        public void ShowDetailedTooltip()
        {
            // Implementation for showing more detailed tooltip on hover/click
            // This could include scaling effects and detailed text
        }

        public void HideDetailedTooltip()
        {
            // Hide detailed tooltip
        }
    }
}