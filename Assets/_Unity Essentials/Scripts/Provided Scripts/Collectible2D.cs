using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Collectible2D : MonoBehaviour
{

    public float rotationSpeed = 0.5f;
    public GameObject onCollectEffect;

    // Update is called once per frame
    void Update()
    {
        // The spin is driven by hand rather than by physics, so JAMTimeFreeze cannot
        // stop it for us -- every script that moves something itself has to opt in.
        if (JAMTimeFreeze.IsFrozen) return;

        transform.Rotate(0, 0, rotationSpeed);

    }

    private void OnTriggerEnter2D(Collider2D other) {
        
         // Check if the other object is the player. Both controllers are accepted:
         // PlayerController2D drives the Unity Essentials scenes, JAMPlayerController
         // drives the JAM scene. GetComponentInParent rather than GetComponent, so a
         // player whose collider sits on a child object still collects.
        if (other.GetComponentInParent<PlayerController2D>() != null ||
            other.GetComponentInParent<JAMPlayerController>() != null) {

            // Destroy the collectible
            Destroy(gameObject);

            // Instantiate the particle effect
            if (onCollectEffect != null) {
                Instantiate(onCollectEffect, transform.position, transform.rotation);
            }
        }

        
    }


}


