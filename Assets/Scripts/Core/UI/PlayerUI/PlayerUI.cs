using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using EpochLegends.Core.Hero.Components;
using EpochLegends.Core.Hero.Stats;
using EpochLegends.Core.Ability.Components;

namespace EpochLegends.Core.UI.PlayerUI
{
    public class PlayerUI : MonoBehaviour
    {
        [Header("Health & Resources")]
        [SerializeField] private Image healthBar;
        [SerializeField] private Image manaBar;
        [SerializeField] private Text healthText;
        [SerializeField] private Text manaText;
        
        [Header("Ability UI")]
        [SerializeField] private List<AbilityUISlot> abilitySlots = new List<AbilityUISlot>();
        [SerializeField] private Image targetIndicator;
        
        [Header("Character Info")]
        [SerializeField] private Text heroNameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Image heroPortrait;
        
        [Header("Status Effects")]
        [SerializeField] private Transform statusEffectContainer;
        [SerializeField] private GameObject statusEffectPrefab;
        
        // References
        private Hero trackedHero;
        private Dictionary<int, StatusEffectUI> activeStatusEffects = new Dictionary<int, StatusEffectUI>();
        
        // State tracking
        private bool isInitialized = false;
        
        private void Update()
        {
            if (trackedHero == null || !isInitialized)
                return;
                
            // Update health and mana displays
            UpdateResourceBars();
            
            // Update ability cooldowns
            UpdateAbilityCooldowns();
        }
        
        public void Initialize(Hero hero)
        {
            trackedHero = hero;
            
            if (hero == null)
            {
                Debug.LogError("Cannot initialize PlayerUI with null hero");
                return;
            }
            
            // Set up hero information
            if (heroNameText != null)
                heroNameText.text = hero.HeroDefinition != null ? hero.HeroDefinition.DisplayName : "Hero";
                
            if (levelText != null)
                levelText.text = $"Lvl {hero.Level}";
                
            if (heroPortrait != null && hero.HeroDefinition != null)
                heroPortrait.sprite = hero.HeroDefinition.HeroPortrait;
                
            // Set up ability UI
            InitializeAbilityUI();
            
            // Register for hero events
            hero.OnHeroLevelUp += OnHeroLevelUp;
            
            // Initial UI update
            UpdateResourceBars();
            ClearStatusEffects();
            
            isInitialized = true;
            
            Debug.Log($"PlayerUI initialized for hero: {hero.name}");
        }
        
        private void OnDestroy()
        {
            // Unregister from events
            if (trackedHero != null)
            {
                trackedHero.OnHeroLevelUp -= OnHeroLevelUp;
            }
        }
        
        #region Resource Bars
        
        private void UpdateResourceBars()
        {
            if (trackedHero == null) return;
            
            // Update health
            if (healthBar != null)
            {
                float healthPercent = trackedHero.CurrentHealth / trackedHero.Stats.MaxHealth;
                healthBar.fillAmount = Mathf.Clamp01(healthPercent);
                
                if (healthText != null)
                    healthText.text = $"{Mathf.Ceil(trackedHero.CurrentHealth)}/{Mathf.Ceil(trackedHero.Stats.MaxHealth)}";
            }
            
            // Update mana
            if (manaBar != null)
            {
                float manaPercent = trackedHero.CurrentMana / trackedHero.Stats.MaxMana;
                manaBar.fillAmount = Mathf.Clamp01(manaPercent);
                
                if (manaText != null)
                    manaText.text = $"{Mathf.Ceil(trackedHero.CurrentMana)}/{Mathf.Ceil(trackedHero.Stats.MaxMana)}";
            }
        }
        
        #endregion
        
        #region Ability UI
        
        private void InitializeAbilityUI()
        {
            if (trackedHero == null || trackedHero.Abilities == null) return;
            
            // Match ability slots to hero abilities
            for (int i = 0; i < trackedHero.Abilities.Count && i < abilitySlots.Count; i++)
            {
                BaseAbility ability = trackedHero.Abilities[i];
                AbilityUISlot slot = abilitySlots[i];
                
                if (ability != null && slot != null)
                {
                    slot.Initialize(ability);
                }
            }
        }
        
        private void UpdateAbilityCooldowns()
        {
            if (trackedHero == null || trackedHero.Abilities == null) return;
            
            // Update cooldown indicators on ability slots
            for (int i = 0; i < trackedHero.Abilities.Count && i < abilitySlots.Count; i++)
            {
                BaseAbility ability = trackedHero.Abilities[i];
                AbilityUISlot slot = abilitySlots[i];
                
                if (ability != null && slot != null)
                {
                    slot.UpdateCooldown(ability.CurrentCooldown, ability.MaxCooldown);
                }
            }
        }
        
        #endregion
        
        #region Status Effects
        
        public void AddStatusEffect(int statusId, Sprite icon, float duration, string statusName = "")
        {
            if (statusEffectContainer == null || statusEffectPrefab == null) return;
            
            // Remove any existing instance of this status
            RemoveStatusEffect(statusId);
            
            // Create new status effect UI
            GameObject statusObj = Instantiate(statusEffectPrefab, statusEffectContainer);
            StatusEffectUI statusUI = statusObj.GetComponent<StatusEffectUI>();
            
            if (statusUI != null)
            {
                statusUI.Initialize(icon, duration, statusName);
                activeStatusEffects[statusId] = statusUI;
            }
        }
        
