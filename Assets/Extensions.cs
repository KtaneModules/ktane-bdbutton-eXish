using UnityEngine;

public static class Extensions
{
    public static Color Inverse(this Color c)
    {
        return new Color(1f - c.r, 1f - c.g, 1f - c.b);
    }
}