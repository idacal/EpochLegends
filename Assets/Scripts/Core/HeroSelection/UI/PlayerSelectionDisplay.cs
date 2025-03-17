using UnityEngine;
using UnityEngine.UI;
using EpochLegends.Core.Hero;

namespace EpochLegends.UI.HeroSelection
{
    public class PlayerSelectionDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Image heroIconImage;
        [SerializeField] private Text heroNameText;
        [SerializeField] private GameObject localPlayerIndicator;
        [SerializeField] private GameObject readyIndicator;
        [SerializeField] private GameObject noSelectionIndicator;
        
        [Header("Colors")]
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;

        private uint playerNetId;
        private bool isLocalPlayer;
        private bool isReady;
        private HeroDefinition selectedHero;

        public void Initialize(string playerName, bool isLocalPlayer, HeroDefinition selectedHero = null)
        {
            // Set player name
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
                
                // Add (You) suffix for local player
                if (isLocalPlayer)
                {
                    playerNameText.text += " (You)";
                }
            }

            this.isLocalPlayer = isLocalPlayer;
            this.selectedHero = selectedHero;

            // Set local player indicator visibility
            if (localPlayerIndicator != null)
            {
                localPlayerIndicator.SetActive(isLocalPlayer);
            }

            // Set hero info
            UpdateHeroSelection(selectedHero);

            // Set ready status (default to not ready)
            SetReadyStatus(false);
        }

        public void UpdateHeroSelection(HeroDefinition hero)
        {
            selectedHero = hero;

            if (hero != null)
            {
                // Show hero icon and name
                if (heroIconImage != null)
                {
                    heroIconImage.sprite = hero.HeroIcon;
                    heroIconImage.gameObject.SetActive(true);
                }

                if (heroNameText != null)
                {
                    heroNameText.text = hero.DisplayName;
                }

                // Hide no selection indicator
                if (noSelectionIndicator != null)
                {
                    noSelectionIndicator.SetActive(false);
                }
            }
            else
            {
                // No hero selected
                if (heroIconImage != null)
                {
                    heroIconImage.gameObject.SetActive(false);
                }

                if (heroNameText != null)
                {
                    heroNameText.text = "No seleccionado";
                }

                // Show no selection indicator
                if (noSelectionIndicator != null)
                {
                    noSelectionIndicator.SetActive(true);
                }
            }
        }

        public void SetReadyStatus(bool ready)
        {
            isReady = ready;

            if (readyIndicator != null)
            {
                readyIndicator.SetActive(ready);
                
                // Optionally change color
                Image indicatorImage = readyIndicator.GetComponent<Image>();
                if (indicatorImage != null)
                {
                    indicatorImage.color = ready ? readyColor : notReadyColor;
                }
            }
            
            // Optionally add a text indicator
            Text readyText = readyIndicator?.GetComponentInChildren<Text>();
            if (readyText != null)
            {
                readyText.text = ready ? "Listo" : "No listo";
            }
        }

        public void SetPlayerNetId(uint netId)
        {
            playerNetId = netId;
        }

        public uint GetPlayerNetId()
        {
            return playerNetId;
        }

        public bool IsReady()
        {
            return isReady;
        }

        public HeroDefinition GetSelectedHero()
        {
            return selectedHero;
        }

        public bool IsLocalPlayer()
        {
            return isLocalPlayer;
        }
    }
}