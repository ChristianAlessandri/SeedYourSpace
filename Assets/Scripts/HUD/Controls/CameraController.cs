using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera controller that allows for free movement and rotation in a 3D space.
/// Supports WASD movement, mouse look, scroll zoom, and target focusing (F key).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 50f;
    
    [Header("Rotation Settings")]
    public float lookSensitivity = 0.2f;

    [Header("Focus System")]
    public SystemListHUD systemListHUD;
    public MoonListHUD moonListHUD;
    [Tooltip("How far the camera should be placed based on the body's radius.")]
    public float focusDistanceMultiplier = 3.5f;

    private float pitch = 0f;
    private float yaw = 0f;

    // Target tracking
    private Transform activeTarget;
    private float activeTargetRadius = 1f;

    private void Start()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.farClipPlane = 1000000f; 
        }

        Vector3 initialAngles = transform.eulerAngles;
        pitch = initialAngles.x;
        yaw = initialAngles.y;
    }

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

    // Event Handlers
    private void OnStarSelected(StarData data) => SetTarget(data.name);
    private void OnPlanetSelected(PlanetData data) => SetTarget(data.name);
    private void OnMoonSelected(MoonData data) => SetTarget(data.name);

    /// <summary>
    /// Sets the active target based on the provided body name. If the body is found in the scene, it updates the active target and its radius for focusing.
    /// </summary>
    /// <param name="bodyName">The name of the body to set as the target.</param>
    private void SetTarget(string bodyName)
    {
        GameObject targetObj = GameObject.Find(bodyName);
        if (targetObj != null)
        {
            activeTarget = targetObj.transform;
            activeTargetRadius = targetObj.transform.localScale.y;
        }
    }

    /// <summary>
    /// Clears the active target, typically called when clicking empty space.
    /// </summary>
    public void ClearTarget()
    {
        activeTarget = null;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        HandleFocusInput();
        HandleRotation();
        HandleMovement();
    }

    /// <summary>
    /// Handles the input for focusing on the active target when the F key is pressed.
    /// If a target is set, the camera will move to a position that frames the target based on its radius and the focus distance multiplier.
    /// </summary>
    private void HandleFocusInput()
    {
        // FOCUS ACTION (Press F)
        if (Keyboard.current.fKey.wasPressedThisFrame && activeTarget != null)
        {
            FocusOnTarget();
        }
    }

    /// <summary>
    /// Handles camera rotation based on mouse movement when the middle mouse button is pressed.
    /// The camera's pitch is clamped to prevent flipping over.
    /// </summary>
    private void HandleRotation()
    {
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            yaw += mouseDelta.x * lookSensitivity;
            pitch -= mouseDelta.y * lookSensitivity;

            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
    }

    /// <summary>
    /// Handles camera movement based on WASD input, allowing for free navigation in the scene.
    /// Movement speed is scaled by panSpeed and Time.unscaledDeltaTime to ensure consistent movement regardless of time scale changes.
    /// </summary>
    private void HandleMovement()
    {
        Vector3 movement = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) movement += transform.forward;
        if (Keyboard.current.sKey.isPressed) movement -= transform.forward;
        if (Keyboard.current.aKey.isPressed) movement -= transform.right;
        if (Keyboard.current.dKey.isPressed) movement += transform.right;

        if (movement.magnitude > 1f) movement.Normalize();
        
        transform.position += movement * panSpeed * Time.unscaledDeltaTime;
    }

    /// <summary>
    /// Teleports the camera to frame the target perfectly without changing rotation.
    /// </summary>
    private void FocusOnTarget()
    {
        float safeDistance = activeTargetRadius * focusDistanceMultiplier;
        transform.position = activeTarget.position - (transform.forward * safeDistance);
    }
}