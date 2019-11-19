public class Util
{
    public static float ClampAngle(float angle)
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
}
