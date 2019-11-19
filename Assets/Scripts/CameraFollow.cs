using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public GameObject ToFollow;

    private Vector3 _initialOffset;

    void Start()
    {
        Check.NotNull(ToFollow, "ToFollow field");

        _initialOffset = this.transform.position - ToFollow.transform.position;
    }

    void Update()
    {
        transform.position = ToFollow.transform.position + _initialOffset;
    }
}
