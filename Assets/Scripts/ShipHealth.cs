using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipHealth : MonoBehaviour
{
    public int BaseHealth;
    public int Health { get; set; }

    // TODO add better way of getting velocity
    public Vector3 Velocity { get => GetComponent<ShipControl>().Rigidbody.velocity; }

    void Start()
    {
        Health = BaseHealth;
    }

    void Update()
    {
        if (Health <= 0)
        {
            GetComponent<ShipControl>().Sink();
        }
    }
}
