using System.Collections;
using UnityEngine;

/// <summary>
/// Player controller for the JAM prototype (2D top-down shooter with abilities).
///
///   WASD          move
///   Left mouse    fire a bullet toward the cursor (1 damage)
///   1             teleport to the cursor, after a short wind-up
///   2             freeze time for everything but the player, for 5 seconds
///   3             possess a nearby enemy, or drop out of one early
///
/// While possessing, this body is hidden and inert and JAMEnemy does the driving --
/// the controls are identical because both read the same JAMInput. All this script
/// does during a possession is run the session timer and put the player back.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(JAMWeapon))]
public class JAMPlayerController : MonoBehaviour
{
    /// <summary>
    /// Whatever the player is currently driving: this body normally, the host while
    /// possessing. Enemy AI chases this rather than the player object, so possessing
    /// an enemy really does make the others come after you in your new body.
    /// </summary>
    public static Transform ControlledTransform { get; private set; }

    [Header("Movement")]
    [Tooltip("Units per second.")]
    public float moveSpeed = 6f;

    [Tooltip("Rotate the sprite to face the cursor. The sprite's +X axis is treated as its forward.")]
    public bool faceCursor = true;

    [Header("Shooting")]
    [Tooltip("Hold to keep firing. Off means one shot per click. Rate, damage and the bullet prefab live on JAMWeapon.")]
    public bool automaticFire = true;

    [Header("Ability 1 - Teleport")]
    [Tooltip("Wind-up before the jump. The player is rooted and cannot shoot during it.")]
    public float teleportChargeTime = 0.35f;

    [Tooltip("Maximum distance. A cursor further out is clamped to this range.")]
    public float teleportMaxRange = 8f;

    public float teleportCooldown = 3f;

    [Tooltip("Layers that block a teleport, and that the player cannot reappear inside after a possession. Leave as Nothing to allow landing anywhere -- set it once you have an Obstacle layer.")]
    public LayerMask teleportBlockMask = 0;

    [Tooltip("Clearance needed at the destination. Roughly the player's collider radius.")]
    public float teleportClearRadius = 0.85f;

    [Tooltip("Optional marker spawned at the destination during the wind-up, so the jump is telegraphed.")]
    public GameObject teleportMarkerPrefab;

    [Header("Ability 2 - Time Freeze")]
    public float freezeDuration = 5f;

    [Tooltip("Measured from the moment the freeze ends.")]
    public float freezeCooldown = 10f;

    [Header("Ability 3 - Possession")]
    [Tooltip("How long the player keeps the host before being thrown out.")]
    public float possessDuration = 5f;

    [Tooltip("How far away an enemy can be grabbed.")]
    public float possessRange = 6f;

    [Tooltip("How long the host is left stunned afterwards.")]
    public float possessStunDuration = 2f;

    [Tooltip("Measured from the moment the possession ends.")]
    public float possessCooldown = 8f;

    [Tooltip("Layers searched for a host. Everything is fine until you have an Enemy layer.")]
    public LayerMask possessMask = ~0;

    [Tooltip("How far from the host the player reappears.")]
    public float reappearDistance = 1.2f;

    private Rigidbody2D body;
    private JAMWeapon weapon;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;
    private float nextTeleportTime;
    private float nextFreezeTime;
    private float nextPossessTime;
    private bool isTeleporting;
    private bool isFreezing;

    private JAMEnemy possessedEnemy;
    private float possessionEndTime;

    public bool IsPossessing => possessedEnemy != null;

    /// <summary>True while the player is rooted mid-ability and should not move or shoot.</summary>
    private bool IsBusy => isTeleporting;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        weapon = GetComponent<JAMWeapon>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        ControlledTransform = transform;
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (IsPossessing)
        {
            // The host reads its own input. All that is left here is the session
            // timer and the early bail-out.
            if (Time.time >= possessionEndTime || JAMInput.AbilityPressed(3))
            {
                EndPossession(stunHost: true);
            }
            return;
        }

        moveInput = JAMInput.MoveAxis();
        aimDirection = JAMInput.AimDirection(mainCamera, transform.position, aimDirection);

        if (faceCursor)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        HandleFire();

