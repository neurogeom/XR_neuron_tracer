using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential, Pack = 0)]
public struct SdfNode
{
  public static readonly int Stride = 2 * sizeof(int) + 4 * sizeof(float);
  public Vector4 posRad;
  public int parentIndex;
  public int type;

  public SdfNode(Vector3 position, float radius, int parentIndex, int type)
  {
    this.posRad = new Vector4(position.x, position.y, position.z, radius);
    this.parentIndex = parentIndex;
    this.type = type;
  }
}
