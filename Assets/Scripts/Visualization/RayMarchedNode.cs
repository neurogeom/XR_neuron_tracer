using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class RayMarchedNode : MonoBehaviour
{
    public float radius;
    public int index;
    public RayMarchedNode parent; 
    public int type;
    public int Type
    {
        get => type;
        set
        {
            if (value == 0) value += 1;
            type = type < 0 ? -Mathf.Abs(value): value;
        }
    }

    private static AabbTree<RayMarchedNode> s_tree = new AabbTree<RayMarchedNode>();
    private static List<RayMarchedNode> s_shapeComponents = new List<RayMarchedNode>();
    private static List<SdfNode> s_sdfShapes = new List<SdfNode>();

    public static List<SdfNode> GetShapes()
    {
        if (s_sdfShapes.Capacity < s_shapeComponents.Count)
        {
            s_sdfShapes.Capacity = s_shapeComponents.Count;
        }

        s_sdfShapes.Clear();

        foreach (var s in s_shapeComponents)
        {
            var sdfShape = s.Node;
            s_sdfShapes.Add(sdfShape);
        }

        return s_sdfShapes;
    }

    public static void Query(Aabb bounds, AabbTree<RayMarchedNode>.QueryCallbcak callback)
    {
        s_tree.Query(bounds, callback);
    }

    public static void RayCast(Vector3 from, Vector3 to, AabbTree<RayMarchedNode>.RayCastCallback callback)
    {
        s_tree.RayCast(from, to, callback);
    }

    public static int AabbTreeCapacity
    {
        get { return s_tree.Capacity; }
    }

    public static int AabbTreeRoot
    {
        get { return s_tree.Root; }
    }

    public static int FillAabbTree(ComputeBuffer buffer, float aabbTightenRadius = 0.0f)
    {
        SyncBounds();

        int root = s_tree.Fill(buffer, aabbTightenRadius);
        return root;
    }

    public static void SyncBounds()
    {
        
        foreach (var s in s_shapeComponents)
        {
            s_tree.UpdateProxy(s.m_iProxy, s.Bounds);
        }
    }

    public static void DrawBoundingVolumeHierarchyGizmos(int isolateDepth = -1)
    {
        SyncBounds();
        s_tree.DrwaGizmos(isolateDepth);
    }

    public enum OperatorEnum
    {
        Union,
        Subtraction,
        Intersection
    }

    public OperatorEnum Operator = OperatorEnum.Union;
    private int m_shapeIndex = -1;
    private int m_iProxy = AabbTree<RayMarchedNode>.Null;

    public int ShapeIndex
    {
        get { return m_shapeIndex; }
    }

    private void OnEnable()
    {
        m_shapeIndex = s_shapeComponents.Count;
        s_shapeComponents.Add(this);

        CreateProxy();
    }

    private void OnDisable()
    {
        s_shapeComponents[m_shapeIndex] = s_shapeComponents[s_shapeComponents.Count - 1];
        s_shapeComponents[m_shapeIndex].m_shapeIndex = m_shapeIndex;
        s_shapeComponents.RemoveAt(s_shapeComponents.Count - 1);
        m_shapeIndex = -1;

        s_tree.DestroyProxy(m_iProxy);
        m_iProxy = AabbTree<RayMarchedNode>.Null;
    }

    public void CreateProxy()
    {
        if (parent != null)
        {
            m_iProxy = s_tree.CreateProxy(Bounds, this);
        }
    }

    protected virtual void OnValidate()
    {
    }

    private SdfNode Node => new(transform.position, radius * transform.lossyScale.x, parent.index, type);

    public Aabb Bounds
    {
        get
        {
            Vector3 aMin = transform.position - Vector3.one * (radius * transform.lossyScale.x);
            Vector3 aMax = transform.position + Vector3.one * (radius * transform.lossyScale.x);
            Vector3 bMin = parent.transform.position - Vector3.one * (parent.radius * parent.transform.lossyScale.x);
            Vector3 bMax = parent.transform.position + Vector3.one * (parent.radius * parent.transform.lossyScale.x);
            Vector3 cMin = parent.parent.transform.position - Vector3.one * (parent.parent.radius * parent.parent.transform.lossyScale.x);
            Vector3 cMax = parent.parent.transform.position + Vector3.one * (parent.parent.radius * parent.parent.transform.lossyScale.x);
            Vector3 min = Vector3.Min(aMin,Vector3.Min(bMin,cMin));
            Vector3 max = Vector3.Max(aMax, Vector3.Max(bMax,cMax));
            return new Aabb(min, max);
        }
    }
}