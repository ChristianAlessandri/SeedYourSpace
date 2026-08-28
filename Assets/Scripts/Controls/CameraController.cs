using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera controller that allows for free movement and rotation in a 3D space.
/// Supports WASD movement, mouse look, and scroll zoom.
/// </summary>
public class SimpleCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 50f;
    public float scrollSpeed = 500f;
    
    [Header("Rotation Settings")]
    public float lookSensitivity = 0.2f;

    private float pitch = 0f;
    private float yaw = 0f;

    private void Start()
    {
        // Prevent distant planets from disappearing by extending the far clipping plane
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.farClipPlane = 1000000f; // Render up to 1 million units away
        }

        // Initialize internal rotation state to match the camera's initial rotation
        Vector3 initialAngles = transform.eulerAngles;
        pitch = initialAngles.x;
        yaw = initialAngles.y;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // Visual rotation and movement controls for the camera
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            yaw += mouseDelta.x * lookSensitivity;
            pitch -= mouseDelta.y * lookSensitivity;

            // Clamp pitch to prevent gimbal lock (camera flipping upside down)
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            // Apply rotation via Euler angles, which intrinsically keeps the Z-axis (roll) at 0
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        // WASD movement controls for the camera (Forward/Backward/Left/Right)
        Vector3 movement = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) movement += transform.forward;
        if (Keyboard.current.sKey.isPressed) movement -= transform.forward;
        if (Keyboard.current.aKey.isPressed) movement -= transform.right;
        if (Keyboard.current.dKey.isPressed) movement += transform.right;

        if (movement.magnitude > 1f) movement.Normalize();
        
        transform.position += movement * panSpeed * Time.deltaTime;

        // Mouse scroll wheel altitude control (Up/Down)
        float scroll = Mouse.current.scroll.y.ReadValue();
        if (scroll != 0f)
        {
            // We use normalized sign to prevent huge jumps from high-resolution scroll wheels,
            // but keep your scrollSpeed multiplier to manage the actual velocity.
            float normalizedScroll = Mathf.Sign(scroll);
            transform.position += transform.up * normalizedScroll * scrollSpeed * Time.deltaTime;
        }
    }
}