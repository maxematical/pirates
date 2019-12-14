using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cannonball : MonoBehaviour
{
    public float Gravity { get; set; }
    public Vector3 Velocity { get; set; }
    public GameObject IgnoreCollisions;
    public GameObject CollisionVFXPrefab;
    
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

    void OnCollisionEnter(Collision collision)
    {
        // Find ShipHealth component in the object's highest parent
        Transform obj = collision.gameObject.transform;
        while (obj.parent != null)
            obj = obj.parent;
        ShipHealth health = obj.GetComponent<ShipHealth>();

        if (health?.gameObject == IgnoreCollisions)
        {
            return;
        }

        if (health != null)
        {
            health.Health--;
            Destroy(this.gameObject);

            ContactPoint contact = collision.contacts[0];
            Vector3 vfxDirection = contact.normal;
            vfxDirection.y = Util.Cap(vfxDirection.y, 0.2f);
            vfxDirection.Normalize();

            GameObject vfx = Instantiate(CollisionVFXPrefab, contact.point, Quaternion.identity);
            vfx.transform.rotation = Quaternion.LookRotation(vfxDirection, Vector3.up);
        }
    }
}
