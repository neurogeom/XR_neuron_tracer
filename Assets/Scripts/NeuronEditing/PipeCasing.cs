using CommandStructure;
using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using UnityEngine.XR;

public class PipeCasing : MonoBehaviour
{
    public List<Marker> markers;
    public Dictionary<Marker, GameObject> spheres;
    [SerializeField] private int beginning;
    private Transform cube;
    [SerializeField] int len = 0;
    public List<uint> targets;
    private Config config;
    public float radiusBias = 2;
    public float pipeExtension = 1;
    public Vector3Int dim;
    HandsAggregatorSubsystem aggregator;
    // Start is called before the first frame update
    void Start()
    {
        aggregator = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();
    }

    // Update is called once per frame
    void Update()
    {
        bool jointIsValid = aggregator.TryGetJoint(TrackedHandJoint.IndexTip, XRNode.RightHand, out HandJointPose jointPose);
        bool handIsValid = aggregator.TryGetPinchProgress(XRNode.RightHand, out bool isReadyToPinch, out bool isPinching, out float pinchAmount);
        if (jointIsValid && handIsValid && isPinching && pinchAmount >= 0.95)
        {

            int center = len / 2 + beginning;
            Marker centerMarker = markers[center];
            Vector3 centerPos = cube.TransformPoint(centerMarker.position.Div(dim) - 0.5f * Vector3.one);
            Debug.Log($"centerIndex: {center} centerPos:{centerPos}");
            if (center>0)
            {
                Marker preMarker = markers[center - 1];
                Vector3 prePos = cube.TransformPoint(preMarker.position.Div(dim) - 0.5f * Vector3.one);
                // Debug.Log($"preIndex: {center-1} prePos:{prePos}");
                if (Vector3.Distance(prePos, jointPose.Position) < Vector3.Distance(centerPos, jointPose.Position)) MoveBack();
            }
            if(center< beginning + len)
            {
                Marker nextMarker = markers[center + 1];
                Vector3 nextPos = cube.TransformPoint(nextMarker.position.Div(dim) - 0.5f * Vector3.one);
                if (Vector3.Distance(nextPos, jointPose.Position) < Vector3.Distance(centerPos, jointPose.Position))
                {
                    MoveOn();
                }
                // Debug.Log($"nextIndex: {center+1} nextPos:{nextPos}");
            }
        }

    }

    [InspectorButton]
    public void MoveOn()
    {
        beginning = Math.Min(markers.Count, beginning + 1);
        Activate();
    }

    [InspectorButton]
    public void MoveBack()
    {
        beginning = Math.Max(0, beginning - 1);
        Activate();
    }

    [InspectorButton]
    public void AddLength()
    {
        len = Math.Min(len + 2, markers.Count - beginning -1);
        Activate();
    }

    [InspectorButton]
    public void DecLength()
    {
        len = Math.Max(0, len - 1);
        Activate();
    }

    private void AddRadiusBias()
    {
        radiusBias++;
        Activate();
    }

    private void DecRadiusBias()
    {
        radiusBias--;
        Activate();
    }    
    private void AddPipeExtension()
    {
        pipeExtension++;
        Activate();
    }

    private void DecPipeExtension()
    {
        pipeExtension--;
        Activate();
    }

    [InspectorButton]
    public void Trace()
    {
        config.invoker.Execute(new MaskCommand(config.tracer, targets));
        ClearPipes();
    }

    public void Activate()
    {
        ClearPipes();
        for (int i = 0; i < len && i + beginning < markers.Count; i++)
        {
            Marker marker = markers[i + beginning];
            var cylinder = Primitive.CreateCylinder(marker, dim, cube, radiusBias);
            cylinder.GetComponent<MeshRenderer>().material.color = Color.white;
            cylinder.transform.SetParent(GameObject.Find("Pipe").transform, true);
            targets.AddRange(GetTargets(marker, radiusBias , pipeExtension));

            var sphere = Primitive.CreateSphere(marker, dim, cube, radiusBias);
            sphere.GetComponent<MeshRenderer>().material.color = Color.white;
            sphere.transform.SetParent(GameObject.Find("Pipe").transform, true);

            //if (i == len - 1 || i + beginning == markers.Count - 1)
            //{
            //    cylinder.AddComponent<BoxCollider>();
            //    var si = cylinder.AddComponent<StatefulInteractable>();
            //    si.IsPokeSelected.OnEntered.AddListener((args) => {
            //        HeadInteract();
            //    });
            //}
            //else if (i == 0)
            //{
            //    cylinder.AddComponent<BoxCollider>();
            //    var si = cylinder.AddComponent<StatefulInteractable>();
            //    si.IsPokeSelected.OnEntered.AddListener((args) => {
            //        TailInteract();
            //    });
            //}
        }
    }

