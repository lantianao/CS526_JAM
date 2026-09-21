using UnityEngine;

/// <summary>
/// Spawns bullets on request and enforces the fire rate. Shared by the player and
/// by every enemy, so "pull the trigger" means the same thing whoever is deciding
/// to pull it -- an AI, or a player driving that enemy through possession.
/// </summary>
public class JAMWeapon : MonoBehaviour
{
    [Tooltip("Bullet.prefab, with a JAMBullet component on it.")]
    public JAMBullet bulletPrefab;

    [Tooltip("Optional muzzle transform. Leave empty to spawn at muzzleDistance in front of the shooter.")]
    public Transform firePoint;

    [Tooltip("Spawn distance from the shooter's centre when there is no firePoint. Keep this larger than the shooter's collider radius.")]
    public float muzzleDistance = 1f;

    [Tooltip("Shots per second.")]
    public float fireRate = 6f;

    public float bulletSpeed = 14f;

    public int damage = 1;

    private float nextFireTime;

    /// <summary>True when the cooldown has elapsed and a shot would actually come out.</summary>
    public bool IsReady => bulletPrefab != null && Time.time >= nextFireTime;

    private void Awake()
    {
        // Warn once at startup rather than on every trigger pull.
        if (bulletPrefab == null)
        {
            Debug.LogWarning($"[JAMWeapon] {name} has no Bullet Prefab assigned and will never fire.", this);
        }
    }

    /// <summary>
    /// Fires along <paramref name="direction"/> if the cooldown allows it.
    /// <paramref name="firedByPlayer"/> marks the bullet as the player's, which is
    /// what lets it keep flying through a time freeze -- pass true for a possessed
    /// enemy too, since the player is the one shooting.
    /// </summary>
    /// <returns>True if a bullet was actually spawned.</returns>
    public bool TryFire(Vector2 direction, bool firedByPlayer)
    {
        if (!IsReady) return false;

        Vector2 origin = firePoint != null
            ? (Vector2)firePoint.position
            : (Vector2)transform.position + direction.normalized * muzzleDistance;

        JAMBullet bullet = Instantiate(bulletPrefab, origin, Quaternion.identity);
        bullet.damage = damage;
        bullet.speed = bulletSpeed;
        bullet.Launch(direction, gameObject, firedByPlayer);

        nextFireTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0f);
        return true;
    }
}
