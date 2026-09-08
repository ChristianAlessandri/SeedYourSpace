using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Handles 3D raycasting to select celestial bodies by clicking them in the scene.
/// Acts as a bridge between physical interaction and the UI selection logic.
/// </summary>
public class MouseInteractionController : MonoBehaviour
{
    [Header("Camera Reference")]
    public Camera mainCamera;

    [Header("UI Managers")]
    public SystemListHUD systemListHUD;
    public MoonListHUD moonListHUD;
    public CelestialBodyDetailHUD detailHUD;
    public SelectionVisualizer visualizer;
    public CameraController cameraController;

    private void Update()
    {
        // Ensure input system and mouse exist
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        // Prevent clicking through the UI (e.g., clicking a button shouldn't click the space behind it)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        ProcessClick();
    }

    /// <summary>
    /// Executes the raycast into the 3D scene to find a target.
    /// </summary>
    private void ProcessClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            HandleBodySelection(hit.collider.gameObject.name);
        }
        else
        {
            DeselectEverything();
        }
    }

    /// <summary>
    /// Routes the selected hit name to the appropriate UI lists.
    /// </summary>
    /// <param name="hitName">The name of the clicked physics collider.</param>
    private void HandleBodySelection(string hitName)
    {
        bool selectedInSystem = systemListHUD != null && systemListHUD.TrySelectBody(hitName);
        bool selectedInMoons = false;
        
        // If it wasn't a star or planet, check if it's one of the currently visible moons
        if (!selectedInSystem)
        {
            selectedInMoons = moonListHUD != null && moonListHUD.TrySelectBody(hitName);
        }

        // If we clicked an object but it's not selectable (e.g. rings without data)
        if (!selectedInSystem && !selectedInMoons)
        {
            DeselectEverything();
        }
    }

    /// <summary>
    /// Resets all visual states and hides context panels.
    /// </summary>
    private void DeselectEverything()
    {
        if (systemListHUD != null) systemListHUD.DeselectAll();
        
        if (moonListHUD != null) 
        {
            moonListHUD.DeselectAll();
            moonListHUD.HideCompletely();
        }
        
        if (detailHUD != null) detailHUD.ClearDetails();
        if (visualizer != null) visualizer.ClearVisuals();
        if (cameraController != null) cameraController.ClearTarget();
    }
}