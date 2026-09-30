using System.Collections;
using UnityEngine;

/// <summary>
/// The player's three active abilities, lifted out of JAMPlayerController so that
/// controller is left with only movement, aiming and shooting.
///
/// JAMPlayerController still owns the keys: it checks JAMSkillProgression for the
/// unlock and then calls <see cref="TryUse"/>. Everything past that point -- ranges,
/// cooldowns, wind-ups, the possession session -- lives here.
///
/// Lives on the Player alongside JAMPlayerController, so `transform` and `body`
/// below are the player's own.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(JAMPlayerController))]
public class JAMSkill : MonoBehaviour
{
    [Header("1 - Teleport")]
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

    [Header("2 - Time Freeze")]
    public float freezeDuration = 5f;

    [Tooltip("Measured from the moment the freeze ends.")]
    public float freezeCooldown = 10f;

    [Header("3 - Possession")]
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

    /// <summary>True during the teleport wind-up: the player is rooted and cannot shoot.</summary>
    public bool IsCharging => isTeleporting;

    public bool IsPossessing => possessedEnemy != null;

    private JAMPlayerController player;
    private Rigidbody2D body;
    private Camera mainCamera;

    private float nextTeleportTime;
    private float nextFreezeTime;
    private float nextPossessTime;
    private bool isTeleporting;
    private bool isFreezing;

    private JAMEnemy possessedEnemy;
    private float possessionEndTime;

    private void Awake()
    {
        player = GetComponent<JAMPlayerController>();
        body = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Only the session clock. The early-release key is read by
        // JAMPlayerController, so exactly one script decides what key 3 means.
        if (IsPossessing && Time.time >= possessionEndTime)
        {
            EndPossession(stunHost: true);
        }
    }

    /// <summary>
    /// Fires an ability. The caller has already confirmed it is unlocked; this only
    /// judges cooldowns and whether the ability can do anything right now.
    /// </summary>
    /// <returns>True if the ability actually started.</returns>
    public bool TryUse(JAMAbility ability)
    {
        switch (ability)
        {
            case JAMAbility.Teleport:   return TryTeleport();
            case JAMAbility.TimeFreeze: return TryFreezeTime();
            case JAMAbility.Possession: return TryPossess();
            default:                    return false;
        }
    }

    // ------------------------------------------------------------ 1: teleport

    private bool TryTeleport()
    {
        if (isTeleporting || IsPossessing || Time.time < nextTeleportTime) return false;

        StartCoroutine(TeleportRoutine());
        return true;
    }

    private IEnumerator TeleportRoutine()
    {
        isTeleporting = true;

        // The destination is locked in on the key press rather than after the
        // wind-up, so the marker the player sees is the spot they actually land on.
        Vector2 destination = ResolveTeleportDestination(
            JAMInput.CursorWorldPosition(mainCamera, transform.position));

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
            Debug.Log("[JAMSkill] Teleport blocked -- destination is occupied.", this);
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

    // ---------------------------------------------------------- 2: time freeze

    private bool TryFreezeTime()
    {
        if (isFreezing || JAMTimeFreeze.IsFrozen || Time.time < nextFreezeTime) return false;

        StartCoroutine(FreezeRoutine());
        return true;
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

    // ----------------------------------------------------------- 3: possession

    private bool TryPossess()
    {
        if (IsPossessing || isTeleporting || Time.time < nextPossessTime) return false;

        JAMEnemy host = FindPossessTarget();
        if (host == null)
        {
            Debug.Log("[JAMSkill] No enemy in possession range.", this);
            return false;
        }

        BeginPossession(host);
        return true;
    }

    /// <summary>Nearest-to-the-crosshair enemy in range, so the player takes over who they aim at.</summary>
    private JAMEnemy FindPossessTarget()
    {
        JAMEnemy best = null;
        float bestAlignment = float.NegativeInfinity;
        Vector2 aim = player.AimDirection;

        foreach (Collider2D candidate in Physics2D.OverlapCircleAll(body.position, possessRange, possessMask))
        {
            JAMEnemy enemy = candidate.GetComponentInParent<JAMEnemy>();
            if (enemy == null || enemy.CurrentMode == JAMEnemy.Mode.Possessed) continue;

            Vector2 toEnemy = (Vector2)enemy.transform.position - body.position;
            float alignment = toEnemy.sqrMagnitude < 0.0001f
                ? 1f
                : Vector2.Dot(aim, toEnemy.normalized);

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

        SetBodyVisible(false);
        player.SetControlledTransform(host.transform);
    }

    /// <summary>Hands control back. Called by the session clock, by the release key, or by the host dying.</summary>
    public void EndPossession(bool stunHost)
    {
        if (possessedEnemy == null) return;

        JAMEnemy host = possessedEnemy;
        possessedEnemy = null;

        if (host.Health != null) host.Health.Died -= OnHostDied;

        Vector2 hostPosition = host.transform.position;
        if (stunHost) host.EndPossession(possessStunDuration);

        SetBodyVisible(true);
        MoveTo(FindFreeSpotNear(hostPosition));

        player.SetControlledTransform(null);
        nextPossessTime = Time.time + possessCooldown;
    }

    private void OnHostDied(JAMHealth hostHealth)
    {
        // The host is on its way out this frame, so there is nothing left to stun.
        EndPossession(stunHost: false);
    }

    /// <summary>
    /// Hides the player body without deactivating the GameObject -- this component
    /// has to keep running to time the possession and put the player back.
    /// </summary>
    private void SetBodyVisible(bool visible)
    {
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = visible;
        }
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = visible;
        }

        body.simulated = visible;
        if (visible) body.linearVelocity = Vector2.zero;
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

    // ------------------------------------------------------------- shared bits

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
        // Destroyed or disabled mid-ability, nothing else would ever clean up: the
        // scene would stay frozen, or the host would keep the controls.
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
            player.SetControlledTransform(null);
        }
        isTeleporting = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, teleportMaxRange);

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, possessRange);
    }
}
