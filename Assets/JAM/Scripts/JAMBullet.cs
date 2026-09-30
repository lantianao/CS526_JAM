using UnityEngine;

/// <summary>
/// Projectile fired by JAMPlayerController. Flies in a straight line at a constant
/// velocity, deals its damage to the first JAMHealth it touches, and despawns.
///
/// Prefab requirements (see Bullet.prefab):
///   - Rigidbody2D: Body Type = Dynamic, Gravity Scale = 0, Collision Detection =
///     Continuous. Dynamic rather than Kinematic so the bullet still registers
///     triggers against enemies that JAMTimeFreeze has switched to Kinematic.
///   - A Collider2D with Is Trigger = On.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class JAMBullet : MonoBehaviour
{
    [Tooltip("Units per second. Overwritten by the shooter when it calls Launch().")]
    public float speed = 14f;

    [Tooltip("Hit points removed from the first JAMHealth hit. Overwritten by the shooter.")]
    public int damage = 1;

    [Tooltip("Seconds before the bullet despawns on its own, so strays do not pile up.")]
    public float lifetime = 3f;

    [Tooltip("Layers the bullet reacts to. Everything else is passed straight through.")]
    public LayerMask hitMask = ~0;

    [Tooltip("Fly through trigger colliders that have no JAMHealth. Triggers are volumes, not walls -- collectibles, detection zones, doorways. Turn off to make the bullet react to them.")]
    public bool passThroughTriggers = true;

    [Tooltip("Despawn on hitting solid geometry with no JAMHealth (a wall). Turn off for a piercing shot.")]
    public bool destroyOnEnvironment = true;

    [Tooltip("Optional VFX spawned at the impact point.")]
    public GameObject onHitEffect;

    /// <summary>True when the player fired this. JAMTimeFreeze uses it to let player bullets keep flying.</summary>
    public bool FiredByPlayer { get; private set; }

    private GameObject owner;
    private Rigidbody2D body;
    private bool launched;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        // A discrete-stepping bullet at 14 units/s can tunnel straight through a
        // thin wall between two physics steps, so force continuous detection here
        // rather than relying on the prefab being set up correctly.
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        // A bullet dropped into the scene by hand (or fired by something that forgot
        // to call Launch) would otherwise sit there forever.
        if (!launched)
        {
            Launch(transform.right, null, false);
        }
    }

    /// <summary>
    /// Sends the bullet on its way. Set <see cref="damage"/> and <see cref="speed"/>
    /// before calling if the shooter overrides the prefab values.
    /// </summary>
    public void Launch(Vector2 direction, GameObject shooter, bool firedByPlayer)
    {
        launched = true;
        owner = shooter;
        FiredByPlayer = firedByPlayer;

        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        body.linearVelocity = direction * speed;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // Ignore the shooter's own colliders, so a muzzle inside the shooter's
        // collider does not eat the shot on frame one.
        if (owner != null)
        {
            Collider2D mine = GetComponent<Collider2D>();
            if (mine != null)
            {
                foreach (Collider2D theirs in owner.GetComponentsInChildren<Collider2D>())
                {
                    Physics2D.IgnoreCollision(mine, theirs, true);
                }
            }
        }

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.transform.IsChildOf(owner.transform)) return;
        if (other.GetComponentInParent<JAMBullet>() != null) return; // bullets pass through each other
        if ((hitMask.value & (1 << other.gameObject.layer)) == 0) return;

        JAMHealth target = other.GetComponentInParent<JAMHealth>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }
        else if (passThroughTriggers && other.isTrigger)
        {
            // Nothing here to damage, and a trigger is a volume rather than a wall:
            // pickups, detection zones and doorways are all triggers. The shot flies
            // on through. Solid geometry uses a non-trigger collider and still stops it.
            return;
        }
        else if (!destroyOnEnvironment)
        {
            return;
        }

        if (onHitEffect != null)
        {
            Instantiate(onHitEffect, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }
}
