using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional hook for scripts that need to react to the freeze transition itself
/// (swap to a frozen tint, mute a loop, cancel a wind-up, ...).
/// Anything simpler should just early-out on JAMTimeFreeze.IsFrozen.
/// </summary>
public interface IJAMFreezable
{
    void OnTimeFreezeStart();
    void OnTimeFreezeEnd();
}

/// <summary>
/// Global time-freeze state for the JAM prototype: everything holds still except
/// the player and the player's own bullets.
///
/// Four kinds of motion exist in the scene, and each needs its own treatment:
///
///   1. Physics        -- every Rigidbody2D is switched to Kinematic with its
///                        velocity cached. Kinematic rather than simulated = false
///                        on purpose: a non-simulated body leaves the physics world
///                        entirely, which would let the player's bullets fly
///                        straight through frozen enemies. Kinematic keeps colliders.
///   2. Animation      -- every Animator has its speed set to 0 (the Dog).
///   3. Particles      -- every playing ParticleSystem is paused.
///   4. Script-driven  -- a script spinning a transform by hand cannot be detected
///                        from out here, so it has to opt in itself:
///
///                            void Update()
///                            {
///                                if (JAMTimeFreeze.IsFrozen) return;
///                                ...
///                            }
///
///                        Collectible2D.Update does exactly this for the cheese spin.
///
/// Time.timeScale is deliberately left alone, so the player keeps moving under
/// normal physics and FixedUpdate keeps running.
/// </summary>
public static class JAMTimeFreeze
{
    public static bool IsFrozen { get; private set; }

    private struct FrozenBody
    {
        public Rigidbody2D body;
        public RigidbodyType2D bodyType;
        public Vector2 linearVelocity;
        public float angularVelocity;
    }

    private struct FrozenAnimator
    {
        public Animator animator;
        public float speed;
    }

    private static readonly List<FrozenBody> frozenBodies = new List<FrozenBody>();
    private static readonly List<FrozenAnimator> frozenAnimators = new List<FrozenAnimator>();
    private static readonly List<ParticleSystem> pausedParticles = new List<ParticleSystem>();
    private static readonly List<IJAMFreezable> notified = new List<IJAMFreezable>();

    /// <summary>
    /// Freezes the scene. Everything under one of <paramref name="exemptRoots"/> is
    /// left alone, as is any bullet the player fired. Pass the player body and, if a
    /// possession is running, the host being driven -- nulls are ignored.
    /// </summary>
    public static void Freeze(params Transform[] exemptRoots)
    {
        if (IsFrozen) return;

        FreezeBodies(exemptRoots);
        FreezeAnimators(exemptRoots);
        FreezeParticles(exemptRoots);

        IsFrozen = true;

        // Collected after IsFrozen is set, so listeners can query it from the callback.
        NotifyFreezables(exemptRoots);
    }

    /// <summary>Restores everything Freeze() touched.</summary>
    public static void Unfreeze()
    {
        if (!IsFrozen) return;

        foreach (FrozenBody frozen in frozenBodies)
        {
            if (frozen.body == null) continue; // destroyed while frozen

            // bodyType has to be restored first: a velocity assigned to a body that
            // is still Kinematic is silently dropped by Unity.
            frozen.body.bodyType = frozen.bodyType;
            if (frozen.bodyType != RigidbodyType2D.Static)
            {
                frozen.body.linearVelocity = frozen.linearVelocity;
                frozen.body.angularVelocity = frozen.angularVelocity;
            }
        }
        frozenBodies.Clear();

        foreach (FrozenAnimator frozen in frozenAnimators)
        {
            if (frozen.animator == null) continue;
            frozen.animator.speed = frozen.speed;
        }
        frozenAnimators.Clear();

        foreach (ParticleSystem system in pausedParticles)
        {
            if (system == null) continue;
            system.Play(false); // resumes from where Pause() left it
        }
        pausedParticles.Clear();

        IsFrozen = false;

        foreach (IJAMFreezable freezable in notified)
        {
            // A MonoBehaviour destroyed mid-freeze compares equal to null through
            // Unity's object identity, so test it as a Unity object rather than a
            // plain reference.
            if (freezable is Object unityObject && unityObject == null) continue;
            freezable.OnTimeFreezeEnd();
        }
        notified.Clear();
    }

    /// <summary>
    /// Call this if a scene is unloaded or the player dies mid-freeze, so the
    /// static state does not leak into the next play session.
    /// </summary>
    public static void ResetState()
    {
        frozenBodies.Clear();
        frozenAnimators.Clear();
        pausedParticles.Clear();
        notified.Clear();
        IsFrozen = false;
    }

    // ------------------------------------------------------------------ internals

    private static void FreezeBodies(Transform[] exemptRoots)
    {
        frozenBodies.Clear();

        foreach (Rigidbody2D body in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
        {
            if (body == null || IsExempt(body.transform, exemptRoots)) continue;

            bool isStatic = body.bodyType == RigidbodyType2D.Static;
            frozenBodies.Add(new FrozenBody
            {
                body = body,
                bodyType = body.bodyType,
                linearVelocity = isStatic ? Vector2.zero : body.linearVelocity,
                angularVelocity = isStatic ? 0f : body.angularVelocity
            });

            if (isStatic) continue; // already immovable

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private static void FreezeAnimators(Transform[] exemptRoots)
    {
        frozenAnimators.Clear();

        foreach (Animator animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (animator == null || IsExempt(animator.transform, exemptRoots)) continue;

            frozenAnimators.Add(new FrozenAnimator { animator = animator, speed = animator.speed });
            animator.speed = 0f;
        }
    }

    private static void FreezeParticles(Transform[] exemptRoots)
    {
        pausedParticles.Clear();

        foreach (ParticleSystem system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (system == null || IsExempt(system.transform, exemptRoots)) continue;
            if (!system.isPlaying) continue; // do not resurrect one that had already finished

            // withChildren: false, because FindObjectsByType already returns the
            // children separately and each is tracked on its own.
            system.Pause(false);
            pausedParticles.Add(system);
        }
    }

    private static void NotifyFreezables(Transform[] exemptRoots)
    {
        notified.Clear();

        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is IJAMFreezable freezable && !IsExempt(behaviour.transform, exemptRoots))
            {
                notified.Add(freezable);
                freezable.OnTimeFreezeStart();
            }
        }
    }

    private static bool IsExempt(Transform candidate, Transform[] exemptRoots)
    {
        if (exemptRoots != null)
        {
            foreach (Transform root in exemptRoots)
            {
                if (root != null && candidate.IsChildOf(root)) return true;
            }
        }

        JAMBullet bullet = candidate.GetComponentInParent<JAMBullet>();
        return bullet != null && bullet.FiredByPlayer;
    }
}