        public void UpdateStatusEffectDuration(int statusId, float remainingDuration)
        {
            if (activeStatusEffects.TryGetValue(statusId, out StatusEffectUI statusUI))
            {
                statusUI.UpdateDuration(remainingDuration);
            }
        }
        
        public void RemoveStatusEffect(int statusId)
        {
            if (activeStatusEffects.TryGetValue(statusId, out StatusEffectUI statusUI))
            {
                if (statusUI != null)
                {
                    Destroy(statusUI.gameObject);
                }
                activeStatusEffects.Remove(statusId);
            }
        }
        
        public void ClearStatusEffects()
        {
            foreach (var statusUI in activeStatusEffects.Values)
            {
                if (statusUI != null)
                {
                    Destroy(statusUI.gameObject);
                }
            }
            
            activeStatusEffects.Clear();
        }
        
        #endregion
        
        #region Target Indicator
        
        public void ShowTargetIndicator(Transform target)
        {
            if (targetIndicator == null) return;
            
            targetIndicator.gameObject.SetActive(true);
            
            // In a full implementation, this would position and update the indicator
            // above the target, possibly involving camera space calculations
        }
        
        public void HideTargetIndicator()
        {
            if (targetIndicator == null) return;
            
            targetIndicator.gameObject.SetActive(false);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnHeroLevelUp(Hero hero)
        {
            // Update level text
            if (levelText != null)
                levelText.text = $"Lvl {hero.Level}";
                
            // Update ability UI as abilities might have leveled up
            UpdateAbilityCooldowns();
        }
        
        #endregion
    }
    
    // Helper class for individual ability UI slots
    [System.Serializable]
    public class AbilityUISlot
    {
        [SerializeField] private Image abilityIcon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Text cooldownText;
        [SerializeField] private Text keyBindText;
        [SerializeField] private GameObject activeIndicator;
        
        private BaseAbility linkedAbility;
        
        public void Initialize(BaseAbility ability)
        {
            linkedAbility = ability;
            
            if (ability == null) return;
            
            // Set ability icon
            if (abilityIcon != null && ability.Definition != null)
            {
                abilityIcon.sprite = ability.Definition.AbilityIcon;
            }
            
            // Reset cooldown display
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
            }
            
            if (cooldownText != null)
            {
                cooldownText.text = "";
                cooldownText.gameObject.SetActive(false);
            }
            
            // Show active indicator if ability is active
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(ability.IsActive);
            }
        }
        
        public void UpdateCooldown(float currentCooldown, float maxCooldown)
        {
            if (maxCooldown <= 0) return;
            
            // Update cooldown fill
            if (cooldownOverlay != null)
            {
                float cooldownRatio = currentCooldown / maxCooldown;
                cooldownOverlay.fillAmount = cooldownRatio;
                cooldownOverlay.gameObject.SetActive(cooldownRatio > 0);
            }
            
            // Update cooldown text
            if (cooldownText != null)
            {
                if (currentCooldown > 0)
                {
                    cooldownText.text = Mathf.Ceil(currentCooldown).ToString();
                    cooldownText.gameObject.SetActive(true);
                }
                else
                {
                    cooldownText.text = "";
                    cooldownText.gameObject.SetActive(false);
                }
            }
            
            // Update active state
            if (activeIndicator != null && linkedAbility != null)
            {
                activeIndicator.SetActive(linkedAbility.IsActive);
            }
        }
        
        public void SetKeyBind(string key)
        {
            if (keyBindText != null)
            {
                keyBindText.text = key;
            }
        }
    }
    
    // Helper class for status effect UI
    public class StatusEffectUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image durationFill;
        [SerializeField] private Text durationText;
        [SerializeField] private Text nameText;
        
        private float maxDuration;
        private float remainingDuration;
        
        public void Initialize(Sprite icon, float duration, string statusName = "")
        {
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
            }
            
            maxDuration = duration;
            remainingDuration = duration;
            
            if (durationFill != null)
            {
                durationFill.fillAmount = 1f;
            }
            
            if (durationText != null && duration > 0)
            {
                durationText.text = Mathf.Ceil(duration).ToString();
            }
            
            if (nameText != null && !string.IsNullOrEmpty(statusName))
            {
                nameText.text = statusName;
                nameText.gameObject.SetActive(true);
            }
            else if (nameText != null)
            {
                nameText.gameObject.SetActive(false);
            }
        }
        
        public void UpdateDuration(float remaining)
        {
            remainingDuration = remaining;
            
            if (durationFill != null && maxDuration > 0)
            {
                durationFill.fillAmount = Mathf.Clamp01(remaining / maxDuration);
            }
            
            if (durationText != null)
            {
                durationText.text = Mathf.Ceil(remaining).ToString();
            }
        }
        
        private void Update()
        {
            if (remainingDuration <= 0) return;
            
            remainingDuration -= Time.deltaTime;
            
            if (durationFill != null && maxDuration > 0)
            {
                durationFill.fillAmount = Mathf.Clamp01(remainingDuration / maxDuration);
            }
            
            if (durationText != null)
            {
                durationText.text = Mathf.Ceil(Mathf.Max(0, remainingDuration)).ToString();
            }
            
            if (remainingDuration <= 0)
            {
                // This status effect has expired
                // In a real implementation, you might want to notify a parent controller
                // For simplicity, we'll just fade out and destroy
                FadeOutAndDestroy();
            }
        }
        
        private void FadeOutAndDestroy()
        {
            // Fade out animation
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                LeanTween.alphaCanvas(group, 0f, 0.5f).setOnComplete(() => {
                    Destroy(gameObject);
                });
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}