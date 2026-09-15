using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeController : MonoBehaviour
{
    [Header("UI References")]
    public Slider timeSlider;
    public TextMeshProUGUI timeComparisonText;
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
        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
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
        
        timeText.text = $"Time: {newSpeed:F1}x";
        timeComparisonText.text = $"Real: 1s | Simulation: {newSpeed*24:F0}h";
    }
}