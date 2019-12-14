using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CaravelModelController : MonoBehaviour
{
    public GameObject Rudder;
    public Transform RudderAxis;
    public float RudderTiltSpeed;

    public List<CannonSettings> Cannons;

    public GameObject FireVFXPrefab;
    public float FireVFXScale;

    public float TargetRudderTilt { private get; set; }
    private float _rudderTilt;

    public Vector3 TargetAimPos { private get; set; }
    public float CannonballSpeed { private get; set; }
    public float CannonballGravity { private get; set; }

    // Maximum yaw angle that the cannons can be fired from, measured in degrees from helm/stern
    public float CannonMaxFiringAngle { private get; set; }

    public Vector3 Velocity { private get; set; }

    private int _nextLeftCannonIndex;
    private int _nextRightCannonIndex;
    private int _numberLeftCannons;
    private int _numberRightCannons;

    private Quaternion _rudderInitialRotation;

    private void Start()
    {
        _rudderInitialRotation = Rudder.transform.rotation;

        foreach (CannonSettings cannon in Cannons)
        {
            cannon.InitialRotation = cannon.Barrel.transform.localRotation;
            cannon.InitialBaseRotation = cannon.Base.transform.localRotation;
        }

        UpdateCannonCount();
        InstantiateParticleSystems();
    }

    private void OnValidate()
    {
        UpdateCannonCount();
    }

    void Update()
    {
        // Update rudder
        if (Mathf.Abs(_rudderTilt - TargetRudderTilt) <= RudderTiltSpeed * Time.deltaTime)
        {
            _rudderTilt = TargetRudderTilt;
        }
        else
        {
            _rudderTilt += RudderTiltSpeed * Time.deltaTime * Mathf.Sign(TargetRudderTilt - _rudderTilt);
        }
        Rudder.transform.rotation = Quaternion.AngleAxis(_rudderTilt, RudderAxis.right) * _rudderInitialRotation;

        // Update cannons
        if (TargetAimPos != Vector3.zero)
        {
            foreach (CannonSettings cannonSettings in Cannons)
            {
                GameObject barrel = cannonSettings.Barrel;
                GameObject spawnPos = cannonSettings.SpawnPos;
                Quaternion initialRotation = cannonSettings.InitialRotation;
                Quaternion initialBaseRotation = cannonSettings.InitialBaseRotation;

                initialBaseRotation = Quaternion.Euler(initialBaseRotation.eulerAngles.x, 0, initialBaseRotation.eulerAngles.z);

                var (yaw, pitch) = ShipControl.CalculateCannonAim(spawnPos.transform.position, TargetAimPos, CannonballSpeed,
                    CannonballGravity);

                // Ensure the yaw angle isn't too far to either side
                float relativeYaw = Util.Clamp180(yaw - transform.rotation.eulerAngles.y);

                float clampMin = CannonMaxFiringAngle;
                float clampMax = 180 - CannonMaxFiringAngle;
                if (cannonSettings.IsRightSide)
                {
                    float oldMin = clampMin;
                    clampMin = -clampMax;
                    clampMax = -oldMin;
                }
                if (relativeYaw < clampMin || relativeYaw > clampMax)
                {
                    // relativeYaw = Util.ClosestAngle(relativeYaw, clampMin, clampMax);
                    continue;
                }
                float cannonYaw = relativeYaw + 90;

                Quaternion baseRotation = Quaternion.AngleAxis(cannonYaw, Vector3.up) * initialBaseRotation;
                barrel.transform.localRotation = Quaternion.AngleAxis(pitch, Vector3.right) * initialRotation;
                cannonSettings.Base.transform.localRotation = baseRotation;
            }
        }
    }

    public int GetNextLeftCannonIndex()
    {
        int index = _nextLeftCannonIndex;
        _nextLeftCannonIndex = (index + 1) % _numberLeftCannons;
        return index;
    }

    public int GetNextRightCannonIndex()
    {
        int index = _nextRightCannonIndex;
        _nextRightCannonIndex = _numberLeftCannons + ((index + 1) % _numberRightCannons);
        return index;
    }

    public CannonSettings GetCannon(int index)
    {
        return Cannons[index];
    }

    public void PlayCannonEffects(int index)
    {
        CannonSettings cannon = Cannons[index];

        float particleRotation = cannon.Particles.transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
        var mainParticlesModule = cannon.Particles.main;
        mainParticlesModule.startRotation = particleRotation;

        var velocityModule = cannon.Particles.GetComponent<ParticleSystem>().velocityOverLifetime;
        velocityModule.x = Velocity.x * 10;
        velocityModule.y = Velocity.y * 10;
        velocityModule.z = Velocity.z * 10;

        cannon.Particles.Play();
    }

    private void UpdateCannonCount()
    {
        _numberLeftCannons = 0;
        _numberRightCannons = 0;
        foreach (CannonSettings cannon in Cannons)
        {
            if (cannon.IsRightSide)
                _numberRightCannons++;
            else
                _numberLeftCannons++;
        }

        _nextLeftCannonIndex %= _numberLeftCannons;
        _nextRightCannonIndex = _numberLeftCannons + (_nextRightCannonIndex - _numberLeftCannons) % _numberRightCannons;
    }

    private void InstantiateParticleSystems()
    {
        foreach (CannonSettings cannon in Cannons)
        {
            GameObject vfx = Instantiate(FireVFXPrefab, cannon.SpawnPos.transform);
            vfx.transform.localScale = Vector3.one * FireVFXScale;

            if (cannon.IsRightSide)
            {
                vfx.transform.localRotation = Quaternion.Euler(180, 0, 0);
            }

            cannon.Particles = vfx.GetComponent<ParticleSystem>();
            //cannon.Particles.Stop();
        }
    }

    [Serializable]
    public class CannonSettings
    {
        public GameObject Barrel;
        public GameObject Base;
        public GameObject SpawnPos;
        public bool IsRightSide;

        public Quaternion InitialRotation { get; set; }
        public Quaternion InitialBaseRotation { get; set; }
        public ParticleSystem Particles { get; set; }

        public float LastFireTime { get; set; } = float.MinValue;
    }
}
