using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AiControl : ShipControl
{
    public GameObject Player;
    public AiSettings Settings;

    public GameObject CannonballPrefab;

    public float TimeUntilReloaded { get; set; }

    private AiHelper _helper;
    private AiState _state;
    private float _angularVelocity;

    void Start()
    {
        _helper = new AiHelper(this.gameObject, Player, Settings);
        _state = new PursueState(this, _helper);
        _angularVelocity = 0;

        TimeUntilReloaded = 0;
    }

    void Update()
    {
        // Update reload
        TimeUntilReloaded = Mathf.Max(0, TimeUntilReloaded - Time.deltaTime);

        // Update state
        AiState nextState = _state.Update();

        // Update heading/speed based on desired values

        // Turn towards desired heading
        float deltaRotation = Util.Clamp180(_helper.Heading - _state.DesiredHeading);
        float distanceToTargetHeading = Util.AngleDist(_helper.Heading, _state.DesiredHeading); // AKA rotationSize
        float turnDir = Util.GetTurnDirection(_helper.Heading, _state.DesiredHeading);

        float angularAcceleration = 0;

        // Stop within 10 degrees of the target
        if (distanceToTargetHeading < 10 && false)
        {
            angularAcceleration = 0;
        }
        else
        {
            float maxRotationSpeed = 60f; // maxRotation
            float slowRadius = 8f;
            float maxAngularAccleration = 30f;
            float timeToTarget = 0.1f;
            
            float targetAngularVelocity;
            // Begin to slow within 30 degrees
            if (distanceToTargetHeading > slowRadius)
            {
                targetAngularVelocity = maxRotationSpeed;
            }
            else
            {
                targetAngularVelocity = maxRotationSpeed * distanceToTargetHeading / slowRadius;
            }
            targetAngularVelocity *= Util.GetTurnDirection(_helper.Heading, _state.DesiredHeading);

            angularAcceleration = (targetAngularVelocity - _angularVelocity) / timeToTarget;

            if (Mathf.Abs(angularAcceleration) > maxAngularAccleration)
            {
                //angularAccleration = Mathf.Sign(angularAcceleration) * maxAngularAcceleration;
            }
            //angularAcceleration = Mathf.Min(maxAngularAccleration, angular);
        }
        Debug.Log(angularAcceleration);

        // Determine which way is most efficient to turn
        //float angularAcceleration = Util.GetTurnDirection(_helper.Heading, _state.DesiredHeading) * Settings.RotationAcceleration;
        float nextAngularVelocity = _angularVelocity + angularAcceleration * Time.deltaTime;
        nextAngularVelocity = Mathf.Sign(nextAngularVelocity) * Mathf.Min(Mathf.Abs(nextAngularVelocity), Settings.MaxRotationSpeed); // cap angular velocity

        _angularVelocity = nextAngularVelocity;
        float nextHeading = _helper.Heading + _angularVelocity * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0, nextHeading, 0);

        Speed = _state.DesiredSpeed;

        // Change state to next
        _state = nextState;
    }

    private void OnDrawGizmos()
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
            float dist = Mathf.Sqrt(_h.SqrDistanceTo(_h.TargetPos));
            float broadsideAngle = Mathf.Max(10, 90 + 30 * Mathf.Min(0, 7 - dist));

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

            // Don't actually turn if the difference is small enough
            if (Mathf.Abs(Util.Clamp180(_h.Heading - chosenBroadsideHeading)) < _h.Settings.MaxBroadsideAngle)
            {
                //chosenBroadsideHeading = _h.Heading;
            }

            DesiredHeading = chosenBroadsideHeading;
            DesiredSpeed = _h.Settings.BaseSpeed;

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
            if (_ai.TimeUntilReloaded > 0)
            {
                return;
            }

            // Check that we aren't at too extreme an angle to fire (e.g. don't fire off the bow of the ship)
            float angleTowardsPlayer = Util.AngleTowards(_h.SelfPos, _h.TargetPos);
            float relativeAngle = Util.Clamp180(angleTowardsPlayer - _h.Heading);
            // Angle will be the following:
            // * 0: Front
            // * 180: Back
            // * 0 to 180: Right
            // * -180 to 0: Left
            if (Mathf.Abs(relativeAngle) < _h.Settings.CannonAngle || Mathf.Abs(relativeAngle) > 180 - _h.Settings.CannonAngle)
            {
                return;
            }

            // Check if we are in range, then if necessary, fire
            float sqrDistance = _h.SqrDistanceTo(_h.TargetPos);
            if (sqrDistance <= _h.Settings.CannonRange * _h.Settings.CannonRange)
            {
                Vector3 spawnPos = _h.SelfPos;

                GameObject instantiated = Instantiate(_ai.CannonballPrefab, spawnPos, Quaternion.identity);
                Cannonball cannonball = instantiated.GetComponent<Cannonball>();
                cannonball.Gravity = _h.Settings.CannonballGravity;
                cannonball.Velocity = _ai.CalculateCannonballTrajectory(spawnPos, _h.TargetPos, _h.Settings.CannonballSpeed, _h.Settings.CannonballGravity);
                cannonball.IgnoreCollisions = _h.Self;

                _ai.TimeUntilReloaded = _ai.Settings.ReloadSpeed.RandomInRange;
            }
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

        public AiHelper(GameObject self, GameObject target, AiSettings settings)
        {
            Self = self;
            Target = target;
            Settings = settings;
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
        public float MaxBroadsideAngle;

        [Header("Navigation Settings")]
        public float MaxRotationSpeed;
        public float RotationAcceleration;

        [Header("Cannon Settings")]
        public float CannonRange;
        public float CannonAngle;
        public RangeFloat ReloadSpeed;

        public float CannonballGravity;
        public float CannonballSpeed;
    }
}
