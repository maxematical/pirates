﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerControl : ShipControl
{
    public float BaseSpeed;

    public GameObject CannonballPrefab;
    public GameObject CannonballSpawnL;
    public GameObject CannonballSpawnR;

    public CaravelModelController Caravel;
    public Rigidbody _Rigidbody;

    public Text _DebugText;

    public GameObject _WindForceCenter;
    public float _WindMultiplier;

    [Tooltip("The turning speed, in deg/s")]
    public float _TurningSpeed;
    public float _TurningTorque;

    [Tooltip("The maximum horizontal angle that cannonballs can be fired from, measured from the helm/stern, in degrees")]
    public float MaxFiringAngle;

    public float CannonballSpeed;
    public float CannonballGravity;

    [Header("Controls Settings")]
    [Tooltip("Max rotation speed in deg/s")]
    public float MaxRotationSpeed;

    [Tooltip("Max rotation acceleration in deg/s^2")]
    public float RotationAcceleration;

    [Tooltip("Percent of angular velocity lost, in deg/s (0-1)")]
    public float RotationDamping;

    [Tooltip("Percent of speed that will be lost while turning (0-1)")]
    public float RotationSpeedReduction;

    [Header("Roll Animation Settings")]
    [Tooltip("The rotation speed which will yield the most roll, in deg/s")]
    public float MaxRollSpeed;

    [Tooltip("The maximum roll, in deg")]
    public float MaxRoll;

    public float _CannonPushForce;
    public float _CannonForceDecay;
    public float _MaxCannonTorque;

    private float _angularVelocity;
    private Vector3 _currentCannonTorque;

    void Start()
    {
        _angularVelocity = 0;
    }

    void Update()
    {
        HandleRotationInput();
        HandleFireInput();

        // Update rotation
        float heading = transform.rotation.eulerAngles.y;
        float nextHeading = heading + _angularVelocity * Time.deltaTime;
        float nextRoll = ComputeRoll();

        Quaternion nextRotation = Quaternion.AngleAxis(nextHeading, Vector3.up);
        nextRotation = Quaternion.AngleAxis(nextRoll, nextRotation * Vector3.forward) * nextRotation;
        //transform.rotation = nextRotation;

        UpdateDebugText();
    }

    private void HandleRotationInput()
    {
        // Apply user input
        float rotateInput = 0f;
        if (Input.GetKey(KeyCode.A)) rotateInput -= 1f;
        if (Input.GetKey(KeyCode.D)) rotateInput += 1f;
        _angularVelocity += rotateInput * RotationAcceleration * Time.deltaTime;

        // Apply damping when no user input
        if (rotateInput == 0)
        {
            _angularVelocity *= Mathf.Pow(1 - RotationDamping, Time.deltaTime);
        }

        // Cap angular velocity
        if (Mathf.Abs(_angularVelocity) > MaxRotationSpeed)
        {
            _angularVelocity = MaxRotationSpeed * Mathf.Sign(_angularVelocity);
        }
        if (Mathf.Abs(_angularVelocity) < 0.0001f)
        {
            _angularVelocity = 0;
        }

        // Animate model
        Caravel.TargetRudderTilt = rotateInput * 30;

        // Apply rotation
        float currentYawSpeed = Mathf.Abs(_Rigidbody.angularVelocity.y);
        _Rigidbody.AddTorque(transform.up * rotateInput * _TurningTorque * (_TurningSpeed - currentYawSpeed));
    }

    private void HandleFireInput()
    {
        if (Input.GetButtonDown("Fire"))
        {
            Vector3? mouseoverPos = GetMouseoverPosition();
            if (mouseoverPos.HasValue)
            {
                Vector3 target = mouseoverPos.Value;
                float angle = Util.AngleTowards(transform.position, target);
                float relativeAngle = Util.Clamp180(angle - transform.rotation.eulerAngles.y);

                // Angle will be the following:
                // * 0: Front
                // * 180: Back
                // * 0 to 180: Right
                // * -180 to 0: Left

                // Ensure the angle is within bounds (must not be too close to the front or the back)
                if (Mathf.Abs(relativeAngle) < MaxFiringAngle || Mathf.Abs(relativeAngle) > 180 - MaxFiringAngle)
                {
                    return;
                }

                GameObject spawner = relativeAngle <= 0 ? CannonballSpawnL : CannonballSpawnR;
                Vector3 spawnPos = spawner.transform.position;

                GameObject instantiated = Instantiate(CannonballPrefab, spawnPos, Quaternion.identity);
                Vector3 cannonballVelocity = CalculateCannonballTrajectory(spawnPos, target, CannonballSpeed, CannonballGravity) + this.Velocity;

                Cannonball cannonball = instantiated.GetComponent<Cannonball>();
                cannonball.Velocity = cannonballVelocity;
                cannonball.Gravity = CannonballGravity;
                cannonball.IgnoreCollisions = this.gameObject;

                // Add force from cannonball
                Vector3 localCannonForce = transform.rotation * -cannonballVelocity.normalized * _CannonPushForce;
                localCannonForce.y = Mathf.Abs(localCannonForce.y);

                Vector3 localSpawnPos = transform.worldToLocalMatrix.MultiplyPoint3x4(spawnPos);

                Vector3 relativeTorque = Vector3.Cross(localSpawnPos - _Rigidbody.centerOfMass, localCannonForce);
                relativeTorque.x = relativeTorque.y = 0;

                _currentCannonTorque += transform.rotation * relativeTorque;
            }
        }
    }

    private Vector3? GetMouseoverPosition()
    {
        Vector2 mousePos = Input.mousePosition;

        RaycastHit hitInfo;
        bool hit = Physics.Raycast(Camera.main.ScreenPointToRay(mousePos), out hitInfo, 100f, LayerMask.GetMask("Sea"));
        return hit ? hitInfo.point : (Vector3?)null;
    }

    private void UpdateDebugText()
    {
        string result = "";

        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();
        result += $"Forward speed: {Vector3.Project(Quaternion.Inverse(transform.rotation) * _Rigidbody.velocity, Vector3.forward).z} / Desired {BaseSpeed}\n";

        float yawSpeed = _Rigidbody.angularVelocity.y;
        result += $"Yaw speed: {Mathf.Round(yawSpeed * Mathf.Rad2Deg)} deg/s\n";

        float cannonTorque = _currentCannonTorque.magnitude;
        result += $"Cannon torque: {cannonTorque}\n";

        _DebugText.text = result;
    }

    protected override void FixedUpdate()
    {
        float turnFactor = Mathf.Abs(_angularVelocity) / MaxRotationSpeed; // 0 = not turning at all, 1 = turning at maximum rate
        Speed = (1.0f - turnFactor * RotationSpeedReduction) * BaseSpeed;
        base.FixedUpdate();

        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();
        float currentSpeed = Vector3.Project(Quaternion.Inverse(transform.rotation) * _Rigidbody.velocity, Vector3.forward).z;
        Vector3 windForce = transform.forward * Util.Cap(Speed - currentSpeed, 0.15f) * _WindMultiplier;
        _Rigidbody.AddForceAtPosition(windForce, _WindForceCenter.transform.position);

        Vector3 velocityXZ = _Rigidbody.velocity;
        velocityXZ.y = 0;
        //Debug.Log(velocityXZ.magnitude + " / " + Speed);

        // Update cannon torque
        if (_currentCannonTorque.sqrMagnitude > _MaxCannonTorque * _MaxCannonTorque)
        {
            _currentCannonTorque.Normalize();
            _currentCannonTorque *= _MaxCannonTorque;
        }

        _Rigidbody.AddTorque(_currentCannonTorque);
        _currentCannonTorque *= (1f - Time.fixedDeltaTime * _CannonForceDecay);
        if (_currentCannonTorque.sqrMagnitude <= 0.05f)
        {
            _currentCannonTorque = Vector3.zero;
        }
    }

    private float ComputeRoll()
    {
        float x = -5 * _angularVelocity / MaxRollSpeed;
        float ex = Mathf.Pow((float)Math.E, x);
        float d = ex / (ex + 1);
        return MaxRoll * (d - 0.5f);
    }
}
