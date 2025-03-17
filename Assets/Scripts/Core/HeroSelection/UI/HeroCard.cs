using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EpochLegends.Core.Hero;
using System;

namespace EpochLegends.UI.HeroSelection
{
    public enum HeroCardState
    {
        Available,
        Selected,
        Unavailable
    }

    public class HeroCard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image heroIconImage;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image stateIndicator;
        [SerializeField] private Button button;
        
        [Header("State Colors")]
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color selectedColor = Color.green;
        [SerializeField] private Color unavailableColor = Color.gray;
        
        private HeroDefinition heroDefinition;
        private HeroCardState currentState = HeroCardState.Available;
        
        // Event for when card is clicked
        public delegate void HeroCardClickHandler(HeroDefinition hero);
        public event HeroCardClickHandler OnClicked;
        
        public string HeroId => heroDefinition?.HeroId;
        
        private void Awake()
        {
            // Set up button click handler
            if (button == null)
                button = GetComponent<Button>();
                
            if (button != null)
                button.onClick.AddListener(OnCardClicked);
                
            // Find components if not assigned
            if (heroIconImage == null)
                heroIconImage = transform.Find("HeroIcon")?.GetComponent<Image>();
                
            if (heroNameText == null)
                heroNameText = transform.Find("HeroNameText")?.GetComponent<TextMeshProUGUI>();
                
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
                
            if (stateIndicator == null)
                stateIndicator = transform.Find("StateIndicator")?.GetComponent<Image>();
        }
        
        public void Initialize(HeroDefinition hero)
        {
            heroDefinition = hero;
            
            // Set icon and name
            if (heroIconImage != null && hero.HeroIcon != null)
            {
                heroIconImage.sprite = hero.HeroIcon;
            }
            
            if (heroNameText != null)
            {
                heroNameText.text = hero.DisplayName;
            }
            
            // Default to available state
            SetState(HeroCardState.Available);
        }
        
        public void SetState(HeroCardState state)
        {
            currentState = state;
            
            // Update visual state
            if (backgroundImage != null)
            {
                switch (state)
                {
                    case HeroCardState.Available:
                        backgroundImage.color = availableColor;
                        button.interactable = true;
                        break;
                    case HeroCardState.Selected:
                        backgroundImage.color = selectedColor;
                        button.interactable = true;
                        break;
                    case HeroCardState.Unavailable:
                        backgroundImage.color = unavailableColor;
                        button.interactable = false;
                        break;
                }
            }
            
            // Update state indicator if present
            if (stateIndicator != null)
            {
                stateIndicator.gameObject.SetActive(state != HeroCardState.Available);
                
                switch (state)
                {
                    case HeroCardState.Selected:
                        stateIndicator.color = selectedColor;
                        break;
                    case HeroCardState.Unavailable:
                        stateIndicator.color = unavailableColor;
                        break;
                }
            }
        }
        
        private void OnCardClicked()
        {
            // Play click sound
            PlayClickSound();
            
            // Invoke click event
            OnClicked?.Invoke(heroDefinition);
        }
        
        private void PlayClickSound()
        {
            // You can implement sound playing here if you have an audio manager
            // For example: AudioManager.Instance.PlaySound("ButtonClick");
        }
        
        public HeroDefinition GetHeroDefinition()
        {
            return heroDefinition;
        }
    }
}