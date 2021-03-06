﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AiControl : ShipControl
{
    public GameObject _Player;
    public GameObject _CannonballPrefab;

    [Header("Components and Children")]
    public CaravelModelController _Caravel;
    public Rigidbody _Rigidbody;
    public GameObject _WindForceCenter;
    public ShipBuoyancy _Buoyancy;

    [Header("Settings")]
    public AiSettings _AiSettings;
    public ShipPhysicsSettings _PhysicsSettings;

    private AiHelper _helper;
    private AiState _state;
    private float _lastFireTime;

    public override Rigidbody Rigidbody => _Rigidbody;
    protected override Vector3 WindForcePosition => _WindForceCenter.transform.position;
    protected override ShipPhysicsSettings PhysicsSettings => _PhysicsSettings;
    protected override bool ShouldApplyForces => !(_state is SinkingState);

    void Start()
    {
        _helper = new AiHelper(this.gameObject, _Player, _AiSettings);
        _state = new PursueState(this, _helper);

        _lastFireTime = float.MinValue;
    }

    void Update()
    {
        // Update state
        AiState nextState = _state.Update();

        // Turn towards the desired heading
        float targetTurningSpeed;

        float distanceToTargetHeading = Util.AngleDist(_helper.Heading, _state.DesiredHeading);
        if (distanceToTargetHeading > _AiSettings.SlowRadius)
        {
            targetTurningSpeed = _AiSettings.MaxRotationSpeed;
        }
        else
        {
            targetTurningSpeed = _AiSettings.MaxRotationSpeed * distanceToTargetHeading / _AiSettings.SlowRadius * 0.8f;
        }
        targetTurningSpeed *= Util.GetTurnDirection(_helper.Heading, _state.DesiredHeading);

        RequestedTurningSpeed = targetTurningSpeed;

        // Update speed to desired amount
        RequestedSpeed = _state.DesiredSpeed;

        // Change state to next
        _state = nextState;

        // Animate model
        _Caravel.TargetRudderTilt = 0;
        _Caravel.TargetAimPos = _helper.TargetPos;
        _Caravel.CannonballSpeed = _AiSettings.CannonballSpeed;
        _Caravel.CannonballGravity = _AiSettings.CannonballGravity;
        _Caravel.CannonMaxFiringAngle = _AiSettings.MaxFiringAngle;
    }

    void OnDrawGizmos()
    {
        if (_state?.DesiredHeading != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, _state.DesiredHeading, 0) * Vector3.forward * 5);
        }

        if (_helper?.Heading != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0, _helper.Heading, 0) * Vector3.forward * 5);
        }

        if (_state != null)
        {
            Gizmos.color = _state.GizmosColor;
            Gizmos.DrawCube(transform.position + Vector3.up, Vector3.one * 0.5f);

            _state.DrawGizmos();
        }
    }

    public override void Sink()
    {
        if (!(_state is SinkingState))
        {
            _state = new SinkingState(this);
        }
    }

    private abstract class AiState
    {
        public float DesiredHeading { get; protected set; }
        public float DesiredSpeed { get; protected set; }
        public Vector3? ShootAtPos;

        public abstract Color GizmosColor { get; }

        public abstract AiState Update();
        public virtual void DrawGizmos() { }
    }

    private class PursueState : AiState
    {
        private AiControl _ai;
        private AiHelper _h;

        public override Color GizmosColor => Color.red;

        public PursueState(AiControl ai, AiHelper h)
        {
            _ai = ai;
            _h = h;
        }

        public override AiState Update()
        {
            // Update desired speed/heading
            DesiredHeading = Util.AngleTowards(_h.SelfPos, _h.TargetPos);
            DesiredSpeed = _h.Settings.BaseSpeed * _h.Settings.PursuitSpeedMultiplier;

            // Check if we are too close
            float sqrDistance = _h.SqrDistanceTo(_h.TargetPos);
            if (sqrDistance <= _h.Settings.PursuitStopDistance * _h.Settings.PursuitStopDistance)
            {
                return new CircleState(_ai, _h);
            }

            return this;
        }
    }

    private class CircleState : AiState
    {
        private AiControl _ai;
        private AiHelper _h;

        private float _gizmosBroadsideHeading1;
        private float _gizmosBroadsideHeading2;

        public override Color GizmosColor => Color.blue;

        public CircleState(AiControl ai, AiHelper h)
        {
            _ai = ai;
            _h = h;
        }

        public override AiState Update()
        {
            // Turn such that the AI can easily broadside the player

            // Normally, we want to turn so we are 90 degrees from the player (this is most optimal for shooting)
            // However, if we are far away, this will usually lead to the AI going too perpendicular to the player's
            // path and the AI will drift away from the player.
            // Therefore, we want to constrict the angle the farther away the AI is from the player.
            // (For example, if the player is 9 units away, then we only want to turn 60 degrees away so we will still
            // be close to the player)
            float dist = Mathf.Sqrt(_h.SqrDistanceTo(_h.TargetPos)) / _h.Settings.CircleDistanceMultiplier;
            float broadsideAngle = Mathf.Max(10, 90 + 10 * Mathf.Min(0, 5 - dist));
            float speedMultiplier = 1 + Mathf.Clamp(0.05f * (65 - broadsideAngle), 0, 0.4f); // move faster when angle is constricted

            // There are two ways that we can turn -- with port side facing the player, or starboard side facing the player
            // We'll choose between the two by checking which is faster to turn to, from our current position
            float headingTowardsPlayer = Util.AngleTowards(_h.SelfPos, _h.TargetPos);
            float broadsideHeading1 = Util.Clamp180(headingTowardsPlayer - broadsideAngle);
            float broadsideHeading2 = Util.Clamp180(headingTowardsPlayer + broadsideAngle);

            _gizmosBroadsideHeading1 = broadsideHeading1;
            _gizmosBroadsideHeading2 = broadsideHeading2;

            // Decide between broadsideHeading1 or broadsideHeading2 depending on which is closer to the current heading
            float turnDistance1 = Util.AngleDist(_h.Heading, broadsideHeading1);
            float turnDistance2 = Util.AngleDist(_h.Heading, broadsideHeading2);
            float chosenBroadsideHeading = turnDistance1 < turnDistance2 ? broadsideHeading1 : broadsideHeading2;

            DesiredHeading = chosenBroadsideHeading;
            DesiredSpeed = _h.Settings.BaseSpeed * speedMultiplier;

            // If we are in range, fire some cannonballs
            TryFireCannons();

            // Check if we are too far from the player
            float sqrDistance = _h.SqrDistanceTo(_h.TargetPos);
            if (sqrDistance >= _h.Settings.PursuitStartDistance * _h.Settings.PursuitStartDistance)
            {
                return new PursueState(_ai, _h);
            }

            return this;
        }

        public override void DrawGizmos()
        {
            Vector3 p = _h.SelfPos + Vector3.up * 0.5f;

            Gizmos.color = new Color(0.6f, 0, 0);
            Gizmos.DrawLine(p, p + Quaternion.Euler(0, _gizmosBroadsideHeading1, 0) * Vector3.forward * 2);
            Gizmos.DrawLine(p, p + Quaternion.Euler(0, _gizmosBroadsideHeading2, 0) * Vector3.forward * 2);
        }

        private void TryFireCannons()
        {
            //if (_ai.TimeUntilReloaded > 0 && false)
            {
            //    return;
            }

            // Check that we aren't at too extreme an angle to fire (e.g. don't fire off the bow of the ship)
            float angleTowardsPlayer = Util.AngleTowards(_h.SelfPos, _h.TargetPos);
            float relativeAngle = Util.Clamp180(angleTowardsPlayer - _h.Heading);
            // Angle will be the following:
            // * 0: Front
            // * 180: Back
            // * 0 to 180: Right
            // * -180 to 0: Left
            if (Mathf.Abs(relativeAngle) < _h.Settings.MaxFiringAngle || Mathf.Abs(relativeAngle) > 180 - _h.Settings.MaxFiringAngle)
            {
                return;
            }

            // Check if we are in range, then if necessary, fire
            float sqrDistance = _h.SqrDistanceTo(_h.TargetPos);
            if (sqrDistance <= _h.Settings.CannonRange * _h.Settings.CannonRange)
            {
                int cannonIndex = relativeAngle <= 0 ?
                    _ai._Caravel.GetNextLeftCannonIndex() :
                    _ai._Caravel.GetNextRightCannonIndex();
                var cannon = _ai._Caravel.GetCannon(cannonIndex);

                if (Time.time - cannon.LastFireTime < _ai._AiSettings.ReloadTime ||
                    Time.time - _ai._lastFireTime < _ai._AiSettings.FireInterval)
                {
                    return;
                }
                cannon.LastFireTime = Time.time;
                _ai._lastFireTime = Time.time;

                Vector3 spawnPos = cannon.SpawnPos.transform.position;

                // Basic velocity prediction
                // Predict how long a cannonball would take to land, if it was shot directly to where the target is right
                // now.
                // Then shoot the cannonball to where the target will be in that amount of time
                // This isn't perfect because technically the amount of time for the cannonball to land is dependent on the
                // distance to the target, so if we're changing the distance, we'll get a slightly different answer for where
                // it will land.
                // To solve this, we run multiple iterations of the prediction and cross our fingers it's close enough

                Vector3 predictedTargetPos = Vector3.zero; // will be initialized in the for-loop
                float predictedTime = _ai.PredictCannonballTime(spawnPos, _h.TargetPos, _h.Settings.CannonballSpeed, _h.Settings.CannonballGravity);
                for (int i = 0; i < 25; i++)
                {
                    predictedTargetPos = _h.TargetPos + predictedTime * _h.TargetVelocity;
                    predictedTime = _ai.PredictCannonballTime(spawnPos, predictedTargetPos, _h.Settings.CannonballSpeed, _h.Settings.CannonballGravity);
                }

                // TODO AI cannonballs inherit AI ship velocity, and update prediction to take this into account
                GameObject instantiated = Instantiate(_ai._CannonballPrefab, spawnPos, Quaternion.identity);
                Cannonball cannonball = instantiated.GetComponent<Cannonball>();
                cannonball.Gravity = _h.Settings.CannonballGravity;
                cannonball.Velocity = CalculateCannonballTrajectory(spawnPos, predictedTargetPos, _h.Settings.CannonballSpeed, _h.Settings.CannonballGravity);
                cannonball.IgnoreCollisions = _h.Self;

                _ai._Caravel.PlayCannonEffects(cannonIndex);
            }
        }
    }

    private class SinkingState : AiState
    {
        public override Color GizmosColor => Color.black;

        private AiControl _ai;
        private float _startTime;
        private float _initialDensity;
        private float _initialDrag;

        public SinkingState(AiControl ai)
        {
            _ai = ai;
            _startTime = Time.time;
            _initialDensity = _ai._Buoyancy._Density;
            _initialDrag = _ai._Buoyancy._DragCoefficient;
        }

        public override AiState Update()
        {
            DesiredSpeed = 0;
            DesiredHeading = _ai.transform.eulerAngles.y;

            float time = (Time.time - _startTime) / _ai._AiSettings.SinkingTime;
            
            _ai._Buoyancy._Density = Mathf.Lerp(_initialDensity, _ai._AiSettings.SunkDensity, time);
            _ai._Buoyancy._DragCoefficient = Mathf.Lerp(_initialDrag, _ai._AiSettings.SunkDrag, time);

            if (_ai.transform.position.y <= _ai._AiSettings.DespawnY)
            {
                Destroy(_ai.gameObject);
            }

            return this;
        }
    }

    private class AiHelper
    {
        public GameObject Self { get; }
        public GameObject Target { get; }
        public AiSettings Settings { get; }

        public Vector3 SelfPos { get => Self.transform.position; }
        public Vector3 TargetPos { get => Target.transform.position; }
        public float Heading { get => Self.transform.rotation.eulerAngles.y; }
        public Vector3 TargetVelocity { get => _targetShip?.Rigidbody.velocity?? Vector3.zero; }

        private ShipControl _targetShip;

        public AiHelper(GameObject self, GameObject target, AiSettings settings)
        {
            Self = self;
            Target = target;
            Settings = settings;
            _targetShip = target.GetComponent<ShipControl>();
        }

        public float SqrDistanceTo(Vector3 other)
        {
            return (Self.transform.position - other).sqrMagnitude;
        }
    }

    [Serializable]
    public class AiSettings
    {
        public float BaseSpeed;

        [Header("Pursuit State Settings")]
        public float PursuitStartDistance;
        public float PursuitStopDistance;
        public float PursuitSpeedMultiplier;

        [Header("Circle State Settings")]
        [Tooltip("Multiplier for the distance the AI will try to 'maintain' between the player")]
        public float CircleDistanceMultiplier;

        [Header("Navigation Settings")]
        [Tooltip("The maximum angular speed that can be reached")]
        public float MaxRotationSpeed = 60f;
        [Tooltip("The radius in which the AI will attempt to slow down its rotation, in degrees")]
        public float SlowRadius = 20f;
        [Tooltip("Maximum angular acceleration")]
        public float MaxAngularAcceleration = 20f;
        [Tooltip("Amount of time used to reach target angular velocity, lower values give faster acceleration")]
        public float TimeToTarget = 0.05f;

        [Header("Cannon Settings")]
        public float CannonRange;
        public float MaxFiringAngle;
        [Tooltip("The reload time in seconds for one individual cannon.")]
        public float ReloadTime;
        [Tooltip("The AI will wait at least this long in between firing another cannon shot.")]
        public float FireInterval;
        public float CannonballGravity;
        public float CannonballSpeed;

        [Header("Sinking Settings")]
        [Tooltip("The amount of time it takes to lerp the original density and drag settings to the sunk ones.")]
        public float SinkingTime;
        [Tooltip("The water density constant to use once the ship sinks.")]
        public float SunkDensity;
        [Tooltip("The drag coefficient to use once the ship sinks.")]
        public float SunkDrag;
        [Tooltip("The ship will be destroyed once it is sinking and is at or below this y-position.")]
        public float DespawnY;
    }
}
