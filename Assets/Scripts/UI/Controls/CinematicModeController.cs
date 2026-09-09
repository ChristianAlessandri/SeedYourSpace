using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the cinematic time-lapse mode. 
/// Handles hiding UI, accelerating time, automating camera orbit, and flattening camera pitch.
/// Dynamically orbits and tracks the currently selected moving celestial body.
/// </summary>
public class CinematicModeController : MonoBehaviour
{
    [Header("System References")]
    public Canvas mainUICanvas;
    public CameraController cameraController;
    public TimeController timeController;

    [Header("Selection UI")]
    public SystemListHUD systemListHUD;
    public MoonListHUD moonListHUD;

    [Header("Cinematic Settings")]
    public float cinematicTimeSpeed = 50f;
    public float cameraOrbitSpeed = 2f;
    public float cinematicPitch = 10f;
    public float framingDistanceMultiplier = 4f;

    private bool isCinematicActive = false;
    private float storedTimeSpeed = 1f;
    
    private Transform centralStarTransform;
    private Transform selectedTarget;
    private Transform activePivot;
    
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    
    // Stores the relative distance vector to maintain a perfect lock on moving bodies
    private Vector3 currentOrbitOffset; 

    private void OnEnable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnStarSelected.AddListener(OnStarSelected);
            systemListHUD.OnPlanetSelected.AddListener(OnPlanetSelected);
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.AddListener(OnMoonSelected);
        }
    }

    private void OnDisable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnStarSelected.RemoveListener(OnStarSelected);
            systemListHUD.OnPlanetSelected.RemoveListener(OnPlanetSelected);
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.RemoveListener(OnMoonSelected);
        }
    }

    private void OnStarSelected(StarData data) => SetTarget(data.name);
    private void OnPlanetSelected(PlanetData data) => SetTarget(data.name);
    private void OnMoonSelected(MoonData data) => SetTarget(data.name);

    private void SetTarget(string bodyName)
    {
        GameObject targetObj = GameObject.Find(bodyName);
        if (targetObj != null)
        {
            selectedTarget = targetObj.transform;
        }
    }

    public void ClearTarget()
    {
        selectedTarget = null;
    }

    public void ActivateCinematicMode()
    {
        if (isCinematicActive) return;

        LocateCentralStar(); 

        activePivot = selectedTarget != null ? selectedTarget : centralStarTransform;

        if (activePivot == null)
        {
            Debug.LogWarning("Cinematic Mode failed: No valid pivot found.");
            return;
        }

        isCinematicActive = true;

        if (mainUICanvas != null) mainUICanvas.enabled = false;

        if (cameraController != null) 
        {
            cameraController.enabled = false;
            SetupCinematicCamera();
        }

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
    }

    private void LateUpdate()
    {
        if (!isCinematicActive) return;
        ExecuteCinematicCameraMovement();
    }

    private void HandleExitInput()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeactivateCinematicMode();
        }
    }

    /// <summary>
    /// Rotates the offset vector and applies it to the pivot's current moving position,
    /// ensuring the camera flawlessly tracks the celestial body through space.
    /// </summary>
    private void ExecuteCinematicCameraMovement()
    {
        if (activePivot == null || cameraController == null) return;

        Transform camTransform = cameraController.transform;
        
        // Rotate the offset vector by the orbit speed around the global up axis
        Quaternion rotationStep = Quaternion.AngleAxis(cameraOrbitSpeed * Time.unscaledDeltaTime, Vector3.up);
        currentOrbitOffset = rotationStep * currentOrbitOffset;

        // Apply the newly rotated offset to the current position of the moving body
        camTransform.position = activePivot.position + currentOrbitOffset;
        camTransform.LookAt(activePivot);
    }

    private void SetupCinematicCamera()
    {
        Transform camTransform = cameraController.transform;
        
        savedCameraPosition = camTransform.position;
        savedCameraRotation = camTransform.rotation;

        float currentDistance = Vector3.Distance(camTransform.position, activePivot.position);
        float targetRadius = activePivot.localScale.y;
        
        float minReasonableDistance = targetRadius * framingDistanceMultiplier;
        float maxReasonableDistance = targetRadius * framingDistanceMultiplier * 3f;
        float distanceToPivot = Mathf.Clamp(currentDistance, minReasonableDistance, maxReasonableDistance);

        Vector3 flatDirection = camTransform.position - activePivot.position;
        flatDirection.y = 0f;
        if (flatDirection.magnitude < 0.1f) flatDirection = Vector3.forward;
        flatDirection = flatDirection.normalized;

        float pitchInRadians = cinematicPitch * Mathf.Deg2Rad;
        float horizontalDistance = distanceToPivot * Mathf.Cos(pitchInRadians);
        float verticalHeight = distanceToPivot * Mathf.Sin(pitchInRadians);

        Vector3 newCinematicPosition = activePivot.position + (flatDirection * horizontalDistance) + (Vector3.up * verticalHeight);
        camTransform.position = newCinematicPosition;
        camTransform.LookAt(activePivot);

        // Store the relative vector right after setup so ExecuteCinematicCameraMovement can rotate it
        currentOrbitOffset = camTransform.position - activePivot.position;
    }

    private void DeactivateCinematicMode()
    {
        isCinematicActive = false;

        if (mainUICanvas != null) mainUICanvas.enabled = true;

        if (cameraController != null) 
        {
            cameraController.transform.position = savedCameraPosition;
            cameraController.transform.rotation = savedCameraRotation;
            cameraController.enabled = true;
        }

        if (timeController != null)
        {
            timeController.timeSlider.value = storedTimeSpeed;
        }
    }

    private void LocateCentralStar()
    {
        if (centralStarTransform != null) return;

        CelestialBody[] allBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        foreach (CelestialBody body in allBodies)
        {
            if (body.centralStar == null)
            {
                centralStarTransform = body.transform;
                break;
            }
        }
    }
}