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
