using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VectorExtension
{
    public static Vector3 Mul(this Vector3 self, Vector3 other)
    {
        return new Vector3(self.x * other.x, self.y * other.y, self.z * other.z);
    }

    public static Vector3 Div(this Vector3 self, Vector3 other)
    {
        if(other.x==0||other.y==0||other.z==0) throw new System.Exception("The divisor vector component cannot be zero!");
        return new Vector3(self.x / other.x, self.y / other.y, self.z / other.z);
    }

    public static bool isLessThan(this Vector3 self, Vector3 other)
    {
        return self.x < other.x && self.y < other.y && self.z < other.z;
    }
    public static bool isGreaterThan(this Vector3 self, Vector3 other)
    {
        return self.x > other.x && self.y > other.y && self.z > other.z;
    }

    public static bool isLessEqual(this Vector3 self, Vector3 other)
    {
        return self.x <= other.x && self.y <= other.y && self.z <= other.z;
    }

    public static bool isGreaterEqual(this Vector3 self, Vector3 other)
    {
        return self.x >= other.x && self.y >= other.y && self.z >= other.z;
    }

    public static float MaxComponent(this Vector3 self)
    {
        return Mathf.Max(self.x, Mathf.Max(self.y, self.z));
    }
    
    public static float MinComponent(this Vector3 self)
    {
        return Mathf.Min(self.x, Mathf.Min(self.y, self.z));
    }
    
    public static Vector3Int ToVector3Int(this Vector3 value)
    {
        return new Vector3Int((int)value.x , (int)value.y, (int)value.z);
    }

}
