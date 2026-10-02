using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(JAMHealth))]
[RequireComponent(typeof(JAMWeapon))]

public class JAMEnemy : MonoBehaviour
{
    [Tooltip("Leave empty to follow whatever the player is driving, which also makes other enemies chase a possessed host instead of the hidden player body.")]
    public Transform player;
    public float patrolSpeed;
    public float chaseSpeed;

    public float detectRange;
    public float attackRange;
    public float chaseRange;
    public Vector2 homePosition;
    private bool returning = false;

    public Transform patrolA;
    public Transform patrolB;

    [Tooltip("How close counts as having reached a patrol point. Velocity-driven movement overshoots, so this cannot be near-zero.")]
    public float patrolArriveDistance = 0.2f;

    [Tooltip("How long a hit keeps this enemy hunting, even when the shooter is well outside Detect Range. Also how long a possession stays blown for it.")]
    public float alertDuration = 6f;

    private Transform curTarget;
    private Rigidbody2D body;
    private JAMHealth health;
    private JAMWeapon weapon;
    private Mode currentMode = Mode.AI;
    private float stunEndTime;

    // Set by the Update logic, applied in FixedUpdate. Writing transform.position
    // directly would bypass the physics engine entirely and let enemies -- and a
    // possessed player -- walk straight through walls.
    private Vector2 desiredVelocity;

    public Color possessedTint = new Color(0.6f, 0.85f, 1f);
    public Color stunnedTint = new Color(0.6f, 0.6f, 0.6f);
    public Color frozenTint = new Color(0.6f, 0.85f, 1f);
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private bool wasFrozen = false;

    // Set when this enemy takes a hit: it has worked out that the newcomer is the
    // player. Cleared at the start of each possession so every disguise is a fresh
    // one -- otherwise an enemy shot in round one would see through every later
    // possession immediately.
    private float alertEndTime;

    /// <summary>
    /// True for a while after this enemy is hit. Being shot drags it into the fight
    /// whatever the range, and during a possession it is also what blows the
    /// player's cover. It expires so that one stray bullet does not leave an enemy
    /// hunting forever.
    /// </summary>
    private bool Alerted => Time.time < alertEndTime;
    private bool wasPossessionActive = false;


    // Cached in Awake, not Start: JAMSkill reads Health the moment a possession
    // begins, and Start() ordering between components is not guaranteed.
    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        health = GetComponent<JAMHealth>();
        weapon = GetComponent<JAMWeapon>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       // move to patrol point A first
        curTarget = patrolA != null ? patrolA : patrolB;
        // set home position
        homePosition = transform.position;

