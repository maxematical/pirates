using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cannonball : MonoBehaviour
{
    public float Gravity { get; set; }
    public Vector3 Velocity { get; set; }
    public GameObject IgnoreCollisions;
    
    void Start()
    {
        
    }

    void FixedUpdate()
    {
        transform.position += Velocity * Time.fixedDeltaTime;
        Velocity += Vector3.down * Gravity * Time.fixedDeltaTime;

        if (transform.position.y < -1)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider otherCollider)
    {
        if (otherCollider.gameObject == IgnoreCollisions)
        {
            return;
        }
        ShipHealth health = otherCollider.GetComponent<ShipHealth>();
        if (health != null)
        {
            health.Health--;
            Destroy(this.gameObject);
        }
    }
}
