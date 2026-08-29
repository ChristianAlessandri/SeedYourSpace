using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the global time scale of the game, allowing for speed adjustments and pausing.
/// </summary>
public class TimeController : MonoBehaviour
{
    [Header("UI References")]
    public Slider timeSlider;
    public TextMeshProUGUI timeText;

    void Start()
    {
        // Set the slider's range and initial value
        timeSlider.minValue = 0f;
        timeSlider.maxValue = 100f;
        timeSlider.value = 1f;

        // Subscribe to the slider's value change event
        timeSlider.onValueChanged.AddListener(UpdateTimeScale);
        
        // Initialize the time scale and UI text
        UpdateTimeScale(timeSlider.value);
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        bool plusPressed = UnityEngine.InputSystem.Keyboard.current.equalsKey.wasPressedThisFrame || 
                           UnityEngine.InputSystem.Keyboard.current.numpadPlusKey.wasPressedThisFrame;

        if (plusPressed)
        {
            timeSlider.value = Mathf.Clamp(timeSlider.value + 1f, timeSlider.minValue, timeSlider.maxValue);
        }
        
        bool minusPressed = UnityEngine.InputSystem.Keyboard.current.minusKey.wasPressedThisFrame || 
                            UnityEngine.InputSystem.Keyboard.current.numpadMinusKey.wasPressedThisFrame;

        if (minusPressed)
        {
            timeSlider.value = Mathf.Clamp(timeSlider.value - 1f, timeSlider.minValue, timeSlider.maxValue);
        }
    }

    private void UpdateTimeScale(float newSpeed)
    {
        Time.timeScale = newSpeed;
        
        if (newSpeed == 0f)
            timeText.text = "Time: PAUSED";
        else
            timeText.text = $"Time: {newSpeed:F1}x";
    }
}