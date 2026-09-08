using UnityEngine;

/// <summary>
/// Computational kinematic engine for celestial bodies.
/// Handles Keplerian orbital revolution in 3D space and precise axial rotation.
/// </summary>
public class CelestialBody : MonoBehaviour
{
    [Header("Simulation Time")]
    public static float globalTimeScale = 1.0f; 

    [Header("Orbital Parameters")]
    public float semiMajorAxis;
    public float eccentricity;
    public float orbitalInclination;
    public Transform centralStar; 

    private float revolutionSpeed; 
    private float rotationSpeed;   
    private float currentMeanAnomaly = 0f;

    public void InitializeKinematics(float distance, float ecc, float inclination, Transform focalPoint, float revPeriodDays, float rotPeriodHours)
    {
        semiMajorAxis = distance;
        eccentricity = ecc;
        orbitalInclination = inclination;
        centralStar = focalPoint;

        if (revPeriodDays > 0)
            revolutionSpeed = (Mathf.PI * 2f) / revPeriodDays;
        else
            revolutionSpeed = 0f;

        if (rotPeriodHours > 0)
        {
            float rotPeriodDays = rotPeriodHours / 24f;
            rotationSpeed = 360f / rotPeriodDays;
        }

        currentMeanAnomaly = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float simulatedDeltaTime = Time.deltaTime * globalTimeScale;

        // Axial Rotation
        transform.Rotate(Vector3.up, -rotationSpeed * simulatedDeltaTime, Space.Self);

        // Orbital Revolution
        if (centralStar != null && semiMajorAxis > 0)
        {
            currentMeanAnomaly += revolutionSpeed * simulatedDeltaTime;
            float eccentricAnomaly = currentMeanAnomaly + eccentricity * Mathf.Sin(currentMeanAnomaly);

            float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
            
            // Calculate the position in the orbital plane (X-Z plane)
            float xPos = semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - eccentricity);
            float zPos = semiMinorAxis * Mathf.Sin(eccentricAnomaly);
            Vector3 flatOrbitalPosition = new Vector3(xPos, 0f, zPos);

            // Apply the orbital inclination by rotating around the X-axis
            Quaternion inclinationRotation = Quaternion.Euler(orbitalInclination, 0f, 0f);
            Vector3 tiltedOrbitalPosition = inclinationRotation * flatOrbitalPosition;

            // Apply the final position relative to the central body
            transform.position = centralStar.position + tiltedOrbitalPosition;
        }
    }
}