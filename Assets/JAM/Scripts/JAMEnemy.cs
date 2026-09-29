using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(JAMHealth))]
[RequireComponent(typeof(JAMWeapon))]

public class JAMEnemy : MonoBehaviour
{
    public Transform player;
    public float patrolSpeed;
    public float chaseSpeed;
    public Transform patrolA;
    public Transform patrolB;
    public float detectRange;
    public float attackRange;
    private Transform curTarget;
    private bool isPossessed;
    private JAMHealth health;
    private JAMWeapon weapon;
    private Mode currentMode = Mode.AI;
    private float stunEndTime;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       // move to patrol point A first
        curTarget = patrolA;
        // set health 
        health = GetComponent<JAMHealth>();
        weapon = GetComponent<JAMWeapon>();
    }

    private void faceToward(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Patrol()
    {
        // turn towards target
        Vector2 aimDir = (curTarget.position - transform.position).normalized;
        faceToward(aimDir);

        transform.position = Vector2.MoveTowards(transform.position, curTarget.position, patrolSpeed * Time.deltaTime);
        
        // switch to other target when reach cur target
        if (Vector2.Distance(transform.position, curTarget.position) < 0.01f)
        {
            curTarget = (curTarget == patrolA) ? patrolB : patrolA;
        }
    }

    private void ChasePlayer()
    {
        // move towards player
        Vector2 aimDir = (player.position - transform.position).normalized;
        faceToward(aimDir);
        transform.position = Vector2.MoveTowards(transform.position, player.position, chaseSpeed * Time.deltaTime);
    }

    private void AttackPlayer()
    {
        // aim at player's current position
        Vector2 aimDir = (player.position - transform.position).normalized;
        faceToward(aimDir);
        weapon.TryFire(aimDir, currentMode == Mode.Possessed);
    }

    private void Possessed()
    {
        // move as player input
        Vector2 moveInput = JAMInput.MoveAxis();
        transform.position += (Vector3)(moveInput * chaseSpeed * Time.deltaTime);

        // face cursor direction
        Vector2 aimDir = JAMInput.AimDirection(Camera.main, transform.position, Vector2.right);
        faceToward(aimDir);

        if (JAMInput.FireHeld())
        {
            
            weapon.TryFire(aimDir, true);
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        // detect player, attack and chase
        // patrol
        // possessed
        
        switch (currentMode)
        {
            case Mode.Possessed:
                Possessed();
                return;
 
            case Mode.Stunned:
                if (Time.time >= stunEndTime)
                {
                    currentMode = Mode.AI;
                }
                else
                {
                    return;
                }
                break;
        }

        if (JAMTimeFreeze.IsFrozen)
        {
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (distanceToPlayer <= detectRange)
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
    }
 
    public void EndPossession(float stunDuration)
    {
        // stun the possessed enemy
        stunEndTime = Time.time + stunDuration;
        currentMode = Mode.Stunned;
    }
}
