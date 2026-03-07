using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Cloud Camera Drift Controller
/// - Swipe left/right → moves on Z axis + slight rotation tilt
/// - Swipe up/down    → moves on Y axis
/// - Separate max offset and sensitivity for horizontal vs vertical
/// Attach to Main Camera.
/// </summary>
public class CloudCameraDrift : MonoBehaviour
{
    [Header("Vertical (Y axis — up/down swipe)")]
    public float verticalSensitivity = 0.3f;
    public float verticalMaxOffset   = 80f;

    [Header("Horizontal (Z axis — left/right swipe)")]
    public float horizontalSensitivity = 0.5f;
    public float horizontalMaxOffset   = 40f;

    [Header("Tilt Rotation on Horizontal Move")]
    [Tooltip("Max degrees the camera tilts when moving left/right")]
    [Range(0f, 45f)]
    public float maxTiltAngle = 8f;

    [Tooltip("How fast the tilt lerps to target angle")]
    [Range(1f, 20f)]
    public float tiltSpeed = 5f;

    [Header("Smoothing")]
    [Range(1f, 20f)]
    public float followSpeed = 8f;

    [Header("Return To Center")]
    public bool returnToCenter = false;
    [Range(1f, 20f)]
    public float returnSpeed = 4f;

    // ── Private state ──────────────────────────────────────
    private Vector3 _originPosition;
    private Quaternion _originRotation;
    private Vector2 _targetOffset;   // x = horizontal (Z world), y = vertical (Y world)
    private Vector2 _lastInputPosition;
    private bool    _isDragging;

    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

    void Start()
    {
        _originPosition = transform.position;
        _originRotation = transform.rotation;
    }

    void Update()
    {
        if (Touch.activeTouches.Count > 0)
            HandleTouch();
        else
            HandleMouse();

        if (!_isDragging && returnToCenter)
            _targetOffset = Vector2.Lerp(_targetOffset, Vector2.zero, Time.deltaTime * returnSpeed);

        ApplyMovement();
        ApplyTilt();
    }

    // ── Mouse ──────────────────────────────────────────────
    void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _lastInputPosition = mouse.position.ReadValue();
            _isDragging = true;
        }
        else if (mouse.leftButton.isPressed && _isDragging)
        {
            Vector2 current = mouse.position.ReadValue();
            ApplyDelta(current - _lastInputPosition);
            _lastInputPosition = current;
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            _isDragging = false;
        }
    }

    // ── Touch ──────────────────────────────────────────────
    void HandleTouch()
    {
        if (Touch.activeTouches.Count == 0) return;
        Touch touch = Touch.activeTouches[0];

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            _lastInputPosition = touch.screenPosition;
            _isDragging = true;
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
        {
            ApplyDelta(touch.screenPosition - _lastInputPosition);
            _lastInputPosition = touch.screenPosition;
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                 touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            _isDragging = false;
        }
    }

    // ── Shared logic ───────────────────────────────────────
    void ApplyDelta(Vector2 delta)
    {
        // Horizontal swipe → Z axis offset (x stores this)
        _targetOffset.x = Mathf.Clamp(
            _targetOffset.x + delta.x * horizontalSensitivity,
            -horizontalMaxOffset, horizontalMaxOffset);

        // Vertical swipe → Y axis offset
        _targetOffset.y = Mathf.Clamp(
            _targetOffset.y + delta.y * verticalSensitivity,
            -verticalMaxOffset, verticalMaxOffset);
    }

    void ApplyMovement()
    {
        Vector3 target = _originPosition + new Vector3(0f, _targetOffset.y, _targetOffset.x);
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * followSpeed);
    }

    void ApplyTilt()
    {
        // Tilt is based on how far we are offset horizontally (normalized 0-1)
        float normalizedH = _targetOffset.x / Mathf.Max(horizontalMaxOffset, 0.001f);

        // Tilt rolls the camera on Z axis — leans into the direction of travel
        float targetTilt = -normalizedH * maxTiltAngle;
        Quaternion targetRotation = _originRotation * Quaternion.Euler(0f, 0f, targetTilt);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * tiltSpeed);
    }

    public void ResetOrigin()
    {
        _originPosition = transform.position;
        _originRotation = transform.rotation;
        _targetOffset   = Vector2.zero;
    }
}
