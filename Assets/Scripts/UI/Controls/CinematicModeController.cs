using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the cinematic time-lapse mode. 
/// Handles hiding UI, accelerating time, automating camera orbit, and flattening camera pitch.
/// Respects SRP by acting as an orchestrator over existing controllers.
/// </summary>
public class CinematicModeController : MonoBehaviour
{
    [Header("System References")]
    [Tooltip("The main UI Canvas to hide during cinematic mode.")]
    public Canvas mainUICanvas;
    
    [Tooltip("Reference to the existing camera controller.")]
    public CameraController cameraController;
    
    [Tooltip("Reference to the time controller to manipulate simulation speed.")]
    public TimeController timeController;

    [Header("Cinematic Settings")]
    [Tooltip("The target time scale multiplier during cinematic mode (e.g., 50x).")]
    public float cinematicTimeSpeed = 50f;
    
    [Tooltip("Degrees per second the camera will orbit around the central star.")]
    public float cameraOrbitSpeed = 2f;

    [Tooltip("The forced pitch angle (in degrees) to look at the horizon during cinematic mode.")]
    public float cinematicPitch = 10f;

    private bool isCinematicActive = false;
    private float storedTimeSpeed = 1f;
    private Transform centralStarTransform;

    // State restoration variables
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;

    /// <summary>
    /// Activates the cinematic time-lapse mode. Hook this to your UI Button.
    /// </summary>
    public void ActivateCinematicMode()
    {
        if (isCinematicActive) return;

        if (!LocateCentralStar())
        {
            Debug.LogWarning("Cinematic Mode failed: Central Star not found.");
            return;
        }

        isCinematicActive = true;

        // Hide the entire UI Canvas efficiently
        if (mainUICanvas != null) mainUICanvas.enabled = false;

        // Disable manual camera controls and setup cinematic position
        if (cameraController != null) 
        {
            cameraController.enabled = false;
            SetupCinematicCamera();
        }

        // Store the current time speed and force the cinematic acceleration
        if (timeController != null)
        {
            storedTimeSpeed = timeController.timeSlider.value;
            timeController.timeSlider.value = cinematicTimeSpeed;
        }
    }

    private void Update()
    {
        if (!isCinematicActive) return;

        HandleExitInput();
        ExecuteCinematicCameraMovement();
    }

    /// <summary>
    /// Listens for the ESC key to gracefully exit the cinematic mode.
    /// </summary>
    private void HandleExitInput()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeactivateCinematicMode();
        }
    }

    /// <summary>
    /// Smoothly orbits the camera around the central star while keeping it focused.
    /// </summary>
    private void ExecuteCinematicCameraMovement()
    {
        if (centralStarTransform == null || cameraController == null) return;

        Transform camTransform = cameraController.transform;
        
        // Orbit around the star using unscaled delta time
        camTransform.RotateAround(centralStarTransform.position, Vector3.up, cameraOrbitSpeed * Time.unscaledDeltaTime);
        
        // Ensure the camera always perfectly frames the star
        camTransform.LookAt(centralStarTransform);
    }

    /// <summary>
    /// Calculates and applies a low-angle horizon shot for the cinematic view,
    /// while preserving the camera's current distance from the star.
    /// </summary>
    private void SetupCinematicCamera()
    {
        Transform camTransform = cameraController.transform;
        
        // Save state for later restoration
        savedCameraPosition = camTransform.position;
        savedCameraRotation = camTransform.rotation;

        float distanceToStar = Vector3.Distance(camTransform.position, centralStarTransform.position);
        
        // Get the current flat direction towards the star to avoid sudden horizontal jumps
        Vector3 flatDirection = camTransform.position - centralStarTransform.position;
        flatDirection.y = 0f;
        if (flatDirection.magnitude < 0.1f) flatDirection = Vector3.forward;
        flatDirection = flatDirection.normalized;

        // Calculate new height and horizontal distance based on the desired cinematic pitch
        float pitchInRadians = cinematicPitch * Mathf.Deg2Rad;
        float horizontalDistance = distanceToStar * Mathf.Cos(pitchInRadians);
        float verticalHeight = distanceToStar * Mathf.Sin(pitchInRadians);

        // Apply new position and look directly at the star
        Vector3 newCinematicPosition = centralStarTransform.position + (flatDirection * horizontalDistance) + (Vector3.up * verticalHeight);
        camTransform.position = newCinematicPosition;
        camTransform.LookAt(centralStarTransform);
    }

    /// <summary>
    /// Restores all systems to their previous state before the cinematic mode was engaged.
    /// </summary>
    private void DeactivateCinematicMode()
    {
        isCinematicActive = false;

        // Restore UI visibility
        if (mainUICanvas != null) mainUICanvas.enabled = true;

        // Restore camera position, rotation, and manual controls
        if (cameraController != null) 
        {
            cameraController.transform.position = savedCameraPosition;
            cameraController.transform.rotation = savedCameraRotation;
            cameraController.enabled = true;
        }

        // Restore the previous time simulation speed
        if (timeController != null)
        {
            timeController.timeSlider.value = storedTimeSpeed;
        }
    }

    /// <summary>
    /// Dynamically finds the central star of the generated system.
    /// </summary>
    /// <returns>True if the central star was successfully found.</returns>
    private bool LocateCentralStar()
    {
        CelestialBody[] allBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        
        foreach (CelestialBody body in allBodies)
        {
            if (body.centralStar == null)
            {
                centralStarTransform = body.transform;
                return true;
            }
        }
        
        return false;
    }
}