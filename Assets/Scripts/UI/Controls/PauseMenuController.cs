using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject HUDContainer;
    public GameObject pauseMenuPanel;
    public Button backToMenuButton;
    public Button closeMenuButton;

    [Header("System References")]
    public TimeController timeController;
    public CinematicModeController cinematicController;

    private bool isPaused = false;
    private float savedTimeSpeed = 1f;

    private void Start()
    {
        pauseMenuPanel.SetActive(false);
        HUDContainer.SetActive(true);

        backToMenuButton.onClick.AddListener(ReturnToHub);
        closeMenuButton.onClick.AddListener(ResumeGame);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (cinematicController != null && cinematicController.IsCinematicActive)
            {
                cinematicController.DeactivateCinematicMode();
                return;
            }

            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        HUDContainer.SetActive(false);

        if (timeController != null)
        {
            savedTimeSpeed = timeController.timeSlider.value;
            timeController.timeSlider.value = 0f;
        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseMenuPanel.SetActive(false);
        HUDContainer.SetActive(true);

        if (timeController != null)
        {
            timeController.timeSlider.value = savedTimeSpeed;
        }
    }

    private void ReturnToHub()
    {
        if (timeController != null)
        {
            timeController.timeSlider.value = 1f;
        }

        SceneManager.LoadScene("MainMenuScene"); 
    }
}