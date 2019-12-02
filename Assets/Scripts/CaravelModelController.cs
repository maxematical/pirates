using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaravelModelController : MonoBehaviour
{
    public GameObject Rudder;
    public Transform RudderAxis;
    public float RudderTiltSpeed;

    public float TargetRudderTilt { private get; set; }
    private float _rudderTilt;

    private Quaternion _rudderInitialRotation;

    private void Start()
    {
        _rudderInitialRotation = Rudder.transform.rotation;
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
    }
}
