using System;

public class Check
{
    public static void NotNull(object o, string message)
    {
        if (o == null)
        {
            throw new InvalidOperationException("Provided object is null: " + message);
        }
    }
}
