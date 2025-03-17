using UnityEngine;
using UnityEngine.UI;
using EpochLegends.Core.Hero;

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
        [SerializeField] private Text heroNameText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject selectionIndicator;
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

            // Hide selection indicator initially
            if (selectionIndicator != null)
                selectionIndicator.SetActive(false);
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

            // Show/hide selection indicator
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(state == HeroCardState.Selected);
            }
        }

        private void OnCardClicked()
        {
            // Notify listeners that this card was clicked
            OnClicked?.Invoke(heroDefinition);

            // Play click sound effect
            PlayClickSound();
        }

        private void PlayClickSound()
        {
            // You can implement sound playing logic here or through a sound manager
            // For example:
            // AudioManager.Instance.PlaySound("ButtonClick");
        }

        public HeroDefinition GetHeroDefinition()
        {
            return heroDefinition;
        }
    }
}