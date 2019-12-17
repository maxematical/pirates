using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public GameObject ToFollow;
    public float _CameraSpeed;

    private Vector3 _initialOffset;

    void Start()
    {
        Check.NotNull(ToFollow, "ToFollow field");

        _initialOffset = this.transform.position - ToFollow.transform.position;
    }

    void Update()
    {
        Vector3 target = ToFollow.transform.position;
        target.y = 0;
        transform.position = Vector3.Lerp(transform.position, target + _initialOffset, _CameraSpeed * Time.deltaTime);
    }
}
