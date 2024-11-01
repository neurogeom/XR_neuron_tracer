using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

public class RayMarcher : MonoBehaviour
{
  private static readonly int TileSize = 8;
  [Range(1.0f, 100000)]
  public float blendDistanceInverse = 1000;
  public bool DrawBoundingVolumes = false;
  public int IsolateBoundingVolumeDepth = -1;
  public bool TestBvhBoundsQuery = false;
  public bool TestBvhRayCast = false;
  public enum HeatMapModeEnum
  {
    None, 
    StepCountPerThread, 
    StepCountPerTile, 
    ShapeCountPerThread, 
    ShapeCountPerTile, 
  }

  private Material material;

  private struct ShaderConstants
  {
    public int BlendDistance;
    [FormerlySerializedAs("SdfShapes")] public int SdfNodes;
    [FormerlySerializedAs("NumSdfShapes")] public int NumSdfNodes;
    public int AabbTree;
    public int AabbTreeRoot;
  }

  private ShaderConstants m_const;
  private ComputeBuffer m_shapes;
  private ComputeBuffer m_aabbTree;
  
  private void Start()
  {
    InitShaderConstants();
    material = GetComponent<MeshRenderer>().material;
  }

  private void InitShaderConstants()
  {
    m_const.SdfNodes = Shader.PropertyToID("_SdfNodes");
    m_const.NumSdfNodes = Shader.PropertyToID("_NumSdfNodes");
    m_const.AabbTree = Shader.PropertyToID("_AabbTree");
    m_const.AabbTreeRoot = Shader.PropertyToID("_AabbTreeRoot");
    m_const.BlendDistance = Shader.PropertyToID("_BlendDistance");
  }

  protected void Dispose()
  {
    if (m_shapes != null)
    {
      m_shapes.Dispose();
      m_shapes = null;
    }
  }

  private void SetBuffer()
  {
    // validate SDF shapes buffer
    var sdfShapes = RayMarchedNode.GetShapes();
    int numShapes = sdfShapes.Count;
    if (m_shapes == null 
        || m_shapes.count != numShapes)
    {
      if (m_shapes != null)
      {
        m_shapes.Dispose();
        m_shapes = null;
      }

      m_shapes = new ComputeBuffer(Mathf.Max(1, numShapes), SdfNode.Stride);
    }

    // validate AABB tree buffer
    if (m_aabbTree == null 
        || m_aabbTree.count != RayMarchedNode.AabbTreeCapacity)
    {
      if (m_aabbTree != null)
      {
        m_aabbTree.Dispose();
        m_aabbTree = null;
      }

      m_aabbTree = new ComputeBuffer(RayMarchedNode.AabbTreeCapacity, AabbTree<RayMarchedNode>.NodePod.Stride);
    }

    // fill buffers
    m_shapes.SetData(sdfShapes);
    RayMarchedNode.FillAabbTree(m_aabbTree, AabbTree<RayMarchedNode>.FatBoundsRadius - 1 / blendDistanceInverse);
    
    material.SetFloat(m_const.BlendDistance, 1/blendDistanceInverse);
    material.SetBuffer(m_const.SdfNodes, m_shapes);
    material.SetBuffer( m_const.AabbTree, m_aabbTree);
    material.SetInt(m_const.NumSdfNodes, numShapes);
    material.SetInt(m_const.AabbTreeRoot, RayMarchedNode.AabbTreeRoot);
  }
  
