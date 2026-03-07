using UnityEngine;

/// <summary>
/// CloudTorusController
/// Sets torus cloud volume parameters every frame via global shader properties.
/// Camera positioning is handled externally by your own camera controller.
/// </summary>
[ExecuteAlways]
public class CloudTorusController : MonoBehaviour
{
    [Header("Torus Shape")]
    [Tooltip("Radius of the ring (center to tube center). Try 2000–5000")]
    public float majorRadius = 4000f;

    [Tooltip("Radius of the tube cross-section. Try 400–1000")]
    public float minorRadius = 700f;

    [Tooltip("World center of the torus")]
    public Vector3 torusCenter = Vector3.zero;

    [Header("Torus Orientation")]
    [Tooltip("Rotate the torus ring axis. Default (0,0,0) = ring flat in XZ plane.\n" +
             "X=90 tilts it vertical (tunnel goes left-right).\n" +
             "Z=90 tilts it so tunnel goes up-down.")]
    public Vector3 torusRotationDeg = Vector3.zero;

    [Header("Travel")]
    [Tooltip("Auto-increment angle to animate cloud movement through the tunnel")]
    public bool autoTravel = false;

    [Tooltip("Degrees per second")]
    public float travelSpeed = 3f;

    [Range(0f, 360f)]
    public float travelAngle = 0f;

    [Header("Debug")]
    [Tooltip("0 = real clouds  |  1 = green/red hit test  |  2 = pure white")]
    [Range(0, 2)]
    public int debugMode = 0;

    void OnEnable()  { Apply(); }
    void OnValidate(){ Apply(); }

    void Update()
    {
        if (autoTravel && Application.isPlaying)
            travelAngle = (travelAngle + travelSpeed * Time.deltaTime) % 360f;

        Apply();
    }

    void Apply()
    {
        Vector3 rotRad = torusRotationDeg * Mathf.Deg2Rad;

        Shader.SetGlobalFloat("_TorusMajorRadius", majorRadius);
        Shader.SetGlobalFloat("_TorusMinorRadius", minorRadius);
        Shader.SetGlobalFloat("_TorusAngle",       travelAngle * Mathf.Deg2Rad);
        Shader.SetGlobalVector("_TorusCenter",     torusCenter);
        Shader.SetGlobalVector("_TorusRotation",   rotRad);
        Shader.SetGlobalFloat("_TorusDebugMode",   (float)debugMode);
    }

    public void SetTravelSpeed(float v) => travelSpeed = v;
    public void SetDebugMode(int m)     { debugMode = m; Apply(); }
}
