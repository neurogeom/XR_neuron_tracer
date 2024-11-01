using IntraXR;
using MixedReality.Toolkit.SpatialManipulation;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Timeline;
using static UnityEngine.GraphicsBuffer;

public class BoardManager : Singleton<BoardManager>
{
    Transform cube;
    // Start is called before the first frame update
    void Start()
    {
        cube = gameObject.GetNamedChild("Cube").transform;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ClearTargets()
    {
        Transform targets = gameObject.GetNamedChild("Targets").transform;
        for(int i = 0; i < targets.childCount; i++) 
        {
            Destroy(targets.GetChild(i).gameObject);
        }
    }

    public void ClearReconstruction()
    {
        Transform reconstruction = gameObject.GetNamedChild("Reconstruction").transform;
        for (int i = 0; i < reconstruction.childCount; i++)
        {
            Destroy(reconstruction.GetChild(i).gameObject);
        }
    }

    public GameObject CreatePoint(Vector3 cubicCoordinate, Vector3 dimension, Color color, float scale = 1.5f)
    {
        Transform targets = gameObject.GetNamedChild("Targets").transform;
        var cubicPosition = cubicCoordinate.Divide(dimension)- 0.5f * Vector3.one;
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        sphere.transform.localScale = Vector3.one.Divide(dimension).MaxComponent()* scale * transform.localScale;
        sphere.transform.position = cube.TransformPoint(cubicPosition);
        sphere.transform.SetParent(targets,true);
        sphere.GetComponent<MeshRenderer>().material.color = color;
        return sphere;
    }

    public void CreatePoint(uint index, Vector3Int dimension, Color color, float scale = 1.5f)
    {
        Vector3 coord = Utils.IndexToCoordinate(index, dimension);
        CreatePoint(coord, dimension, color, scale);
    }

    public bool IsNearInteracting()
    {
        ObjectManipulator OM = GetComponent<ObjectManipulator>();
        return OM.IsGrabHovered || OM.IsGrabSelected;
    }
}
