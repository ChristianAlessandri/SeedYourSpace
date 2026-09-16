using UnityEngine;

public class SelectionVisualizer : MonoBehaviour
{
    [Header("Event Listeners")]
    public SystemListHUD systemListHUD;
    public MoonListHUD moonListHUD;

    [Header("Visual Settings")]
    public Color orbitColor = new Color(1f, 1f, 1f, 0.15f);
    public Color axisColor = new Color(1f, 0.2f, 0.2f, 1f); 
    public float planetOrbitLineWidth = 1.5f;
    public float moonOrbitLineWidth = 0.3f; // Thinner line to prevent swallowing small moons

    [Tooltip("Default material for the orbit line.")]
    public Material lineMaterial;
    [Tooltip("Material for the solid 3D axis sticks.")]
    public Material solidMaterial;

    private LineRenderer orbitLine;
    private GameObject northPoleStick;
    private GameObject southPoleStick;
    
    private CelestialBody activeBody;
    private const int ORBIT_SEGMENTS = 120;

    private void Awake()
    {
        InitializeVisuals();
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

    private void InitializeVisuals()
    {
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        }
        
        if (solidMaterial == null)
        {
            solidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            solidMaterial.color = axisColor;
        }

        GameObject orbitObj = new GameObject("Visualizer_OrbitLine");
        orbitObj.transform.SetParent(this.transform);
        orbitLine = orbitObj.AddComponent<LineRenderer>();
        orbitLine.material = lineMaterial;
        orbitLine.useWorldSpace = true;
        orbitLine.loop = true;
        orbitLine.positionCount = ORBIT_SEGMENTS;
        orbitLine.startColor = orbitColor;
        orbitLine.endColor = orbitColor;
        orbitLine.enabled = false;

        northPoleStick = CreateAxisStick("NorthPole");
        southPoleStick = CreateAxisStick("SouthPole");
    }

    private GameObject CreateAxisStick(string stickName)
    {
        GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.name = $"Visualizer_{stickName}";
        stick.transform.SetParent(this.transform);
        
        Destroy(stick.GetComponent<Collider>());
        
        Renderer mr = stick.GetComponent<Renderer>();
        mr.sharedMaterial = solidMaterial;
        
        stick.SetActive(false);
        return stick;
    }

    // Event Handlers passing their specific line widths
    private void OnStarSelected(StarData data) => TargetCelestialBody(data.name, 0f);
    private void OnPlanetSelected(PlanetData data) => TargetCelestialBody(data.name, planetOrbitLineWidth);
    private void OnMoonSelected(MoonData data) => TargetCelestialBody(data.name, moonOrbitLineWidth);

    private void TargetCelestialBody(string bodyName, float targetLineWidth)
    {
        GameObject targetObj = GameObject.Find(bodyName);
        
        if (targetObj != null)
        {
            activeBody = targetObj.GetComponent<CelestialBody>();
            
            bool hasOrbit = activeBody != null && activeBody.semiMajorAxis > 0;
            orbitLine.enabled = hasOrbit;
            
            // Adjust the thickness dynamically based on what was selected
            if (hasOrbit)
            {
                orbitLine.startWidth = targetLineWidth;
                orbitLine.endWidth = targetLineWidth;
            }
            
            bool showAxis = activeBody != null;
            northPoleStick.SetActive(showAxis);
            southPoleStick.SetActive(showAxis);
        }
        else
        {
            activeBody = null;
            orbitLine.enabled = false;
            northPoleStick.SetActive(false);
            southPoleStick.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (activeBody == null) return;

        UpdateAxisSticks();
        UpdateOrbitLine();
    }

    private void UpdateAxisSticks()
    {
        if (!northPoleStick.activeSelf) return;

        float bodyRadius = activeBody.transform.localScale.y;
        
        float stickExtension = bodyRadius * 0.8f; 
        float stickWidth = bodyRadius * 0.15f;    

        Vector3 stickScale = new Vector3(stickWidth, stickExtension * 0.5f, stickWidth);
        float offsetDistance = bodyRadius + (stickExtension * 0.5f);

        northPoleStick.transform.position = activeBody.transform.position + (activeBody.transform.up * offsetDistance);
        northPoleStick.transform.up = activeBody.transform.up;
        northPoleStick.transform.localScale = stickScale;

        southPoleStick.transform.position = activeBody.transform.position - (activeBody.transform.up * offsetDistance);
        southPoleStick.transform.up = activeBody.transform.up;
        southPoleStick.transform.localScale = stickScale;
    }

    private void UpdateOrbitLine()
    {
        if (!orbitLine.enabled || activeBody.centralStar == null) return;

        float semiMinorAxis = activeBody.semiMajorAxis * Mathf.Sqrt(1f - activeBody.eccentricity * activeBody.eccentricity);
        Quaternion inclinationRotation = Quaternion.Euler(activeBody.orbitalInclination, 0f, 0f);

        for (int i = 0; i < ORBIT_SEGMENTS; i++)
        {
            float eccentricAnomaly = ((float)i / ORBIT_SEGMENTS) * Mathf.PI * 2f;
            float xPos = activeBody.semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - activeBody.eccentricity);
            float zPos = semiMinorAxis * Mathf.Sin(eccentricAnomaly);
            Vector3 flatOrbitalPosition = new Vector3(xPos, 0f, zPos);

            Vector3 tiltedOrbitalPosition = inclinationRotation * flatOrbitalPosition;
            Vector3 finalPoint = activeBody.centralStar.position + tiltedOrbitalPosition;
            
            orbitLine.SetPosition(i, finalPoint);
        }
    }

    public void ClearVisuals()
    {
        activeBody = null;
        orbitLine.enabled = false;
        if (northPoleStick != null) northPoleStick.SetActive(false);
        if (southPoleStick != null) southPoleStick.SetActive(false);
    }
}