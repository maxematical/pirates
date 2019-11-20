using System;
using UnityEngine;

[Serializable]
public struct RangeFloat
{
    public float Min;
    public float Max;

    public float RandomInRange { get => Min + UnityEngine.Random.value * (Max - Min); }
}
