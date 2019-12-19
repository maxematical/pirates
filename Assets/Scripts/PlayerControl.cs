using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerControl : ShipControl
{
    public Text _DebugText;

    [Header("Prefabs")]
    public GameObject CannonballPrefab;

    [Header("Components and Children")]
    public CaravelModelController _Caravel;
    public Rigidbody _Rigidbody;
    public GameObject _WindForceCenter;

    [Header("Controls Settings")]
    public float _BaseSpeed;
    public float _ReloadTime;
    [Tooltip("The turning speed, in deg/s")]
    public float _TurningSpeed;
    [Tooltip("Percent of speed that will be lost while turning at the maximum rate (0-1)")]
    public float RotationSpeedReduction;
    public ShipPhysicsSettings _PhysicsSettings;

    [Header("Cannon Settings")]
    [Tooltip("The maximum horizontal angle that cannonballs can be fired from, measured from the helm/stern, in degrees")]
    public float MaxFiringAngle;
    public float CannonballSpeed;
    public float CannonballGravity;

    // Overrides
    public override Rigidbody Rigidbody => _Rigidbody;
    protected override Vector3 WindForcePosition => _WindForceCenter.transform.position;
    protected override ShipPhysicsSettings PhysicsSettings => _PhysicsSettings;

    void Start()
    {
    }

    void Update()
    {
        // Apply rotation input
        float rotateInput = 0f;
        if (Input.GetKey(KeyCode.A)) rotateInput -= 1f;
        if (Input.GetKey(KeyCode.D)) rotateInput += 1f;

        RequestedTurningSpeed = _TurningSpeed * rotateInput;

        // Animate model
        _Caravel.TargetRudderTilt = rotateInput * 30;
        _Caravel.TargetAimPos = GetMouseoverPosition() ?? Vector3.zero;
        _Caravel.CannonballSpeed = CannonballSpeed;
        _Caravel.CannonballGravity = CannonballGravity;
        _Caravel.CannonMaxFiringAngle = MaxFiringAngle;

        // Handle cannon firing
        HandleFireInput();

        // Update on-screen debug info
        _DebugText.text = GetDebugText();
    }

    public override void Sink()
    {
        Destroy(gameObject);
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
                    _Caravel.GetNextLeftCannonIndex() :
                    _Caravel.GetNextRightCannonIndex();
                var cannon = _Caravel.GetCannon(cannonIndex);

                // Check that we can fire the cannon (aren't reloading)
                float timeSinceFired = Time.time - cannon.LastFireTime;
                if (timeSinceFired < _ReloadTime)
                {
                    return;
                }
                cannon.LastFireTime = Time.time;

                // Fire a cannonball from the cannon
                Vector3 spawnPos = cannon.SpawnPos.transform.position;

                GameObject instantiated = Instantiate(CannonballPrefab, spawnPos, Quaternion.identity);
                Vector3 cannonballVelocity = CalculateCannonballTrajectory(spawnPos, target, CannonballSpeed, CannonballGravity) + _Rigidbody.velocity;

                Cannonball cannonball = instantiated.GetComponent<Cannonball>();
                cannonball.Velocity = cannonballVelocity;
                cannonball.Gravity = CannonballGravity;
                cannonball.IgnoreCollisions = this.gameObject;

                // Play cannon effects
                _Caravel.PlayCannonEffects(cannonIndex);
                ApplyCannonTorque(cannonballVelocity, spawnPos);
            }
        }
    }

    protected override void FixedUpdate()
    {
        float turnFactor = Mathf.Abs(_Rigidbody.angularVelocity.y * Mathf.Rad2Deg) / _TurningSpeed;
        RequestedSpeed = (1.0f - turnFactor * RotationSpeedReduction) * _BaseSpeed;
        base.FixedUpdate();
    }

    private Vector3? GetMouseoverPosition()
    {
        Vector2 mousePos = Input.mousePosition;

        RaycastHit hitInfo;
        bool hit = Physics.Raycast(Camera.main.ScreenPointToRay(mousePos), out hitInfo, 100f, LayerMask.GetMask("Sea"));
        return hit ? hitInfo.point : (Vector3?)null;
    }
}
