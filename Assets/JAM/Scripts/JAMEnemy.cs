using UnityEngine;

/// <summary>
/// One script for both an enemy's own behaviour and the player driving it through
/// possession. They are not two different things: moving, aiming, shooting and
/// taking damage work identically either way -- the only difference is where the
/// intent comes from.
///
/// So the script splits into a decision half and an execution half:
///
///     Mode.AI         -> DecideFromAI()     works out the intent itself
///     Mode.Possessed  -> DecideFromInput()  reads the intent off the keyboard and mouse
///     Mode.Stunned    -> no intent at all
///                              |
///                              v
///     moveIntent / aimDirection / wantsFire   <-- the entire interface between the halves
///                              |
///                              v
///     shared execution: apply velocity, face the aim, pull the trigger
///
/// Adding a possessed mode therefore costs one enum value and one Decide method;
/// none of the execution code is duplicated or special-cased.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(JAMHealth))]
[RequireComponent(typeof(JAMWeapon))]
public class JAMEnemy : MonoBehaviour
{
    public enum Mode
    {
        AI,
        Possessed,
        Stunned
    }

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Tooltip("Speed while the player is driving this enemy. Usually a little quicker than its own patrol pace.")]
    public float possessedMoveSpeed = 5f;

    [Tooltip("Rotate the sprite toward whatever it is aiming at. The sprite's +X axis is treated as its forward.")]
    public bool faceAim = true;

    [Header("AI - Senses")]
    [Tooltip("Distance at which the enemy notices the player and starts chasing.")]
    public float detectionRange = 7f;

    [Tooltip("Distance the enemy tries to hold once it has a target. It stops closing in past this.")]
    public float preferredRange = 4f;

    [Tooltip("Distance inside which it starts shooting. Keep this at or above preferredRange.")]
    public float fireRange = 5.5f;

    [Tooltip("Layers that block line of sight. Leave as Nothing to let the enemy shoot through walls -- set it once you have an Obstacle layer.")]
    public LayerMask sightBlockMask = 0;

    [Header("AI - Patrol")]
    [Tooltip("Points to walk between while idle. Leave empty and the enemy simply holds its ground.")]
    public Transform[] patrolPoints;

    public float patrolArriveDistance = 0.3f;

    public float patrolWaitTime = 0.6f;

    [Header("Feedback")]
    public Color possessedTint = new Color(0.6f, 0.85f, 1f);
    public Color stunnedTint = new Color(0.6f, 0.6f, 0.6f);

    public Mode CurrentMode { get; private set; } = Mode.AI;
    public JAMHealth Health { get; private set; }

    private Rigidbody2D body;
    private JAMWeapon weapon;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private Color baseTint = Color.white;

    // The whole interface between "who decides" and "what happens".
    private Vector2 moveIntent;
    private Vector2 aimDirection = Vector2.right;
    private bool wantsFire;

    private int patrolIndex;
    private float patrolResumeTime;
    private float stunEndTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        Health = GetComponent<JAMHealth>();
        weapon = GetComponent<JAMWeapon>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseTint = spriteRenderer.color;

        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // A possessed enemy is the player for all practical purposes, so it keeps
        // acting through the player's own time freeze.
        if (JAMTimeFreeze.IsFrozen && CurrentMode != Mode.Possessed)
        {
            ClearIntent();
            return;
        }

        switch (CurrentMode)
        {
            case Mode.Stunned:
                ClearIntent();
                if (Time.time >= stunEndTime) SetMode(Mode.AI);
                break;

            case Mode.Possessed:
                DecideFromInput();
                break;

            default:
                DecideFromAI();
                break;
        }

        // ---- shared execution, identical for every mode ----

        if (wantsFire)
        {
            weapon.TryFire(aimDirection, CurrentMode == Mode.Possessed);
        }

        if (faceAim && aimDirection.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void FixedUpdate()
    {
        if (!body.simulated) return;

        float speed = CurrentMode == Mode.Possessed ? possessedMoveSpeed : moveSpeed;
        body.linearVelocity = moveIntent * speed;
    }

    // ------------------------------------------------------------ decision: player

    private void DecideFromInput()
    {
        moveIntent = JAMInput.MoveAxis();
        aimDirection = JAMInput.AimDirection(mainCamera, transform.position, aimDirection);
        wantsFire = JAMInput.FireHeld();
    }

    // ---------------------------------------------------------------- decision: AI

    private void DecideFromAI()
    {
        Transform target = JAMPlayerController.ControlledTransform;
        Vector2 position = body.position;

        if (target == null)
        {
            Patrol(position);
            return;
        }

        Vector2 toTarget = (Vector2)target.position - position;
        float distance = toTarget.magnitude;

        if (distance > detectionRange || !HasLineOfSight(position, target, distance))
        {
            Patrol(position);
            return;
        }

        aimDirection = distance > 0.0001f ? toTarget / distance : aimDirection;
        // Close the gap, then hold station at preferredRange rather than walking
        // into the player's face.
        moveIntent = distance > preferredRange ? aimDirection : Vector2.zero;
        wantsFire = distance <= fireRange;
    }

    private void Patrol(Vector2 position)
    {
        wantsFire = false;

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            moveIntent = Vector2.zero;
            return;
        }

        if (Time.time < patrolResumeTime)
        {
            moveIntent = Vector2.zero;
            return;
        }

        Transform point = patrolPoints[patrolIndex % patrolPoints.Length];
        if (point == null)
        {
            patrolIndex++;
            return;
        }

        Vector2 toPoint = (Vector2)point.position - position;
        if (toPoint.magnitude <= patrolArriveDistance)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            patrolResumeTime = Time.time + patrolWaitTime;
            moveIntent = Vector2.zero;
            return;
        }

        moveIntent = toPoint.normalized;
        aimDirection = moveIntent;
    }

    private bool HasLineOfSight(Vector2 from, Transform target, float distance)
    {
        if (sightBlockMask.value == 0) return true;

        RaycastHit2D hit = Physics2D.Raycast(from, ((Vector2)target.position - from).normalized, distance, sightBlockMask);
        // Nothing in the way, or the only thing in the way is the target itself.
        return hit.collider == null || hit.collider.transform.IsChildOf(target);
    }

    // ------------------------------------------------------------------ possession

    /// <summary>Hands the controls to the player. Called by JAMPlayerController.</summary>
    public void BeginPossession()
    {
        SetMode(Mode.Possessed);
    }

    /// <summary>
    /// Takes the controls back and leaves the enemy reeling.
    /// Called by JAMPlayerController when the possession times out or is released.
    /// </summary>
    public void EndPossession(float stunDuration)
    {
        Stun(stunDuration);
    }

    public void Stun(float duration)
    {
        stunEndTime = Time.time + duration;
        SetMode(Mode.Stunned);
        ClearIntent();
        if (body.simulated && body.bodyType == RigidbodyType2D.Dynamic)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    // ----------------------------------------------------------------- internals

    private void SetMode(Mode mode)
    {
        CurrentMode = mode;

        if (spriteRenderer == null) return;
        switch (mode)
        {
            case Mode.Possessed: spriteRenderer.color = possessedTint; break;
            case Mode.Stunned:   spriteRenderer.color = stunnedTint;   break;
            default:             spriteRenderer.color = baseTint;      break;
        }
    }

    private void ClearIntent()
    {
        moveIntent = Vector2.zero;
        wantsFire = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, fireRange);

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, preferredRange);
    }
}
