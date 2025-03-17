using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EpochLegends.Core.Hero;

namespace EpochLegends.UI.HeroSelection
{
    public class PlayerSelectionDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image heroIconImage;
        [SerializeField] private TextMeshProUGUI heroNameText;
        [SerializeField] private GameObject readyIndicator;
        [SerializeField] private GameObject localPlayerIndicator;
        [SerializeField] private GameObject noSelectionIndicator;
        
        [Header("Colors")]
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color notSelectedColor = Color.gray;
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;
        
        private uint playerNetId;
        private bool isLocalPlayer;
        private bool isReady;
        private HeroDefinition selectedHero;
        
        private void Awake()
        {
            // Find components if not assigned
            if (playerNameText == null)
                playerNameText = transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
                
            if (heroIconImage == null)
                heroIconImage = transform.Find("HeroIcon")?.GetComponent<Image>();
                
            if (heroNameText == null)
                heroNameText = transform.Find("HeroNameText")?.GetComponent<TextMeshProUGUI>();
                
            if (readyIndicator == null)
                readyIndicator = transform.Find("ReadyIndicator")?.gameObject;
                
            if (localPlayerIndicator == null)
                localPlayerIndicator = transform.Find("LocalPlayerIndicator")?.gameObject;
                
            if (noSelectionIndicator == null)
                noSelectionIndicator = transform.Find("NoSelectionIndicator")?.gameObject;
        }
        
        public void Initialize(string playerName, bool isLocalPlayer, HeroDefinition selectedHero = null)
        {
            // Store references
            this.isLocalPlayer = isLocalPlayer;
            this.selectedHero = selectedHero;
            this.isReady = false; // Default to not ready
            
            // Set player name
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
                
                // Make local player name stand out
                if (isLocalPlayer)
                {
                    playerNameText.fontStyle = FontStyles.Bold;
                }
            }
            
            // Show/hide local player indicator
            if (localPlayerIndicator != null)
            {
                localPlayerIndicator.SetActive(isLocalPlayer);
            }
            
            // Update hero selection visuals
            UpdateHeroSelection();
            
            // Update ready status
            SetReadyStatus(false);
        }
        
        // Método que faltaba - corregido para que coincida con la llamada en HeroSelectionUIController
        public void SetPlayerNetId(uint netId)
        {
            playerNetId = netId;
        }
        
        public uint GetNetId()
        {
            return playerNetId;
        }
        
        public void UpdateHeroSelection(HeroDefinition hero = null)
        {
            if (hero != null)
            {
                selectedHero = hero;
            }
            
            // Set hero info if selected
            if (selectedHero != null)
            {
                if (heroIconImage != null)
                {
                    heroIconImage.sprite = selectedHero.HeroIcon;
                    heroIconImage.color = selectedColor;
                    heroIconImage.gameObject.SetActive(true);
                }
                
                if (heroNameText != null)
                {
                    heroNameText.text = selectedHero.DisplayName;
                    heroNameText.color = selectedColor;
                }
                
                if (noSelectionIndicator != null)
                {
                    noSelectionIndicator.SetActive(false);
                }
            }
            else
            {
                // No hero selected yet
                if (heroIconImage != null)
                {
                    heroIconImage.gameObject.SetActive(false);
                }
                
                if (heroNameText != null)
                {
                    heroNameText.text = "No seleccionado";
                    heroNameText.color = notSelectedColor;
                }
                
                if (noSelectionIndicator != null)
                {
                    noSelectionIndicator.SetActive(true);
                }
            }
        }
        
        // Método que faltaba - corregido para que coincida con la llamada en HeroSelectionUIController
        public void SetReadyStatus(bool ready)
        {
            isReady = ready;
            
            if (readyIndicator != null)
            {
                readyIndicator.SetActive(true);
                
                // Change color based on status
                Image readyImage = readyIndicator.GetComponent<Image>();
                if (readyImage != null)
                {
                    readyImage.color = ready ? readyColor : notReadyColor;
                }
                
                // Update text if present
                TextMeshProUGUI readyText = readyIndicator.GetComponentInChildren<TextMeshProUGUI>();
                if (readyText != null)
                {
                    readyText.text = ready ? "Listo" : "No Listo";
                }
            }
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