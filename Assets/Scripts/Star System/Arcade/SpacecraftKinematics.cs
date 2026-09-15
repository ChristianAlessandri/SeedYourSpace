using UnityEngine;

public class SpacecraftKinematics : MonoBehaviour
{
    private Transform startBody;
    private Transform targetBody;
    private float travelSpeed;
    private float timeOffset;
    private float arcHeight;

    private Renderer[] shipRenderers;

    public void InitializeInterplanetaryPath(Transform start, Transform target, float speed, float offset, float arc)
    {
        startBody = start;
        targetBody = target;
        travelSpeed = speed;
        timeOffset = offset;
        arcHeight = arc;

        shipRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        if (startBody == null || targetBody == null) return;

        float rawT = Mathf.PingPong(Time.time * travelSpeed + timeOffset, 1f);
        float nextRawT = Mathf.PingPong((Time.time + 0.016f) * travelSpeed + timeOffset, 1f);

        bool isDocked = (rawT < 0.01f || rawT > 0.99f);
        SetShipVisible(!isDocked);

        if (isDocked)
        {
            transform.position = rawT < 0.01f ? GetStartPosition() : GetTargetPosition();
            return;
        }

        float t = Mathf.SmoothStep(0f, 1f, rawT);
        float nextT = Mathf.SmoothStep(0f, 1f, nextRawT);

        Vector3 currentPos = CalculatePosition(t);
        Vector3 nextPos = CalculatePosition(nextT);

        transform.position = currentPos;

        Vector3 flightDirection = (nextPos - currentPos).normalized;
        if (flightDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flightDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        // Dynamically adjust the spaceship's scale based on the current sizes of the start and target celestial bodies
        float dynamicMinDiameter = Mathf.Min(startBody.localScale.x, targetBody.localScale.x);
        transform.localScale = Vector3.one * (dynamicMinDiameter * 0.5f);
    }

    private Vector3 GetStartPosition()
    {
        Vector3 dir = (targetBody.position - startBody.position).normalized;
        // Calculate the radius by dynamically reading the current scale of the start body
        return startBody.position + dir * (startBody.localScale.x * 0.5f);
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 dir = (targetBody.position - startBody.position).normalized;
        // Calculate the radius by dynamically reading the current scale of the target body
        return targetBody.position - dir * (targetBody.localScale.x * 0.5f);
    }

    private Vector3 CalculatePosition(float t)
    {
        Vector3 startPos = GetStartPosition();
        Vector3 targetPos = GetTargetPosition();

        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);
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