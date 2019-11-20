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
