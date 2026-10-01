using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public JAMHealth targetHealth;
    public Slider slider;
    public Camera cam;
    public Transform target;
    public Vector3 offset;

    [Tooltip("Destroyed along with the enemy. Point it at the EnemyFull root to clear the patrol markers too; left empty, only this bar is removed.")]
    public GameObject cleanupRoot;

    public void UpdateHealthBar(JAMHealth h, int amount)
    {
        slider.value = h.CurrentHealth;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<JAMHealth>();
        }

        if (targetHealth == null || slider == null)
        {
            Debug.LogWarning($"[HealthBar] {name} is missing its Target Health or Slider and will not update.", this);
            return;
        }

        slider.maxValue = targetHealth.maxHealth;
        slider.value = targetHealth.CurrentHealth;
        targetHealth.Damaged += UpdateHealthBar;
        targetHealth.Died += OnTargetDied;
    }

    private void OnDestroy()
    {
        // Comparing a destroyed JAMHealth to null is what tells us there is nothing
        // left to unsubscribe from.
        if (targetHealth == null) return;
        targetHealth.Damaged -= UpdateHealthBar;
        targetHealth.Died -= OnTargetDied;
    }

    private void OnTargetDied(JAMHealth h)
    {
        // This bar is a SIBLING of the enemy rather than a child, so that it does
        // not inherit the rotation JAMEnemy applies every frame. The cost of that
        // is that destroying the enemy leaves the bar floating where it died, so
        // it has to clean itself up.
        Destroy(cleanupRoot != null ? cleanupRoot : gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        // Follow and billboard are independent: an unassigned camera should not
        // also stop the bar from tracking the enemy.
        if (target != null)
        {
            transform.position = target.position + offset;
        }
        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
        }
    }

}
