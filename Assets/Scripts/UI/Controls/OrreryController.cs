using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the global visibility of all orbital paths in the star system.
/// Uses world-space calculations to prevent inheriting parent rotation and scaling artifacts.
/// </summary>
public class OrreryController : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("Material used for the global orrery lines. Should support transparency.")]
    public Material orreryLineMaterial;
    
    public Color planetOrbitColor = new Color(1f, 1f, 1f, 0.25f);
    public Color moonOrbitColor = new Color(1f, 1f, 1f, 0.15f);
    
    public float planetLineWidth = 0.5f;
    public float moonLineWidth = 0.15f;

    [Header("UI References")]
    public TextMeshProUGUI buttonText;
    
    private const int ORBIT_SEGMENTS = 120;
    
    private class OrreryOrbitData
    {
        public LineRenderer lineRenderer;
        public CelestialBody targetBody;
    }

    private List<OrreryOrbitData> activeOrreryOrbits = new List<OrreryOrbitData>();
    private bool isOrreryActive = false;

    /// <summary>
    /// Scans the scene for all CelestialBody instances and builds their orbit lines.
    /// Call this immediately after VisualDioramaBuilder.BuildUniverse() finishes.
    /// </summary>
    public void InitializeOrrery()
    {
        ClearOrrery();

        CelestialBody[] allBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);

        foreach (CelestialBody body in allBodies)
        {
            if (body.centralStar == null || body.semiMajorAxis <= 0f) continue;

            CreateOrbitLine(body);
        }
    }

    /// <summary>
    /// Toggles the visibility of all generated orbital lines.
    /// </summary>
    /// <param name="state">True to show orbits, false to hide.</param>
    public void ToggleOrbits(bool state)
    {
        isOrreryActive = state;
        
        foreach (var orbit in activeOrreryOrbits)
        {
            if (orbit.lineRenderer != null)
            {
                orbit.lineRenderer.enabled = isOrreryActive;
            }
        }

        if (buttonText != null)
        {
            buttonText.text = isOrreryActive ? "ENABLED" : "DISABLED";
        }
    }

    /// <summary>
    /// Flips the current state of the orrery visibility for standard UI Buttons.
    /// </summary>
    public void ToggleOrbitsButtonClicked()
    {
        ToggleOrbits(!isOrreryActive);
    }

    /// <summary>
    /// Creates a world-space LineRenderer for a given celestial body.
    /// </summary>
    private void CreateOrbitLine(CelestialBody body)
    {
        GameObject orbitObj = new GameObject($"OrreryLine_{body.gameObject.name}");
        orbitObj.transform.SetParent(this.transform, false);

        LineRenderer line = orbitObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true; 
        line.loop = true;
        line.positionCount = ORBIT_SEGMENTS;
        
        if (orreryLineMaterial != null)
        {
            line.material = orreryLineMaterial;
        }
        
        // Determine styling based on whether it's a planet or a moon
        CelestialBody parentBody = body.centralStar.GetComponent<CelestialBody>();
        bool isMoon = parentBody != null && parentBody.centralStar != null;

        line.startWidth = isMoon ? moonLineWidth : planetLineWidth;
        line.endWidth = isMoon ? moonLineWidth : planetLineWidth;
        line.startColor = isMoon ? moonOrbitColor : planetOrbitColor;
        line.endColor = isMoon ? moonOrbitColor : planetOrbitColor;
        line.enabled = isOrreryActive;

        activeOrreryOrbits.Add(new OrreryOrbitData {
            lineRenderer = line,
            targetBody = body
        });
    }

    private void LateUpdate()
    {
        if (!isOrreryActive) return;

        foreach (var orbit in activeOrreryOrbits)
        {
            if (orbit.lineRenderer == null || orbit.targetBody == null || orbit.targetBody.centralStar == null) continue;

            UpdateOrbitPoints(orbit.targetBody, orbit.lineRenderer);
        }
    }

    /// <summary>
    /// Updates the positions of the orbit line segments in world space based on Keplerian parameters.
    /// </summary>
    private void UpdateOrbitPoints(CelestialBody body, LineRenderer line)
    {
        float semiMinorAxis = body.semiMajorAxis * Mathf.Sqrt(1f - body.eccentricity * body.eccentricity);
        Quaternion inclinationRotation = Quaternion.Euler(body.orbitalInclination, 0f, 0f);
        Vector3 centerPos = body.centralStar.position;

        for (int i = 0; i < ORBIT_SEGMENTS; i++)
        {
            float eccentricAnomaly = ((float)i / ORBIT_SEGMENTS) * Mathf.PI * 2f;
            
            float xPos = body.semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - body.eccentricity);
            float zPos = semiMinorAxis * Mathf.Sin(eccentricAnomaly);
            Vector3 flatOrbitalPosition = new Vector3(xPos, 0f, zPos);

            Vector3 tiltedOrbitalPosition = inclinationRotation * flatOrbitalPosition;
            Vector3 finalPoint = centerPos + tiltedOrbitalPosition;
            
            line.SetPosition(i, finalPoint);
        }
    }

    /// <summary>
    /// Cleans up existing data.
    /// </summary>
    private void ClearOrrery()
    {
        foreach (var orbit in activeOrreryOrbits)
        {
            if (orbit.lineRenderer != null) Destroy(orbit.lineRenderer.gameObject);
        }
        activeOrreryOrbits.Clear();
    }
}