  // private void OnDrawGizmos()
  // {
  //   #if UNITY_EDITOR
  //
  //   var camera = GetComponent<Camera>();
  //
  //   if (DrawBoundingVolumes)
  //   {
  //     RayMarchedNode.DrawBoundingVolumeHierarchyGizmos(IsolateBoundingVolumeDepth);
  //   }
  //
  //   if (TestBvhBoundsQuery)
  //   {
  //     Color prevColor = Handles.color;
  //
  //     Aabb queryBounds = 
  //       new Aabb
  //       (
  //         camera.transform.position - 0.5f * Vector3.one, 
  //         camera.transform.position + 0.5f * Vector3.one
  //       );
  //
  //     Handles.color = Color.yellow;
  //     Handles.DrawWireCube(queryBounds.Center, queryBounds.Extents);
  //
  //     Handles.color = new Color(1.0f, 1.0f, 0.0f, 0.5f);
  //     RayMarchedNode.Query
  //     (
  //       queryBounds, 
  //       (RayMarchedNode shape) =>
  //       {
  //         Handles.DrawWireCube(shape.Bounds.Center, shape.Bounds.Extents);
  //         return true;
  //       }
  //     );
  //
  //     Handles.color = prevColor;
  //   }
  //
  //   if (TestBvhRayCast)
  //   {
  //     Color prevColor = Handles.color;
  //
  //     Vector3 cameraFrom = camera.transform.position;
  //     Vector3 cameraTo = cameraFrom + 10.0f * camera.transform.forward;
  //
  //     Handles.color = Color.yellow;
  //     Handles.DrawLine(cameraFrom, cameraTo);
  //
  //     Handles.color = new Color(1.0f, 1.0f, 0.0f, 0.5f);
  //     RayMarchedNode.RayCast
  //     (
  //       cameraFrom, 
  //       cameraTo, 
  //       (Vector3 from, Vector3 to, RayMarchedNode shape) => 
  //       {
  //         Handles.DrawWireCube(shape.Bounds.Center, shape.Bounds.Extents);
  //         return 1.0f;
  //       }
  //     );
  //
  //     Handles.color = prevColor;
  //   }
  //
  //   #endif
  // }
  //
  private void OnDisable()
  {
    Dispose();
  }

  public void Update()
  {
    SetBuffer();
  }
  
  private void OnDrawGizmos()
  {
  #if UNITY_EDITOR

      // var camera = GetComponent<Camera>();

      if (DrawBoundingVolumes)
      {
        RayMarchedNode.DrawBoundingVolumeHierarchyGizmos(IsolateBoundingVolumeDepth);
      }

      // if (TestBvhBoundsQuery)
      // {
      //   Color prevColor = Handles.color;
      //
      //   Aabb queryBounds = 
      //     new Aabb
      //     (
      //       camera.transform.position - 0.5f * Vector3.one, 
      //       camera.transform.position + 0.5f * Vector3.one
      //     );
      //
      //   Handles.color = Color.yellow;
      //   Handles.DrawWireCube(queryBounds.Center, queryBounds.Extents);
      //
      //   Handles.color = new Color(1.0f, 1.0f, 0.0f, 0.5f);
      //   RayMarchedShape.Query
      //   (
      //     queryBounds, 
      //     (RayMarchedShape shape) =>
      //     {
      //       Handles.DrawWireCube(shape.Bounds.Center, shape.Bounds.Extents);
      //       return true;
      //     }
      //   );
      //
      //   Handles.color = prevColor;
      // }
      //
      // if (TestBvhRayCast)
      // {
      //   Color prevColor = Handles.color;
      //
      //   Vector3 cameraFrom = camera.transform.position;
      //   Vector3 cameraTo = cameraFrom + 10.0f * camera.transform.forward;
      //
      //   Handles.color = Color.yellow;
      //   Handles.DrawLine(cameraFrom, cameraTo);
      //
      //   Handles.color = new Color(1.0f, 1.0f, 0.0f, 0.5f);
      //   RayMarchedShape.RayCast
      //   (
      //     cameraFrom, 
      //     cameraTo, 
      //     (Vector3 from, Vector3 to, RayMarchedShape shape) => 
      //     {
      //       Handles.DrawWireCube(shape.Bounds.Center, shape.Bounds.Extents);
      //       return 1.0f;
      //     }
      //   );
      //
      //   Handles.color = prevColor;
      // }

  #endif
    }
}
