using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public GameObject _Player;

    private void Update()
    {
        Vector3 position = transform.position;
        position.x = _Player.transform.position.x;
        position.z = _Player.transform.position.z;
        transform.position = position;
    }
}
