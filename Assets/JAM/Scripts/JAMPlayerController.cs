using UnityEngine;

/// <summary>
/// The player's body: movement, aiming and shooting, plus the keys that reach the
/// abilities. The abilities themselves live on JAMSkill; this script only decides
/// whether a press is allowed through, by asking JAMSkillProgression.
///
///   WASD          move
///   Left mouse    fire a bullet toward the cursor (1 damage)
///   1 / 2 / 3     teleport / freeze time / possess -- or, while still locked,
///                 spend a skill unlock on that ability
///
/// While possessing, this body is hidden and inert and JAMEnemy does the driving.
/// The controls feel identical because both read the same JAMInput.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(JAMWeapon))]
public class JAMPlayerController : MonoBehaviour
{
    /// <summary>
    /// Whatever the player is currently driving: this body normally, the host while
    /// possessing. Enemy AI chases this rather than the player object, and
    /// JAMCameraFollow frames it, so possessing an enemy makes the camera follow the
    /// new body and the other enemies come after it.
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

    /// <summary>Where the player is aiming. JAMSkill uses it to pick a possession target.</summary>
    public Vector2 AimDirection => aimDirection;

    private Rigidbody2D body;
    private JAMWeapon weapon;
    private JAMSkill skill;
    private JAMSkillProgression skills;
    private Camera mainCamera;

    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;

    /// <summary>True while an ability has the player rooted and unable to shoot.</summary>
    private bool IsBusy => skill != null && skill.IsCharging;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        weapon = GetComponent<JAMWeapon>();
        skill = GetComponent<JAMSkill>();
        skills = GetComponent<JAMSkillProgression>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        ControlledTransform = transform;
    }

    private void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (skill != null && skill.IsPossessing)
        {
            // The host reads its own input. Key 3 is read here rather than in
            // JAMSkill so that exactly one script decides what the press means.
            if (JAMInput.AbilityPressed(3)) skill.EndPossession(stunHost: true);
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

        if (JAMInput.AbilityPressed(1)) UseAbility(JAMAbility.Teleport);
        if (JAMInput.AbilityPressed(2)) UseAbility(JAMAbility.TimeFreeze);
        if (JAMInput.AbilityPressed(3)) UseAbility(JAMAbility.Possession);
    }

    private void FixedUpdate()
    {
        if (!body.simulated) return; // hidden inside a host
        body.linearVelocity = IsBusy ? Vector2.zero : moveInput * moveSpeed;
    }

    private void HandleFire()
    {
        if (IsBusy) return;

        bool wantsToFire = automaticFire ? JAMInput.FireHeld() : JAMInput.FirePressed();
        if (wantsToFire) weapon.TryFire(aimDirection, true);
    }

    /// <summary>
    /// Routes an ability key. A locked ability spends a pending skill unlock instead
    /// of firing, so the same three keys serve as both the pick-a-skill menu and the
    /// hotkeys -- the press that buys an ability does not also use it. With no
    /// JAMSkillProgression on the player, everything is simply available.
    /// </summary>
    private void UseAbility(JAMAbility ability)
    {
        if (skill == null) return;

        if (skills != null && !skills.IsUnlocked(ability))
        {
            if (skills.TryUnlock(ability))
            {
                Debug.Log($"[JAMPlayerController] Unlocked {JAMSkillProgression.NameOf(ability)}.", this);
            }
            return;
        }

        skill.TryUse(ability);
    }

    /// <summary>
    /// Points <see cref="ControlledTransform"/> at a possessed host, or back at this
    /// body when passed null. Called by JAMSkill as a possession starts and ends.
    /// </summary>
    public void SetControlledTransform(Transform driven)
    {
        ControlledTransform = driven != null ? driven : transform;
    }

    private void OnDisable()
    {
        if (ControlledTransform == transform) ControlledTransform = null;
    }
}
