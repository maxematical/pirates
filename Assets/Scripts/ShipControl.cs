using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipControl : MonoBehaviour
{
    protected float Speed;

    void Start()
    {
        Speed = 0;
    }

    protected virtual void FixedUpdate()
    {
        float heading = transform.rotation.eulerAngles.y;
        Vector3 velocity = Speed * transform.forward;
        transform.position += velocity * Time.fixedDeltaTime;
    }

    public Vector3 CalculateCannonballTrajectory(Vector3 spawnPos, Vector3 target, float speed, float gravity)
    {
        Vector3 position = this.transform.position;
        float yawAngle = -Mathf.Atan2(target.z - position.z, target.x - position.x) * Mathf.Rad2Deg + 90;

        // We want the cannonball to land on a specific spot on the map
        // We can use math to determine the right angle to shoot the cannonball at
        // (this isn't perfect but it mostly works)
        float distance = (position - target).magnitude;
        float pitchAngle = 0.5f * Mathf.Asin(distance * gravity / (speed * speed)) * Mathf.Rad2Deg;

        // If the angle is NaN, then we are technically out of range, but can do our best to fire anyways
        if (float.IsNaN(pitchAngle))
        {
            pitchAngle = -45;
        }

        return Quaternion.Euler(-pitchAngle, yawAngle, 0) * Vector3.forward * speed;
    }

    protected bool IsWithinCannonRange(Vector3 spawnPos, Vector3 target, float speed, float gravity)
    {
        Vector3 position = this.transform.position;
        float distance = (position - target).magnitude;
        float x = distance * gravity / (speed * speed);
        return x >= -1.0f && x <= 1.0f;
    }
}
