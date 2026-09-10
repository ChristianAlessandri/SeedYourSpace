using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Listens for clicks on the Atlas map and calculates the targeted UV coordinates.
/// </summary>
public class MapTargetingHUD : MonoBehaviour, IPointerClickHandler
{
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (rectTransform == null) return;

        // Convert the screen click position into local coordinates within the RectTransform
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 localCursor);

        // Normalize the local coordinates to a 0.0 - 1.0 UV space
        Vector2 normalizedUV = new Vector2(
            (localCursor.x - rectTransform.rect.x) / rectTransform.rect.width,
            (localCursor.y - rectTransform.rect.y) / rectTransform.rect.height
        );

        // Convert UV to Latitude/Longitude format for display
        float longitude = (normalizedUV.x * 360f) - 180f;
        float latitude = (normalizedUV.y * 180f) - 90f;

        Debug.Log($"[Targeting System] Clicked UV: {normalizedUV:F3} | Lat: {latitude:F1}°, Lon: {longitude:F1}°");
    }
}