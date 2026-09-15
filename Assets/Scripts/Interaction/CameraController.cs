using UnityEngine;
using UnityEngine.InputSystem;

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

    private void SetTarget(string bodyName)
    {
        GameObject targetObj = GameObject.Find(bodyName);
        if (targetObj != null)
        {
            activeTarget = targetObj.transform;
            activeTargetRadius = targetObj.transform.localScale.y;
        }
    }

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

    private void HandleFocusInput()
    {
        // FOCUS ACTION (Press F)
        if (Keyboard.current.fKey.wasPressedThisFrame && activeTarget != null)
        {
            FocusOnTarget();
        }
    }

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

    private void FocusOnTarget()
    {
        float safeDistance = activeTargetRadius * focusDistanceMultiplier;
        transform.position = activeTarget.position - (transform.forward * safeDistance);
    }
}