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

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // Visual rotation and movement controls for the camera
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            // Horizontally rotate (Yaw) around the world
            transform.Rotate(Vector3.up, mouseDelta.x * lookSensitivity, Space.World);
            // Vertically rotate (Pitch) relative to the camera itself
            transform.Rotate(Vector3.right, -mouseDelta.y * lookSensitivity, Space.Self);

            // Zero out the Z-axis (Roll) to maintain a stable framing
            Vector3 currentAngles = transform.eulerAngles;
            transform.eulerAngles = new Vector3(currentAngles.x, currentAngles.y, 0f);
        }

        // WASD movement controls for the camera
        Vector3 movement = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) movement += transform.up;
        if (Keyboard.current.sKey.isPressed) movement -= transform.up;
        if (Keyboard.current.aKey.isPressed) movement -= transform.right;
        if (Keyboard.current.dKey.isPressed) movement += transform.right;

        if (movement.magnitude > 1f) movement.Normalize();
        
        transform.position += movement * panSpeed * Time.deltaTime;

        // Mouse scroll wheel zoom control
        float scroll = Mouse.current.scroll.y.ReadValue();
        if (scroll != 0f)
        {
            transform.position += transform.forward * scroll * scrollSpeed * Time.deltaTime;
        }
    }
}