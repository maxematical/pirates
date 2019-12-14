using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipControl : MonoBehaviour
{
    public Vector3 Velocity { get => Speed * transform.forward; }

    protected float Speed;

    void Start()
    {
        Speed = 0;
    }

    protected virtual void FixedUpdate()
    {
        transform.position += Velocity * Time.fixedDeltaTime;
    }

    public static (float, float) CalculateCannonAim(Vector3 position, Vector3 target, float speed, float gravity)
    {
        float yawAngle = -Mathf.Atan2(target.z - position.z, target.x - position.x) * Mathf.Rad2Deg + 90;

        // We want the cannonball to land on a specific spot on the map
        // We can use math to determine the right angle to shoot the cannonball at
        // (this isn't perfect but it mostly works)
        float distance = (position - target).magnitude;
        float pitchAngle = 0.5f * Mathf.Asin(distance * gravity / (speed * speed)) * Mathf.Rad2Deg;

        // If the angle is NaN, then we are technically out of range, but can do our best to fire anyways
        if (float.IsNaN(pitchAngle))
        {
            pitchAngle = 45;
        }

        return (yawAngle, pitchAngle);
    }

    public static Vector3 CalculateCannonballTrajectory(Vector3 position, Vector3 target, float speed, float gravity)
    {
        var (yawAngle, pitchAngle) = CalculateCannonAim(position, target, speed, gravity);
        return Quaternion.Euler(-pitchAngle, yawAngle, 0) * Vector3.forward * speed;
    }

    public float PredictCannonballTime(Vector3 spawnPos, Vector3 target, float ks, float kg)
    {
        float x = (spawnPos - target).magnitude * kg / (ks * ks);
        float predictedLandingTime = 2f * ks / kg * Mathf.Sqrt(0.5f * (1 - Mathf.Sqrt(1 - x * x)));
        if (x * x > 1)
        {
            predictedLandingTime = 2f * ks / kg * Mathf.Sqrt(0.5f);
        }
        return predictedLandingTime;
    }

    protected bool IsWithinCannonRange(Vector3 spawnPos, Vector3 target, float speed, float gravity)
    {
        Vector3 position = this.transform.position;
        float distance = (position - target).magnitude;
        float x = distance * gravity / (speed * speed);
        return x >= -1.0f && x <= 1.0f;
    }
}