        if (JAMInput.AbilityPressed(1)) TryTeleport();
        if (JAMInput.AbilityPressed(2)) TryFreezeTime();
        if (JAMInput.AbilityPressed(3)) TryPossess();
    }

    private void FixedUpdate()
    {
        if (!body.simulated) return; // hidden inside a host
        body.linearVelocity = IsBusy ? Vector2.zero : moveInput * moveSpeed;
    }

    // ------------------------------------------------------------------- shooting

    private void HandleFire()
    {
        if (IsBusy) return;

        bool wantsToFire = automaticFire ? JAMInput.FireHeld() : JAMInput.FirePressed();
        if (wantsToFire) weapon.TryFire(aimDirection, true);
    }

    // ------------------------------------------------------- ability 1: teleport

    private void TryTeleport()
    {
        if (isTeleporting || Time.time < nextTeleportTime) return;
        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        isTeleporting = true;

        // The destination is locked in on the key press rather than after the
        // wind-up, so the marker the player sees is the spot they actually land on.
        Vector2 destination = ResolveTeleportDestination(JAMInput.CursorWorldPosition(mainCamera, transform.position));

        GameObject marker = null;
        if (teleportMarkerPrefab != null)
        {
            marker = Instantiate(teleportMarkerPrefab, destination, Quaternion.identity);
        }

        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(teleportChargeTime);

        if (marker != null) Destroy(marker);

        if (IsPositionClear(destination))
        {
            MoveTo(destination);
        }
        else
        {
            Debug.Log("[JAMPlayerController] Teleport blocked -- destination is occupied.", this);
        }

        nextTeleportTime = Time.time + teleportCooldown;
        isTeleporting = false;
    }

    private Vector2 ResolveTeleportDestination(Vector2 cursorWorldPosition)
    {
        Vector2 origin = body.position;
        Vector2 offset = cursorWorldPosition - origin;

        if (offset.magnitude > teleportMaxRange)
        {
            offset = offset.normalized * teleportMaxRange;
        }
        return origin + offset;
    }

    // ----------------------------------------------------- ability 2: time freeze

    private void TryFreezeTime()
    {
        if (isFreezing || JAMTimeFreeze.IsFrozen || Time.time < nextFreezeTime) return;
        StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        isFreezing = true;
        JAMTimeFreeze.Freeze(transform, possessedEnemy != null ? possessedEnemy.transform : null);

        // Time.timeScale is untouched, so plain WaitForSeconds is real time here.
        yield return new WaitForSeconds(freezeDuration);

        JAMTimeFreeze.Unfreeze();
        nextFreezeTime = Time.time + freezeCooldown;
        isFreezing = false;
    }

    // ------------------------------------------------------ ability 3: possession

    private void TryPossess()
    {
        if (IsPossessing || IsBusy || Time.time < nextPossessTime) return;

        JAMEnemy host = FindPossessTarget();
        if (host == null)
        {
            Debug.Log("[JAMPlayerController] No enemy in possession range.", this);
            return;
        }

        BeginPossession(host);
    }

    /// <summary>Nearest-to-the-crosshair enemy in range, so the player takes over who they aim at.</summary>
    private JAMEnemy FindPossessTarget()
    {
        JAMEnemy best = null;
        float bestAlignment = float.NegativeInfinity;

        foreach (Collider2D candidate in Physics2D.OverlapCircleAll(body.position, possessRange, possessMask))
        {
            JAMEnemy enemy = candidate.GetComponentInParent<JAMEnemy>();
            if (enemy == null || enemy.CurrentMode == JAMEnemy.Mode.Possessed) continue;

            Vector2 toEnemy = (Vector2)enemy.transform.position - body.position;
            float alignment = toEnemy.sqrMagnitude < 0.0001f
                ? 1f
                : Vector2.Dot(aimDirection, toEnemy.normalized);

            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                best = enemy;
            }
        }

        return best;
    }

    private void BeginPossession(JAMEnemy host)
    {
        possessedEnemy = host;
        possessionEndTime = Time.time + possessDuration;

        host.BeginPossession();
        if (host.Health != null) host.Health.Died += OnHostDied;

        SetBodyActive(false);
        ControlledTransform = host.transform;
    }

    private void EndPossession(bool stunHost)
    {
        if (possessedEnemy == null) return;

        JAMEnemy host = possessedEnemy;
        possessedEnemy = null;

        if (host.Health != null) host.Health.Died -= OnHostDied;

        Vector2 hostPosition = host.transform.position;
        if (stunHost) host.EndPossession(possessStunDuration);

        SetBodyActive(true);
        MoveTo(FindFreeSpotNear(hostPosition));

        ControlledTransform = transform;
        nextPossessTime = Time.time + possessCooldown;
    }

    private void OnHostDied(JAMHealth hostHealth)
    {
        // The host is on its way out this frame, so there is nothing left to stun.
        EndPossession(stunHost: false);
    }

    /// <summary>
    /// Hides the player body without deactivating the GameObject -- this script has
    /// to keep running to time the possession and put the player back.
    /// </summary>
    private void SetBodyActive(bool active)
    {
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = active;
        }
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = active;
        }

        body.simulated = active;
        if (active) body.linearVelocity = Vector2.zero;
    }

    private Vector2 FindFreeSpotNear(Vector2 centre)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            Vector2 candidate = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reappearDistance;
            if (IsPositionClear(candidate)) return candidate;
        }
        return centre; // ringed in on every side: better overlapping than nowhere
    }

    // ------------------------------------------------------------------ internals

    private void MoveTo(Vector2 destination)
    {
        body.position = destination;
        transform.position = destination; // avoids a one-frame lag behind the physics step
        body.linearVelocity = Vector2.zero;
    }

    private bool IsPositionClear(Vector2 position)
    {
        if (teleportBlockMask.value == 0) return true;

        foreach (Collider2D overlap in Physics2D.OverlapCircleAll(position, teleportClearRadius, teleportBlockMask))
        {
            // The player's own colliders travel with them, so they never block a spot.
            if (!overlap.transform.IsChildOf(transform)) return false;
        }
        return true;
    }

    private void OnDisable()
    {
        // If the player is destroyed or disabled mid-ability, nothing else would ever
        // clean up: the scene would stay frozen, or the host would keep the controls.
        if (isFreezing)
        {
            JAMTimeFreeze.Unfreeze();
            isFreezing = false;
        }
        if (possessedEnemy != null)
        {
            if (possessedEnemy.Health != null) possessedEnemy.Health.Died -= OnHostDied;
            possessedEnemy.EndPossession(possessStunDuration);
            possessedEnemy = null;
        }
        isTeleporting = false;

        if (ControlledTransform == transform) ControlledTransform = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, teleportMaxRange);

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, possessRange);
    }
}
