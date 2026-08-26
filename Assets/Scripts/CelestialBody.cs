using UnityEngine;

/// <summary>
/// Computational kinematic engine for celestial bodies.
/// Uses Keplerian equations to calculate elliptical orbits deterministically.
/// </summary>
public class CelestialBody : MonoBehaviour
{
    [Header("Orbital Parameters")]
    public float semiMajorAxis; // Distance from the star (a)
    public float eccentricity; // Elliptical distortion (e)
    public float orbitalSpeed; // Mean motion (n)

    [Header("Rendering")]
    public Transform centralStar; // The focal point of the orbit

    // Internal time tracker for the orbit
    private float currentMeanAnomaly = 0f;

    /// <summary>
    /// Initializes the orbital parameters.
    /// </summary>
    /// <param name="distance">The semi-major axis (distance from the star).</param>
    /// <param name="ecc">The eccentricity of the orbit.</param>
    /// <param name="starTransform">The Transform of the central star.</param>
    public void InitializeOrbit(float distance, float ecc, Transform starTransform)
    {
        semiMajorAxis = distance;
        eccentricity = ecc;
        centralStar = starTransform;

        // Simplified Kepler's Third Law to determine orbital speed
        // Speed is inversely proportional to the square root of the distance cubed
        float gravitationalConstant = 5f; // Arbitrary constant for simulation speed
        orbitalSpeed = gravitationalConstant / Mathf.Sqrt(Mathf.Pow(semiMajorAxis, 3));

        // Randomize starting position on the orbit to avoid planets lining up perfectly
        currentMeanAnomaly = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (centralStar == null) return;

        // Calculate Mean Anomaly (M) based on time
        currentMeanAnomaly += orbitalSpeed * Time.deltaTime;

        // Approximate Eccentric Anomaly (E) to simulate Kepler's Second Law (variable speed)
        float eccentricAnomaly = currentMeanAnomaly + eccentricity * Mathf.Sin(currentMeanAnomaly);

        // Calculate 2D coordinates on the orbital plane (X and Z)
        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
        
        float xPos = semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - eccentricity);
        float zPos = semiMinorAxis * Mathf.Sin(eccentricAnomaly);

        // Apply the calculated position relative to the central star
        transform.position = centralStar.position + new Vector3(xPos, 0f, zPos);
    }
}