using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ShipControl : MonoBehaviour
{
    protected float RequestedSpeed;
    protected float RequestedTurningSpeed;

    public abstract Rigidbody Rigidbody { get; }
    protected abstract Vector3 WindForcePosition { get; }
    protected abstract ShipPhysicsSettings PhysicsSettings { get; }
    protected virtual bool ShouldApplyForces => true;

    private Vector3 _currentCannonTorque;

    void Start()
    {
        RequestedSpeed = 0;
    }

    public abstract void Sink();

    protected virtual void FixedUpdate()
    {
        if (!ShouldApplyForces)
        {
            return;
        }

        // Apply forward force
        Vector3 windForce = transform.forward * Util.Cap(RequestedSpeed - GetSpeed(), 0.15f) * PhysicsSettings.WindMultiplier;
        Rigidbody.AddForceAtPosition(windForce, WindForcePosition);

        // Apply turning force
        float currentYawSpeed = Mathf.Abs(Rigidbody.angularVelocity.y);
        Rigidbody.AddTorque(transform.up * PhysicsSettings.TurnMultipler * (RequestedTurningSpeed - currentYawSpeed));

        // Update cannon torque
        if (_currentCannonTorque.sqrMagnitude > PhysicsSettings.MaxCannonTorque * PhysicsSettings.MaxCannonTorque)
        {
            _currentCannonTorque.Normalize();
            _currentCannonTorque *= PhysicsSettings.MaxCannonTorque;
        }
        // Apply the torque
        Rigidbody.AddTorque(_currentCannonTorque);
        // Reduce the torque magnitude over time
        _currentCannonTorque *= (1f - Time.fixedDeltaTime * PhysicsSettings.CannonTorqueDecay);
        if (_currentCannonTorque.sqrMagnitude <= 0.05f)
        {
            _currentCannonTorque = Vector3.zero;
        }
    }

    protected void ApplyCannonTorque(Vector3 cannonballVelocity, Vector3 cannonballSpawnPos)
    {
        // Add torque from cannonball
        Vector3 localCannonForce = transform.rotation * -cannonballVelocity.normalized * PhysicsSettings.CannonPushForce;
        localCannonForce.y = Mathf.Abs(localCannonForce.y);

        Vector3 localSpawnPos = transform.worldToLocalMatrix.MultiplyPoint3x4(cannonballSpawnPos);

        Vector3 relativeTorque = Vector3.Cross(localSpawnPos - Rigidbody.centerOfMass, localCannonForce);
        relativeTorque.x = relativeTorque.y = 0;

        _currentCannonTorque += Quaternion.Inverse(transform.rotation) * relativeTorque;
    }

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;
        ShipControl otherShip = other.GetComponent<ShipControl>();

        // Handle ramming with enemy ship
        if (otherShip != null)
        {
            // Compute the average dot product between the contact normals and the forward direction
            // The closer it is to -1 (i.e. average normal and forward are opposite), the more directly we are going
            // into the collision, and the more damage we will deal to them
            float avgDot = 0;
            for (int i = 0; i < collision.contactCount; i++)
            {
                avgDot += Vector3.Dot(collision.GetContact(i).normal, transform.forward);
            }
            avgDot /= Mathf.Max(1, collision.contactCount);

            // The closer we are to -1, the less damage we deal to this ship
            float damageToThisShip = Mathf.Abs(-1 - avgDot);

            // Square the damage then scale it by the velocity of the collision
            damageToThisShip *= damageToThisShip;
            damageToThisShip *= collision.relativeVelocity.magnitude * 2f;

            // Deal the damage to this ship
            GetComponent<ShipHealth>().Health -= Mathf.CeilToInt(damageToThisShip);
        }
    }

    protected string GetDebugText()
    {
        string result = "";

        result += $"Forward speed: {GetSpeed()} / Desired {RequestedSpeed}\n";

        float yawSpeed = Rigidbody.angularVelocity.y;
        result += $"Yaw speed: {Mathf.Round(yawSpeed * Mathf.Rad2Deg)} deg/s\n";

        float cannonTorque = _currentCannonTorque.magnitude;
        result += $"Cannon torque: {cannonTorque}\n";

        return result;
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

    private float GetSpeed()
    {
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();
        Quaternion forwardRotation = Quaternion.LookRotation(forward);

        return (Quaternion.Inverse(forwardRotation) * Rigidbody.velocity).z;
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

    [Serializable]
    public class ShipPhysicsSettings
    {
        public float WindMultiplier;
        public float TurnMultipler;

        public float CannonPushForce;
        public float MaxCannonTorque;
        public float CannonTorqueDecay;
    }
}
