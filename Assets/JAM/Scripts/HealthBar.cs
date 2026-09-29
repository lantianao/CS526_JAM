using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public JAMHealth targetHealth;
    public Slider slider;
    public Camera cam;
    public Transform target;
    public Vector3 offset;

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

        slider.maxValue = targetHealth.maxHealth;
        slider.value = targetHealth.CurrentHealth;
        targetHealth.Damaged += UpdateHealthBar;
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = cam.transform.rotation;
        transform.position = target.position + offset;
    }

}
