using UnityEngine;

/// <summary>
/// Pickup that feeds JAMSkillProgression. Put this on Skill_cheese.prefab in
/// place of Collectible2D, which only knows how to delete itself.
///
/// The collider must be a trigger. That also keeps bullets from stopping on it --
/// see the trigger rule in JAMBullet.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class JAMSkillCheese : MonoBehaviour
{
    [Tooltip("How much skill cheese this pickup is worth.")]
    public int value = 1;

    [Tooltip("Degrees per second. Set to 0 to hold still.")]
    public float spinSpeed = 90f;

    [Tooltip("Optional VFX spawned where it was picked up.")]
    public GameObject onCollectEffect;

    /// <summary>
    /// How much skill cheese is still lying on the map. Kept as a running total by
    /// the pickups themselves so JAMCheeseCounter never has to sweep the scene.
    /// </summary>
    public static int Remaining { get; private set; }

    private void OnEnable()
    {
        Remaining += Mathf.Max(value, 0);
    }

    private void OnDisable()
    {
        // Also runs on Destroy, which is how a collected wheel leaves the tally.
        Remaining -= Mathf.Max(value, 0);
    }

    // Statics survive a scene reload when Enter Play Mode Options turns the domain
    // reload off, which would otherwise make the count climb every time you hit Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Remaining = 0;
    }

    private void Update()
    {
        // Spun by hand, so JAMTimeFreeze cannot stop it from the outside.
        if (spinSpeed == 0f || JAMTimeFreeze.IsFrozen) return;
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only whoever owns the skill economy can pick this up. While the player is
        // possessing an enemy their collider is disabled, so cheese cannot be
        // hoovered up from inside a host.
        JAMSkillProgression skills = other.GetComponentInParent<JAMSkillProgression>();
        if (skills == null) return;

        skills.Collect(value);

        if (onCollectEffect != null)
        {
            Instantiate(onCollectEffect, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }
}
