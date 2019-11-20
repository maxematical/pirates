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
        // Determine which way is most efficient to turn
        float deltaHeading = Util.GetTurnDirection(_helper.Heading, _state.DesiredHeading) * Settings.MaxRotationSpeed * Time.deltaTime;
        float nextHeading = _helper.Heading + deltaHeading;
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
            // There are two ways that we can turn -- with port side facing the player, or starboard side facing the player
            // We'll choose between the two by checking which is faster to turn to, from our current position
            float headingTowardsPlayer = Util.AngleTowards(_h.SelfPos, _h.TargetPos);
            float broadsideHeading1 = Util.Clamp180(headingTowardsPlayer - 90);
            float broadsideHeading2 = Util.Clamp180(headingTowardsPlayer + 90);

            _gizmosBroadsideHeading1 = broadsideHeading1;
            _gizmosBroadsideHeading2 = broadsideHeading2;

            // Decide between broadsideHeading1 or broadsideHeading2 depending on which is closer to the current heading
            float turnDistance1 = Util.Clamp180(_h.Heading - broadsideHeading1);
            float turnDistance2 = Util.Clamp180(_h.Heading - broadsideHeading2);
            float chosenBroadsideHeading = Mathf.Abs(turnDistance1) < Mathf.Abs(turnDistance2) ? broadsideHeading1 : broadsideHeading2;

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
