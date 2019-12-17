using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerControl : ShipControl
{
    public float BaseSpeed;

    [Header("Prefabs")]
    public GameObject CannonballPrefab;

    [Header("Components and Children")]
    public GameObject CannonballSpawnL;
    public GameObject CannonballSpawnR;
    public CaravelModelController Caravel;
    public Rigidbody _Rigidbody;
    public GameObject _WindForceCenter;

    public Text _DebugText;

    public float ReloadTime;

    [Header("Controls Settings")]
    public float _WindMultiplier;
    [Tooltip("The turning speed, in deg/s")]
    public float _TurningSpeed;
    public float _TurningTorque;
    [Tooltip("Percent of speed that will be lost while turning (0-1)")]
    public float RotationSpeedReduction;

    [Header("Cannon Settings")]
    [Tooltip("The maximum horizontal angle that cannonballs can be fired from, measured from the helm/stern, in degrees")]
    public float MaxFiringAngle;
    public float CannonballSpeed;
    public float CannonballGravity;

    [Header("Cannon Physics Settings")]
    public float _CannonPushForce;
    public float _CannonForceDecay;
    public float _MaxCannonTorque;

    private Vector3 _currentCannonTorque;

    void Start()
    {
    }

    void Update()
    {
        HandleRotationInput();
        HandleFireInput();
        UpdateDebugText();
    }

    private void HandleRotationInput()
    {
        // Query user input
        float rotateInput = 0f;
        if (Input.GetKey(KeyCode.A)) rotateInput -= 1f;
        if (Input.GetKey(KeyCode.D)) rotateInput += 1f;

        // Apply torque
        float currentYawSpeed = Mathf.Abs(_Rigidbody.angularVelocity.y);
        _Rigidbody.AddTorque(transform.up * rotateInput * _TurningTorque * (_TurningSpeed - currentYawSpeed));

        // Animate model
        Caravel.TargetRudderTilt = rotateInput * 30;
        Caravel.TargetAimPos = GetMouseoverPosition() ?? Vector3.zero;
        Caravel.CannonballSpeed = CannonballSpeed;
        Caravel.CannonballGravity = CannonballGravity;
        Caravel.CannonMaxFiringAngle = MaxFiringAngle;
        Caravel.Velocity = Velocity;
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

                int cannonIndex = relativeAngle <= 0 ?
                    Caravel.GetNextLeftCannonIndex() :
                    Caravel.GetNextRightCannonIndex();
                var cannon = Caravel.GetCannon(cannonIndex);

                // Check that we can fire the cannon (aren't reloading)
                float timeSinceFired = Time.time - cannon.LastFireTime;
                if (timeSinceFired < ReloadTime)
                {
                    return;
                }
                cannon.LastFireTime = Time.time;

                // Fire a cannonball from the cannon
                Vector3 spawnPos = cannon.SpawnPos.transform.position;

                GameObject instantiated = Instantiate(CannonballPrefab, spawnPos, Quaternion.identity);
                Vector3 cannonballVelocity = CalculateCannonballTrajectory(spawnPos, target, CannonballSpeed, CannonballGravity) + this.Velocity;

                Cannonball cannonball = instantiated.GetComponent<Cannonball>();
                cannonball.Velocity = cannonballVelocity;
                cannonball.Gravity = CannonballGravity;
                cannonball.IgnoreCollisions = this.gameObject;

                Caravel.PlayCannonEffects(cannonIndex);

                // Add torque from cannonball
                Vector3 localCannonForce = transform.rotation * -cannonballVelocity.normalized * _CannonPushForce;
                localCannonForce.y = Mathf.Abs(localCannonForce.y);

                Vector3 localSpawnPos = transform.worldToLocalMatrix.MultiplyPoint3x4(spawnPos);

                Vector3 relativeTorque = Vector3.Cross(localSpawnPos - _Rigidbody.centerOfMass, localCannonForce);
                relativeTorque.x = relativeTorque.y = 0;

                _currentCannonTorque += Quaternion.Inverse(transform.rotation) * relativeTorque;
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
        result += $"Forward speed: {Vector3.Project(Quaternion.Inverse(transform.rotation) * _Rigidbody.velocity, Vector3.forward).z} / Desired {Speed}\n";

        float yawSpeed = _Rigidbody.angularVelocity.y;
        result += $"Yaw speed: {Mathf.Round(yawSpeed * Mathf.Rad2Deg)} deg/s\n";

        float cannonTorque = _currentCannonTorque.magnitude;
        result += $"Cannon torque: {cannonTorque}\n";

        _DebugText.text = result;
    }

    protected override void FixedUpdate()
    {
        float turnFactor = Mathf.Abs(_Rigidbody.angularVelocity.y * Mathf.Rad2Deg) / _TurningSpeed; // 0 = not turning at all, 1 = turning at maximum rate
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
        
        // Update cannon torque
        // Ensure cannon torque is capped
        if (_currentCannonTorque.sqrMagnitude > _MaxCannonTorque * _MaxCannonTorque)
        {
            _currentCannonTorque.Normalize();
            _currentCannonTorque *= _MaxCannonTorque;
        }
        // Apply the torque
        _Rigidbody.AddTorque(_currentCannonTorque);
        // Reduce the torque magnitude over time
        _currentCannonTorque *= (1f - Time.fixedDeltaTime * _CannonForceDecay);
        if (_currentCannonTorque.sqrMagnitude <= 0.05f)
        {
            _currentCannonTorque = Vector3.zero;
        }
    }
}