        // remember enemy's original color
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    private void faceToward(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// What this enemy is hunting, or null when it has nothing to hunt.
    ///
    /// A possession reads as a disguise: while the player is wearing someone else,
    /// this enemy has no idea where they went and returns to patrolling. Taking a
    /// hit blows the cover, and from then on it chases whatever the player is
    /// actually driving rather than the empty body left behind.
    /// </summary>
    private Transform Target
    {
        get
        {
            if (JAMPlayerController.PossessionActive)
            {
                return Alerted ? JAMPlayerController.ControlledTransform : null;
            }
            return player != null ? player : JAMPlayerController.ControlledTransform;
        }
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    /// <summary>Any hit blows a possession's cover for this enemy.</summary>
    private void OnDamaged(JAMHealth damagedHealth, int amount)
    {
        alertEndTime = Time.time + alertDuration;
    }

    private void Patrol()
    {
        // No route assigned: hold position rather than dereferencing a null point.
        if (curTarget == null) curTarget = patrolA != null ? patrolA : patrolB;
        if (curTarget == null)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = (Vector2)curTarget.position - (Vector2)transform.position;

        // switch to other target when reach cur target
        if (toTarget.magnitude <= patrolArriveDistance)
        {
            Transform other = (curTarget == patrolA) ? patrolB : patrolA;
            if (other != null) curTarget = other;
            desiredVelocity = Vector2.zero;
            return;
        }

        Vector2 aimDir = toTarget.normalized;
        faceToward(aimDir);
        desiredVelocity = aimDir * patrolSpeed;
    }

    /// <summary>Walks back to the spawn point recorded in Start. Used after a chase
    /// is called off, and it is the whole idle behaviour of an enemy with no patrol
    /// points -- such an enemy simply guards the spot it was placed on.</summary>
    private void ReturnHome()
    {
        Vector2 toHome = homePosition - (Vector2)transform.position;
        if (toHome.magnitude <= patrolArriveDistance)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        Vector2 aimDir = toHome.normalized;
        faceToward(aimDir);
        desiredVelocity = aimDir * patrolSpeed;
    }

    private void ChasePlayer()
    {
        // move towards player
        Vector2 aimDir = ((Vector2)Target.position - (Vector2)transform.position).normalized;
        faceToward(aimDir);
        desiredVelocity = aimDir * chaseSpeed;
    }

    private void AttackPlayer()
    {
        // aim at player's current position
        Vector2 aimDir = ((Vector2)Target.position - (Vector2)transform.position).normalized;
        faceToward(aimDir);
        desiredVelocity = Vector2.zero;
        weapon.TryFire(aimDir, currentMode == Mode.Possessed);
    }

    private void Possessed()
    {
        // move as player input
        desiredVelocity = JAMInput.MoveAxis() * chaseSpeed;

        // face cursor direction
        Vector2 aimDir = JAMInput.AimDirection(Camera.main, transform.position, Vector2.right);
        faceToward(aimDir);

        if (JAMInput.FireHeld())
        {

            weapon.TryFire(aimDir, true);
        }
    }

    private void FixedUpdate()
    {
        if (!body.simulated) return;
        body.linearVelocity = desiredVelocity;
    }
    
    // Update is called once per frame
    void Update()
    {
        // detect player, attack and chase
        // patrol
        // possessed

        // Each new possession starts as an intact disguise, so suspicion earned
        // during an earlier one does not carry over.
        bool possessionActive = JAMPlayerController.PossessionActive;
        if (possessionActive && !wasPossessionActive) alertEndTime = 0f;
        wasPossessionActive = possessionActive;

        switch (currentMode)
        {
            case Mode.Possessed:
                Possessed();
                return;
 
            case Mode.Stunned:
                if (Time.time >= stunEndTime)
                {
                    currentMode = Mode.AI;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = baseColor;
                    }
                }
                else
                {
                    desiredVelocity = Vector2.zero;
                    return;
                }
                break;
        }

        if (JAMTimeFreeze.IsFrozen)
        {
            if (!wasFrozen && spriteRenderer != null)
            {
                spriteRenderer.color = frozenTint;
            }
            wasFrozen = true;
            desiredVelocity = Vector2.zero;
            return;
        }
        else if (wasFrozen)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
            wasFrozen = false;
        }

        float distanceToHome = Vector2.Distance(transform.position, homePosition);
        // if still returning to home
        if (returning)
        {
            // Walk back to the post, not to a patrol point: a guard with no patrol
            // route would otherwise stand still here and never clear `returning`,
            // stranding it wherever it gave up the chase.
            ReturnHome();
            if (distanceToHome < patrolArriveDistance)
            {
                returning = false;
            }
            return;
        }

        // if too far from home position, stop chasing and back to patrol

        if (distanceToHome > chaseRange)
        {
            // Giving up the chase means losing interest too. Without this an
            // alerted enemy would bounce forever: leash home, re-engage, leash
            // home again, for as long as the alert lasts.
            ReturnHome();
            returning = true;
            alertEndTime = 0f;
            return;
        }

        // Nothing to hunt (no reference dragged in and no live player): just patrol.
        Transform target = Target;
        if (target == null)
        {
            Patrol();
            return;
        }

        // Spotting the player is the usual way into a fight, but being shot drags
        // the enemy in regardless of range -- otherwise a sniper well outside
        // Detect Range could whittle it down while it patrolled on obliviously.
        float distanceToPlayer = Vector2.Distance(transform.position, target.position);
        bool engaged = Alerted || distanceToPlayer <= detectRange;

        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (engaged)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    public enum Mode { AI, Possessed, Stunned }
    public Mode CurrentMode => currentMode;
    public JAMHealth Health => health;
 
    public void BeginPossession()
    {
        currentMode = Mode.Possessed;

        // change color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = possessedTint;
        }
    }
 
    public void EndPossession(float stunDuration)
    {
        // stun the possessed enemy
        stunEndTime = Time.time + stunDuration;
        currentMode = Mode.Stunned;

        // change back to original color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = stunnedTint;
        }
    }
}