    private void HeadInteract()
    {
        bool jointIsValid = aggregator.TryGetJoint(TrackedHandJoint.IndexTip, XRNode.RightHand, out HandJointPose jointPose);
        bool handIsValid = aggregator.TryGetPinchProgress(XRNode.RightHand, out bool isReadyToPinch, out bool isPinching, out float pinchAmount);
        MoveOn();
    }

    private void TailInteract()
    {
        bool jointIsValid = aggregator.TryGetJoint(TrackedHandJoint.IndexTip, XRNode.RightHand, out HandJointPose jointPose);
        bool handIsValid = aggregator.TryGetPinchProgress(XRNode.RightHand, out bool isReadyToPinch, out bool isPinching, out float pinchAmount);
        if (jointIsValid && handIsValid && isPinching && pinchAmount >= 0.95)
        {
            MoveBack();
        }
        else
        {
            DecLength();
        }
    }

    public void ClearPipes()
    {
        for (int i = 0; i < GameObject.Find("Pipe").transform.childCount; i++)
        {
            GameObject.Destroy(GameObject.Find("Pipe").transform.GetChild(i).gameObject);
        }
        targets.Clear();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="marker"></param>
    /// <param name="radiusBias">Modified value of pipe radius</param>
    /// <param name="pipeExtension">Distance extended along the direction</param>
    /// <returns></returns>
    private List<uint> GetTargets(Marker marker, float radiusBias, float pipeExtension)
    {
        var dim = config.scaledDim;
        int[] dimsArray = new int[3] { dim.x, dim.y, dim.z };
        ComputeShader computeShader = Resources.Load("ComputeShaders/Utility") as ComputeShader;
        var sourceSet = new ComputeBuffer(10000, sizeof(uint), ComputeBufferType.Append);
        sourceSet.SetCounterValue(0);
        var direction = (marker.parent.position - marker.position).normalized;

        int kernel = computeShader.FindKernel("GetPipeCasingTargets");
        computeShader.SetInts("dims", dimsArray);
        computeShader.SetFloat("pipeRadius", marker.radius);
        computeShader.SetFloat("radiusBias", radiusBias);
        computeShader.SetVector("start", marker.position - pipeExtension * direction);
        computeShader.SetVector("end", marker.parent.position + pipeExtension* direction );
        computeShader.SetBuffer(kernel, "sourceSet", sourceSet);
        computeShader.Dispatch(kernel, (dim.x / 8), (dim.y / 8), (dim.z / 8));
        uint sourceCount = GetAppendBufferSize(sourceSet);
        uint[] sourceData = new uint[sourceCount];
        sourceSet.GetData(sourceData);
        sourceSet.Release();
        return sourceData.ToList();
    }
    uint GetAppendBufferSize(ComputeBuffer appendBuffer)
    {
        uint[] countBufferData = new uint[1];
        var countBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        ComputeBuffer.CopyCount(appendBuffer, countBuffer, 0);
        countBuffer.GetData(countBufferData);
        uint count = countBufferData[0];
        countBuffer.Release();

        return count;
    }

    internal void Initial(Marker marker, Vector3Int dim, Transform cubeTransform)
    {
        config = GameObject.FindGameObjectWithTag("Config").GetComponent<Config>();
        markers = new();
        targets = new();
        markers.Add(marker);
        beginning = 0;
        this.dim = dim;
        this.cube = cubeTransform;
        var parent = marker.parent;
        while (parent != null)
        {
            markers.Add(parent);
            parent = parent.parent;
        }
        //Activate();
    }
}
