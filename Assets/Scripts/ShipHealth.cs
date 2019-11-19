using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipHealth : MonoBehaviour
{
    public int BaseHealth;
    public int Health { get; set; }

    void Start()
    {
        Health = BaseHealth;
    }

    void Update()
    {
        if (Health <= 0)
        {
            Destroy(gameObject);
        }
    }
}
