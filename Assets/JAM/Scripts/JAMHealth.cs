using System;
using UnityEngine;

/// <summary>
/// Minimal hit-point container. Put this on anything a bullet should be able to
/// kill -- enemies, breakable props, the player.
/// </summary>
public class JAMHealth : MonoBehaviour
{
    [Tooltip("Hit points at spawn. A JAMBullet deals 1 by default.")]
    public int maxHealth = 3;

    [Tooltip("Optional VFX spawned at this object's position when it dies.")]
    public GameObject onDeathEffect;

    [Tooltip("Destroy the GameObject on death. Turn off if something else (a death animation, a respawner) owns the cleanup.")]
    public bool destroyOnDeath = true;

    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    /// <summary>Raised on every damage tick. Arguments: this, damage taken.</summary>
    public event Action<JAMHealth, int> Damaged;

    /// <summary>Raised once, the moment health reaches zero.</summary>
    public event Action<JAMHealth> Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth -= amount;
        Damaged?.Invoke(this, amount);

        if (CurrentHealth > 0) return;

        CurrentHealth = 0;
        Died?.Invoke(this);

        if (onDeathEffect != null)
        {
            Instantiate(onDeathEffect, transform.position, transform.rotation);
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
    }
}
