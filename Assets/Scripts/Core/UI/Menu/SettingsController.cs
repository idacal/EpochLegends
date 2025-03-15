using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System.Collections.Generic;
using EpochLegends.Core.UI.Manager;

namespace EpochLegends.Core.UI.Menu
{
    public class SettingsController : MonoBehaviour, IUIPanelController
    {
        [Header("UI References")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button backButton;
        
        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;
        
        // Resolution options
        private Resolution[] resolutions;
        private int currentResolutionIndex = 0;
        
        // Store original settings in case user cancels
        private float originalMasterVolume;
        private float originalMusicVolume;
        private float originalSfxVolume;
        private bool originalFullscreen;
        private int originalResolutionIndex;
        
        private void Awake()
        {
            // Set up resolution dropdown
            SetupResolutionDropdown();
            
            // Set up button listeners
            if (applyButton != null)
                applyButton.onClick.AddListener(ApplySettings);
                
            if (backButton != null)
                backButton.onClick.AddListener(ReturnToMainMenu);
                
            // Set up slider listeners
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
                
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
                
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
                
            // Set up toggle listener
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        
        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null) return;
            
            // Get all available resolutions
            resolutions = Screen.resolutions;
            
            // Clear the dropdown options
            resolutionDropdown.ClearOptions();
            
            // Create a list to hold the resolution strings
            List<string> options = new List<string>();
            
            // Set the current resolution index
            for (int i = 0; i < resolutions.Length; i++)
            {
                // Usar refreshRateRatio.value para obtener la tasa de refresco como int
                string option = $"{resolutions[i].width} x {resolutions[i].height} @ {resolutions[i].refreshRateRatio.value.ToString("F0")}Hz";
                options.Add(option);
                
                if (resolutions[i].width == Screen.currentResolution.width && 
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                }
            }
            
            // Add the options to the dropdown
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }
        
        private void LoadCurrentSettings()
        {
            // Load audio settings
            if (audioMixer != null)
            {
                audioMixer.GetFloat("MasterVolume", out originalMasterVolume);
                audioMixer.GetFloat("MusicVolume", out originalMusicVolume);
                audioMixer.GetFloat("SFXVolume", out originalSfxVolume);
                
                if (masterVolumeSlider != null)
                    masterVolumeSlider.value = ConvertVolumeToSlider(originalMasterVolume);
                
                if (musicVolumeSlider != null)
                    musicVolumeSlider.value = ConvertVolumeToSlider(originalMusicVolume);
                
                if (sfxVolumeSlider != null)
                    sfxVolumeSlider.value = ConvertVolumeToSlider(originalSfxVolume);
            }
            
            // Load screen settings
            originalFullscreen = Screen.fullScreen;
            originalResolutionIndex = currentResolutionIndex;
            
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = originalFullscreen;
                
            if (resolutionDropdown != null)
                resolutionDropdown.value = originalResolutionIndex;
        }
        
        private float ConvertVolumeToSlider(float dbVolume)
        {
            // Convert from dB (-80 to 0) to slider range (0 to 1)
            return Mathf.Clamp01((dbVolume + 80f) / 80f);
        }
        
        private float ConvertSliderToVolume(float sliderValue)
        {
            // Convert from slider range (0 to 1) to dB (-80 to 0)
            return Mathf.Lerp(-80f, 0f, sliderValue);
        }
        
        private void SetMasterVolume(float volume)
        {
            if (audioMixer != null)
            {
                audioMixer.SetFloat("MasterVolume", ConvertSliderToVolume(volume));
            }
        }
        
        private void SetMusicVolume(float volume)
        {
            if (audioMixer != null)
            {
                audioMixer.SetFloat("MusicVolume", ConvertSliderToVolume(volume));
            }
        }
        
        private void SetSfxVolume(float volume)
        {
            if (audioMixer != null)
            {
                audioMixer.SetFloat("SFXVolume", ConvertSliderToVolume(volume));
            }
        }
        
        private void SetFullscreen(bool isFullscreen)
        {
            // Preview fullscreen change
            Screen.fullScreen = isFullscreen;
        }
        
        private void ApplySettings()
        {
            // Apply resolution change
            if (resolutionDropdown != null && resolutions != null && resolutionDropdown.value < resolutions.Length)
            {
                Resolution resolution = resolutions[resolutionDropdown.value];
                Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
                currentResolutionIndex = resolutionDropdown.value;
            }
            
            // Save settings to PlayerPrefs
            SaveSettings();
            
            // Return to main menu
            ReturnToMainMenu();
        }
        
        private void SaveSettings()
        {
            // Save audio settings
            if (audioMixer != null)
            {
                float masterVolume, musicVolume, sfxVolume;
                audioMixer.GetFloat("MasterVolume", out masterVolume);
                audioMixer.GetFloat("MusicVolume", out musicVolume);
                audioMixer.GetFloat("SFXVolume", out sfxVolume);
                
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
                PlayerPrefs.SetFloat("MusicVolume", musicVolume);
                PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            }
            
            // Save screen settings
            PlayerPrefs.SetInt("ScreenResolutionIndex", currentResolutionIndex);
            PlayerPrefs.SetInt("Fullscreen", Screen.fullScreen ? 1 : 0);
            
            // Save all PlayerPrefs
            PlayerPrefs.Save();
        }
        
        private void ReturnToMainMenu()
        {
            UIManager.Instance?.ShowPanel(UIPanel.MainMenu);
        }
        
        #region IUIPanelController Implementation
        
        public void OnPanelShown()
        {
            // Load current settings when panel is shown
            LoadCurrentSettings();
        }
        
        public void OnPanelHidden()
        {
            // Nothing specific to do when hidden
        }
        
        #endregion
    }
}