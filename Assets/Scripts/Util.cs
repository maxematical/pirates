using UnityEngine;

public class Util
{
    public static float Clamp180(float angle)
    {
        angle %= 360;
        if (angle <= -180)
        {
            angle += 360;
        }
        if (angle > 180)
        {
            angle -= 360;
        }
        return angle;
    }

    public static float Clamp360(float angle)
    {
        angle %= 360;
        if (angle < 0)
        {
            angle += 360;
        }
        return angle;
    }

    public static float AngleDist(float here, float there)
    {
        return Mathf.Abs(Clamp180(here - there));
    }

    public static float ClosestAngle(float here, float one, float two)
    {
        float dist1 = AngleDist(here, one);
        float dist2 = AngleDist(here, two);
        return dist1 <= dist2 ? one : two;
    }

    /// <summary>
    /// Caps the given number such that its absolute value will not exceed the maximum.
    /// </summary>
    /// <param name="n">the number to cap</param>
    /// <param name="max">the maximum absolute value for the number</param>
    /// <returns>the number capped so that its absolute value does not exceed max</returns>
    public static float Cap(float n, float max)
    {
        return Mathf.Sign(n) * Mathf.Min(Mathf.Abs(n), max);
    }

    public static int GetTurnDirection(float currentAngle, float targetAngle)
    {
        float way1 = Clamp360(currentAngle - targetAngle);
        float way2 = 360 - way1;
        return way1 < way2 ? -1 : 1;
    }

    public static float AngleTowards(Vector3 here, Vector3 target)
    {
        return -Mathf.Atan2(target.z - here.z, target.x - here.x) * Mathf.Rad2Deg + 90;
    }
}
