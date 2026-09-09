using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Linq;
using System.Reflection;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Allows runtime toggling and modification of the Pixelize Render Feature via a UI Slider.
/// 0 = Off, 1-10 = Pixelation scale.
/// </summary>
public class PixelArtController : MonoBehaviour
{
    [Header("URP Asset Reference")]
    public UniversalRenderPipelineAsset urpAsset;

    [Header("UI Elements")]
    public Slider pixelSlider;
    public TextMeshProUGUI pixelScaleText;

    private PixelizeRenderFeature pixelFeature;

    private void Start()
    {
        FindPixelFeature();
        InitializeUI();
    }

    /// <summary>
    /// Searches the URP Renderer data using reflection to find our custom pixel art feature.
    /// </summary>
    private void FindPixelFeature()
    {
        if (urpAsset == null)
        {
            Debug.LogError("URP Asset is missing! Please assign it in the Inspector.");
            return;
        }

        FieldInfo rendererDataListField = urpAsset.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (rendererDataListField != null)
        {
            ScriptableRendererData[] rendererDataList = rendererDataListField.GetValue(urpAsset) as ScriptableRendererData[];
            
            if (rendererDataList != null && rendererDataList.Length > 0)
            {
                ScriptableRendererData activeRenderer = rendererDataList[0]; 
                pixelFeature = activeRenderer.rendererFeatures.OfType<PixelizeRenderFeature>().FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Configures the slider bounds and syncs the UI with the current rendering state.
    /// </summary>
    private void InitializeUI()
    {
        if (pixelSlider == null) return;

        // Force the slider to use only integers from 0 to 10
        pixelSlider.minValue = 0;
        pixelSlider.maxValue = 10;
        pixelSlider.wholeNumbers = true;

        if (pixelFeature != null)
        {
            // Sync slider value with the actual active setting
            pixelSlider.value = pixelFeature.settings.isEnabled ? pixelFeature.settings.pixelScale : 0;
        }

        // Manually trigger the text update once on start
        UpdatePixelationState(pixelSlider.value);

        // Subscribe to slider changes
        pixelSlider.onValueChanged.AddListener(UpdatePixelationState);
    }

    /// <summary>
    /// Updates the text and the render feature when the slider is dragged.
    /// </summary>
    /// <param name="value">The new value from the slider.</param>
    private void UpdatePixelationState(float value)
    {
        int scale = Mathf.RoundToInt(value);

        if (pixelScaleText != null)
        {
            pixelScaleText.text = scale == 0 ? "Pixel Scale: OFF" : $"Pixel Scale: {scale}x";
        }

        if (pixelFeature != null)
        {
            if (scale == 0)
            {
                pixelFeature.settings.isEnabled = false;
            }
            else
            {
                pixelFeature.settings.isEnabled = true;
                pixelFeature.settings.pixelScale = scale;
            }
        }
    }

    private void OnDestroy()
    {
        if (pixelSlider != null)
        {
            pixelSlider.onValueChanged.RemoveListener(UpdatePixelationState);
        }
    }
}