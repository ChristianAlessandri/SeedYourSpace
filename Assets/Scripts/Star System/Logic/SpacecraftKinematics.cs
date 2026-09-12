using UnityEngine;

/// <summary>
/// Deterministic kinematic controller for interplanetary travel.
/// Handles surface-to-surface positioning, arc trajectories, and docking visibility.
/// </summary>
public class SpacecraftKinematics : MonoBehaviour
{
    private Transform startBody;
    private Transform targetBody;
    private float travelSpeed;
    private float timeOffset;
    private float arcHeight;
    private float startRadius;
    private float targetRadius;

    private Renderer[] shipRenderers;

    /// <summary>
    /// Initializes the interplanetary route with body radii for surface calculations.
    /// </summary>
    /// <param name="start">The starting celestial body transform.</param>
    /// <param name="target">The target celestial body transform.</param>
    /// <param name="speed">The normalized travel speed (0 to 1).</param>
    /// <param name="offset">The time offset for the travel cycle.</param>
    /// <param name="arc">The height of the arc trajectory above the solar plane.</param>
    /// <param name="sRad">The radius of the starting body for surface positioning.</param>
    /// <param name="tRad">The radius of the target body for surface positioning.</param>
    public void InitializeInterplanetaryPath(Transform start, Transform target, float speed, float offset, float arc, float sRad, float tRad)
    {
        startBody = start;
        targetBody = target;
        travelSpeed = speed;
        timeOffset = offset;
        arcHeight = arc;
        startRadius = sRad;
        targetRadius = tRad;

        shipRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        if (startBody == null || targetBody == null) return;

        // Continuous round-trip progress (0 to 1 and back)
        float rawT = Mathf.PingPong(Time.unscaledTime * travelSpeed + timeOffset, 1f);
        float nextRawT = Mathf.PingPong((Time.unscaledTime + 0.016f) * travelSpeed + timeOffset, 1f);

        // Disappear near the planets (rawT < 0.01 or rawT > 0.99) to simulate landing/docking
        bool isDocked = (rawT < 0.01f || rawT > 0.99f);
        SetShipVisible(!isDocked);

        if (isDocked)
        {
            // Keep the ship parked at the respective surface while hidden
            transform.position = rawT < 0.01f ? GetStartPosition() : GetTargetPosition();
            return;
        }

        // Apply smooth acceleration and deceleration
        float t = Mathf.SmoothStep(0f, 1f, rawT);
        float nextT = Mathf.SmoothStep(0f, 1f, nextRawT);

        Vector3 currentPos = CalculatePosition(t);
        Vector3 nextPos = CalculatePosition(nextT);

        transform.position = currentPos;

        // Point the spaceship in the direction of travel
        Vector3 flightDirection = (nextPos - currentPos).normalized;
        if (flightDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flightDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.unscaledDeltaTime * 5f);
        }
    }

    private Vector3 GetStartPosition()
    {
        Vector3 dir = (targetBody.position - startBody.position).normalized;
        return startBody.position + dir * startRadius;
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 dir = (targetBody.position - startBody.position).normalized;
        return targetBody.position - dir * targetRadius;
    }

    private Vector3 CalculatePosition(float t)
    {
        Vector3 startPos = GetStartPosition();
        Vector3 targetPos = GetTargetPosition();

        // Interpolate linearly between the surfaces
        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);
        
        // Add a vertical parabolic arc so they fly over the solar plane
        float currentArc = Mathf.Sin(t * Mathf.PI) * arcHeight;
        linearPos.y += currentArc;
        
        return linearPos;
    }

    private void SetShipVisible(bool visible)
    {
        if (shipRenderers == null) return;
        foreach (var r in shipRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }
}