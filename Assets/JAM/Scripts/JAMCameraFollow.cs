using UnityEngine;

/// <summary>
/// Follows the player with a dead zone: a box at the centre of the screen that the
/// target moves around freely without the camera reacting at all. Only the part of
/// the offset that pushes past the box edge moves the camera, so small dodges and
/// strafing stay still and the view only slides when you actually travel.
///
/// The target defaults to JAMPlayerController.ControlledTransform, so the camera
/// follows a possessed enemy without any extra wiring.
/// </summary>
[RequireComponent(typeof(Camera))]
public class JAMCameraFollow : MonoBehaviour
{
    [Tooltip("Leave empty to follow whatever the player is driving: their own body normally, the host during a possession.")]
    public Transform target;

    [Tooltip("Free-move box at the centre of the view, as a fraction of the whole view. (0.4, 0.3) is 40% of the width and 30% of the height. Zero pins the target dead centre.")]
    public Vector2 deadZone = new Vector2(0.4f, 0.3f);

    [Tooltip("Seconds the camera takes to catch up once the target leaves the box. 0 snaps instantly.")]
    public float smoothTime = 0.18f;

    [Tooltip("Optional. The view is kept inside this collider's bounds, so the camera never shows the void past the level edge. Point it at a box covering the whole level.")]
    public Collider2D confineTo;

    [Tooltip("Jump straight to the target on the first frame instead of easing in from wherever the camera was left in the editor.")]
    public bool snapOnStart = true;

    private Camera cam;
    private Vector3 followVelocity;
    private bool warnedAboutBounds;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (!snapOnStart) return;

        Transform focus = ResolveTarget();
        if (focus == null) return;

        Vector2 snapped = Confine(focus.position, ViewHalfExtents());
        transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);
    }

    // LateUpdate, so the camera reads the target's final position for this frame
    // rather than chasing a value that movement code is still about to change.
    private void LateUpdate()
    {
        Transform focus = ResolveTarget();
        if (focus == null) return;

        Vector2 halfExtents = ViewHalfExtents();
        Vector2 boxHalf = new Vector2(halfExtents.x * deadZone.x, halfExtents.y * deadZone.y);

        Vector2 cameraPosition = transform.position;
        Vector2 offset = (Vector2)focus.position - cameraPosition;

        // Only the overshoot past the box edge is worth moving for.
        Vector2 desired = cameraPosition + new Vector2(
            OvershootPast(offset.x, boxHalf.x),
            OvershootPast(offset.y, boxHalf.y));

        desired = Confine(desired, halfExtents);

        Vector3 goal = new Vector3(desired.x, desired.y, transform.position.z);
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, goal, ref followVelocity, smoothTime)
            : goal;
    }

    private Transform ResolveTarget()
    {
        return target != null ? target : JAMPlayerController.ControlledTransform;
    }

    /// <summary>How far past <paramref name="limit"/> the value reaches, 0 while inside.</summary>
    private static float OvershootPast(float value, float limit)
    {
        if (value > limit) return value - limit;
        if (value < -limit) return value + limit;
        return 0f;
    }

    /// <summary>Half the width and height the camera can see, in world units.</summary>
    private Vector2 ViewHalfExtents()
    {
        Camera c = cam != null ? cam : GetComponent<Camera>();
        if (c == null || !c.orthographic) return Vector2.zero;

        return new Vector2(c.orthographicSize * c.aspect, c.orthographicSize);
    }

    /// <summary>
    /// Measures the confining area, or reports false when there is nothing usable.
    ///
    /// Collider2D.bounds is only meaningful while the collider is enabled and in the
    /// physics world: a disabled one reports a zero-size box at the origin, which
    /// would clamp the camera to (0, 0) and show empty space. A BoxCollider2D can be
    /// measured from its own offset and size instead, so the tidy "disabled marker
    /// box" setup keeps working; anything else has to stay enabled.
    /// </summary>
    private bool TryGetConfineBounds(out Bounds bounds)
    {
        bounds = default;
        if (confineTo == null) return false;

        if (confineTo is BoxCollider2D box)
        {
            Transform boxTransform = box.transform;
            Vector3 centre = boxTransform.TransformPoint(box.offset);
            Vector3 size = Vector3.Scale(box.size, boxTransform.lossyScale);
            bounds = new Bounds(
                new Vector3(centre.x, centre.y, 0f),
                new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f));
        }
        else
        {
            bounds = confineTo.bounds;
        }

        if (bounds.size.x > 0f && bounds.size.y > 0f) return true;

        if (!warnedAboutBounds)
        {
            warnedAboutBounds = true;
            Debug.LogWarning(
                $"[JAMCameraFollow] '{confineTo.name}' has zero bounds, so confinement is off. " +
                "A Collider2D only reports bounds while it is enabled -- either enable it " +
                "(Is Trigger keeps it out of the way) or use a BoxCollider2D, which can be " +
                "measured even when disabled.", this);
        }
        return false;
    }

    private Vector2 Confine(Vector2 desired, Vector2 halfExtents)
    {
        if (!TryGetConfineBounds(out Bounds bounds)) return desired;

        float minX = bounds.min.x + halfExtents.x;
        float maxX = bounds.max.x - halfExtents.x;
        float minY = bounds.min.y + halfExtents.y;
        float maxY = bounds.max.y - halfExtents.y;

        // A level narrower than the view has no valid range to clamp into -- centre
        // on it rather than letting the camera snap between two impossible limits.
        desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : bounds.center.x;
        desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : bounds.center.y;
        return desired;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 halfExtents = ViewHalfExtents();
        if (halfExtents == Vector2.zero) return;

        Vector3 centre = new Vector3(transform.position.x, transform.position.y, 0f);

        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
        Gizmos.DrawWireCube(centre, new Vector3(halfExtents.x * deadZone.x * 2f, halfExtents.y * deadZone.y * 2f, 0f));

        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(centre, new Vector3(halfExtents.x * 2f, halfExtents.y * 2f, 0f));
    }
}
