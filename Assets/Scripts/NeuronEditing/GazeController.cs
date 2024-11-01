using IntraXR;
using CommandStructure;
using Cysharp.Threading.Tasks.Triggers;
using Fusion;
using MathNet.Numerics;
using MixedReality.Toolkit;
using MixedReality.Toolkit.Input;
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.Subsystems;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

public class GazeController : Singleton<GazeController>
{
    public enum EyeInteractionType { None, Repair, EditThresh, DeleteNoise, LabelConfirm, LabelRefine }
    public enum GazeState { Saccade, Fixation}

    struct ScanPoint
    {
        public float timeStamp;
        public int length;
        public Vector3 position;

        public ScanPoint(float _timeStamp, int _length, Vector3 _position)
        {
            timeStamp = _timeStamp;
            length = _length;
            position = _position;
        }
    }
    public EyeInteractionType interactionType = EyeInteractionType.None;
    public GazeState gazeState = GazeState.Saccade;

    public float editTimeInterval = 2.0f;
    public float dampTime = 0.05f;
    public float adjustSpeed = 0.5f;

    private FuzzyGazeInteractor interactor;
    //private MRTKRayInteractor interactor;
    private GameObject eyePointer = null;

    private List<float> hitPoints = new();

    private float preTime;
    private float preComputeTime;

    private int[] scanPathLengthCount = new int[100];
    private Vector3 scanSum = Vector3.zero;

    private Vector3 preLocalHitPos = Vector3.zero;
    private Vector3 prePinchPos = Vector3.zero;
    private Vector3 direction = Vector3.zero;

    private Queue<ScanPoint> scanPoints = new();

    private Config config;
    private GameObject cube;

    private float traceTime = 0;
    
    private RenderTexture eyeHeatMap;

    private Vector3 velocity;

    private Material eyePointerMaterial;
    private HandsAggregatorSubsystem aggregator;
    private float depthOffset = 0;

    private int times = 0;

    private GameObject director;
    
    private bool jointIsValid;
    private bool handIsValid;
    private bool isReadyForInteraction;
    private bool preIsPinching = false;

    // Start is called before the first frame update
    void Start()
    {
        interactor = GameObject.Find("GazeInteractor").GetComponent<FuzzyGazeInteractor>();
        //interactor = GameObject.Find("Far Ray").GetComponent<MRTKRayInteractor>();
        config = GetComponent<Config>();
        cube = config.cube;

        Vector3Int dim = config.scaledDim;
        eyeHeatMap = new(dim.x, dim.y, 0, RenderTextureFormat.R8)
        {
            //graphicsFormat = GraphicsFormat.R8_UInt,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = dim.z,
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        eyePointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyePointer.name = "EyePointer";
        eyePointer.transform.localScale = Vector3.one.Divide(Config.Instance.originalDim).MaxComponent() * 2 * BoardManager.Instance.gameObject.transform.localScale;
        eyePointer.GetComponent<SphereCollider>().enabled = false;
        eyePointer.SetActive(false);
        eyePointerMaterial = eyePointer.GetComponent<MeshRenderer>().material;

        aggregator = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();

        
    }


    // Update is called once per frame
    void Update()
    {
        cube = config.cube;
        var result = interactor.PreciseHitResult;
        //RaycastHit result;
        //interactor.TryGetCurrent3DRaycastHit(out result);
        Vector3 boardScale = BoardManager.Instance.gameObject.transform.localScale;
        eyePointer.transform.localScale = Vector3.one.Divide(Config.Instance.originalDim).MaxComponent() * boardScale;
        
        
        traceTime += Time.deltaTime;
        jointIsValid = aggregator.TryGetJoint(TrackedHandJoint.IndexTip, XRNode.RightHand, out HandJointPose jointPose);
        handIsValid = aggregator.TryGetPinchProgress(XRNode.RightHand, out bool isReadyToPinch, out bool isPinching, out float pinchAmount);
        isReadyForInteraction = jointIsValid && handIsValid && isPinching && pinchAmount > 0.95 && interactionType != EyeInteractionType.None && !BoardManager.Instance.IsNearInteracting();
        if(preIsPinching == false && isReadyForInteraction)
        {
            traceTime = 0;
        }
        
        if (result.IsRaycast && result.raycastHit.collider.CompareTag("Board"))
        //if (result.collider == null) return;
        //if (result.collider.CompareTag("Board"))
        {
            var gazeOrigin = interactor.rayOriginTransform.position;
            var hitPos = result.raycastHit.point;
            //var hitPos = result.point;
            //director.SetActive(true);
            //director.transform.position = (eyePointer.transform.position + gazeOrigin)/2;
            //director.transform.up = gazeOrigin - eyePointer.transform.position;
            //director.transform.localScale = new Vector3(0.001f, Vector3.Distance(gazeOrigin, eyePointer.transform.position)/2, 0.001f);

            //gesture
            if (isReadyForInteraction)
            {
                HandlePinching(gazeOrigin, jointPose.Position, hitPos);
            }
            else if (preIsPinching)
            {
                HandleEndOfPinching();
            }
            //gaze 
            else 
            {
                //director.SetActive(false); 
                HandleGazeInteraction(gazeOrigin,hitPos);
            }
        }
        else
        {
            //eyePointer.SetActive(false);
        }

    }

    private void   HandlePinching(Vector3 rayOrigin, Vector3 curPinchPos, Vector3 hitPos)
    {
        eyePointer.GetComponent<MeshRenderer>().material.color = Color.blue;
        if (preIsPinching)
        {
            switch (interactionType)
            {
                case EyeInteractionType.LabelRefine:
                case EyeInteractionType.Repair:
                case EyeInteractionType.DeleteNoise:
                    Vector3 originPos = interactor.rayOriginTransform.position;
                    Quaternion rotationChange = Quaternion.FromToRotation((prePinchPos - rayOrigin).normalized, (curPinchPos - rayOrigin).normalized);
                    Quaternion interpolatedRotation = Quaternion.Slerp(Quaternion.identity, rotationChange, adjustSpeed);
                    direction = interpolatedRotation * direction;
                    depthOffset += (curPinchPos - rayOrigin).magnitude - (prePinchPos - rayOrigin).magnitude;
                    depthOffset = Mathf.Max(depthOffset, 0);
                    if (Physics.Raycast(rayOrigin, direction, out RaycastHit hitInfo))
                    {
                        //if (!SetLabel(cube.transform.InverseTransformPoint(hitInfo.point + depthOffset * direction), cube.transform.InverseTransformPoint(originPos)))
                        if (!SetLabel(cube.transform.InverseTransformPoint(hitInfo.point + depthOffset * direction), cube.transform.InverseTransformPoint(originPos)))
                        {
                            eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, hitInfo.point, ref velocity, dampTime);
                        }
                    }
                    break;
                case EyeInteractionType.EditThresh:
                    var localGazeOrigin = cube.transform.InverseTransformPoint(rayOrigin);
                    var localHitPos = cube.transform.InverseTransformPoint(hitPos);
                    preLocalHitPos = preLocalHitPos == Vector3.zero ? localHitPos : preLocalHitPos;

                    direction = hitPos - rayOrigin;
                    
                    if(!isReadyForInteraction) break;
                    eyePointer.SetActive(true);
                    eyePointer.GetComponent<MeshRenderer>().material.color = Color.white;
                    eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, hitPos, ref velocity, dampTime);
                    
                    var localDirection = (localHitPos - localGazeOrigin).normalized;
                    var hitCoordinate = (localHitPos + 0.5f * Vector3.one).Mul(config.scaledDim / config.thresholdBlockSize);
                    
                    config.tracer.AdjustThreshold(hitCoordinate, localDirection);

                    if (traceTime > editTimeInterval)
                    {
                        //config.tracer.TraceTrunk(1);
                        // config.tracer.dontCoroutine(1);
                        config.tracer.TraceTrunk(2);
                        traceTime = 0;
                    }
                    
                    break;    
            }
        }
        preIsPinching = true;
        prePinchPos = curPinchPos;
    }

    private void HandleEndOfPinching()
    {
        switch (interactionType)
        {
            case EyeInteractionType.Repair:
                config.invoker.Execute(new AdjustCommand(config.tracer, config.curIndex));
                break;
            case EyeInteractionType.LabelConfirm:
            case EyeInteractionType.LabelRefine:
                BoardManager.Instance.CreatePoint(Config.Instance.curIndex, Config.Instance.scaledDim, Color.green, 1.6f);
                SwcLoader.Instance.AddLabel(Utils.IndexToCoordinate((uint)Config.Instance.curIndex, Config.Instance.scaledDim).Divide(Config.Instance.scaledDim).Multiply(Config.Instance.originalDim));
                break;
            case EyeInteractionType.DeleteNoise:
                Config.Instance.selectedIndex = Config.Instance.curIndex;
                Config.Instance.tracer.HighlightNoise();
                break;
        }
        preIsPinching = false;
        depthOffset = 0;
    }

    private void HandleGazeInteraction(Vector3 gazeOrigin,Vector3 hitPos)
    {
        var localGazeOrigin = cube.transform.InverseTransformPoint(gazeOrigin);
        var localHitPos = cube.transform.InverseTransformPoint(hitPos);
        preLocalHitPos = preLocalHitPos == Vector3.zero ? localHitPos : preLocalHitPos;
 
        direction = hitPos - gazeOrigin;
        switch (interactionType)
        {
            case EyeInteractionType.None:
                {
                    eyePointer.SetActive(false);
                    break;
                }
            case EyeInteractionType.Repair:
            case EyeInteractionType.DeleteNoise:
            {
                    eyePointer.SetActive(true);
                    var timeStamp = Time.realtimeSinceStartup;
                    int curIndex = GetIntersection(Config.Instance.ScaledVolume, localHitPos, localGazeOrigin);
                    if (curIndex != -1)
                    {
                        eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, IndexToPos(curIndex), ref velocity, dampTime);
                    }
                    else
                    {
                        eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, hitPos, ref velocity, dampTime);
                    }

                    if ((timeStamp - preTime) > 1.0f / 60)
                    {
#if EYE_HEAT_MAP
                                for eye heat map
                                if (curIndex != -1)
                                {
                                    int i = curIndex % config._scaledDim.x;
                                    int j = (curIndex / config._scaledDim.x) % config._scaledDim.y;
                                    int k = (curIndex / (config._scaledDim.x * config._scaledDim.y) % config._scaledDim.z);
                                    addHitPoint(new Vector3(i, j, k));
                                }
#endif
                        AddScanPoint(localHitPos, timeStamp);
                        RemoveExpiredScanPoint(timeStamp);
                        preTime = timeStamp;
                        preLocalHitPos = localHitPos;

                        double gamma = FitDistribution();
                        if (gamma > 0.85)
                        {
                            gazeState = GazeState.Fixation;
                            Vector3 scanCenter = scanSum / scanPoints.Count;
                            SetSeed(scanCenter, localGazeOrigin);
                        }
                        else
                        {
                            gazeState = GazeState.Saccade;
                            eyePointer.GetComponent<MeshRenderer>().material.color = Color.white;
                        }
                    }
                    break;
                }
            case EyeInteractionType.LabelConfirm:
            case EyeInteractionType.LabelRefine:
                {
                    eyePointer.SetActive(true);
                    var timeStamp = Time.realtimeSinceStartup;
                    int curIndex = GetIntersection(Config.Instance.ScaledVolume, localHitPos, localGazeOrigin);
                    if (curIndex != -1)
                    {
                        eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, IndexToPos(curIndex), ref velocity, dampTime);
                    }
                    else
                    {
                        eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, hitPos, ref velocity, dampTime);
                    }

                    if ((timeStamp - preTime) > 0.5f / 60)
                    {
                        AddScanPoint(localHitPos, timeStamp);
                        RemoveExpiredScanPoint(timeStamp);
                        preTime = timeStamp;
                        preLocalHitPos = localHitPos;

                        if(timeStamp-preComputeTime>0.667)
                        {
                            double gamma = FitDistribution();
                            if (gamma > 0.85)
                            {
                                gazeState = GazeState.Fixation;
                                Vector3 scanCenter = scanSum / scanPoints.Count;
                                SetLabel(scanCenter, localGazeOrigin);
                            }
                            else
                            {
                                gazeState = GazeState.Saccade;
                                eyePointer.GetComponent<MeshRenderer>().material.color = Color.white;
                            }
                            preComputeTime = timeStamp;
                        }
                    }
                    break;
                }
            default:
            {
                break;
            }
        }
    }

    private void SetSeed(Vector3 scanCenter, Vector3 gazeOrigin)
    {
        int max_index = GetIntersection(Config.Instance.ScaledVolume, scanCenter, gazeOrigin);
        //Debug.Log("max_intesity" + max_intensity);
        var position = Utils.IndexToPosition((uint)max_index, Config.Instance.scaledDim);

        if (max_index>=0)
        {
            //SetPointer((int)max_index, false);
            config.curIndex = (uint)max_index;
            position -= new Vector3(.5f, .5f, .5f);
            position = cube.transform.TransformPoint(position);

            if (config.tracer.Contained((uint)max_index))
            {
                eyePointerMaterial.color = Color.blue;
            }
            else
            {
                eyePointerMaterial.color = Color.red;
            }

            eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, position, ref velocity, dampTime);

            if (!config.tracer.Contained((uint)max_index))
            {
                //config.invoker.Execute(new AdjustCommand(config.tracer, max_index));
            }
        }
        else
        {
            eyePointerMaterial.color = Color.white;
        }
    }

    private bool SetLabel(Vector3 scanCenter, Vector3 gazeOrigin)
    {
        int maxIndex = GetIntersection(Config.Instance.ScaledVolume, scanCenter, gazeOrigin);
        //Debug.Log("max_intesity" + max_intensity);
        var position = Utils.IndexToPosition((uint)maxIndex, Config.Instance.scaledDim);

        if (maxIndex >= 0)
        {
            config.curIndex = (uint)maxIndex;
            position -= 0.5f * Vector3.one;
            position = cube.transform.TransformPoint(position);
            eyePointerMaterial.color = Color.red;
            eyePointer.transform.position = Vector3.SmoothDamp(eyePointer.transform.position, position, ref velocity, dampTime);

            //BoardManager.Instance.CreatePoint(Config.Instance.curIndex, Config.Instance.scaledDim, Color.green);
            //SwcLoader.Instance.AddLabel(Utils.IndexToCoordinate((uint)Config.Instance.curIndex, Config.Instance.scaledDim));

            return true;
        }
        else
        {
            eyePointer.GetComponent<MeshRenderer>().material.color = Color.white;
            return false;
        }
    }


    private float Radius(uint index, byte[] volumeData)
    {
        int sz0 = config.scaledDim.x;
        int sz1 = config.scaledDim.y;
        int sz2 = config.scaledDim.z;

        int x = (int)(index % sz0);
        int y = (int)((index / sz0) % sz1);
        int z = (int)((index / (sz0 * sz1) % sz2));

        double max_r = sz0 / 2;
        max_r = Math.Max(max_r, sz1 / 2);
        max_r = Math.Max(max_r, sz2 / 2);

        double tol_num = 0, bkg_num = 0;
        float ir;
        for (ir = 1; ir < max_r; ir++)
        {
            tol_num = 0;
            bkg_num = 0;
            double dz, dy, dx;
            for (dz = -ir; dz <= ir; dz++)
            {
                for (dy = -ir; dy <= ir; dy++)
                {
                    for (dx = -ir; dx <= ir; dx++)
                    {
                        double r = Math.Sqrt(dz * dz + dy * dy + dx * dx);
                        if (r > ir - 1 && r <= ir)
                        {
                            tol_num++;
                            long i = (long)(x + dx);
                            if (i < 0 || i >= sz0) return ir;
                            long j = (long)(y + dy);
                            if (j < 0 || j >= sz1) return ir;
                            long k = (long)(z + dz);
                            if (k < 0 || k >= sz2) return ir;
                            if (volumeData[k * sz0 * sz1 + j * sz0 + i] <= config.ViewThresh)
                            {
                                bkg_num++;
                                if ((bkg_num / tol_num > 0.05)) return ir;
                            }
                        }
                    }
                }
            }
        }
        return ir;
    }

    /// Return the intersection with eye sight and image foreground
    /// </summary>
    /// <param name="texture">The 3D texture to sample from</param>
    /// <param name="targetPos">The intersection with eye sight and the boundary of volume</param>
    /// <param name="gazeOrigin">The position of eye (camera)</param>
    /// <returns></returns>
    private int GetIntersection(Texture3D texture,Vector3 targetPos, Vector3 gazeOrigin)
    {
        int bkgThresh = config.ViewThresh;
        int sz0 = texture.width, sz1 = texture.height, sz2 = texture.depth;
        Vector3 direction = (targetPos - gazeOrigin).normalized;
        byte[] volumeData = config.VolumeData;
        Vector3 minBounds = -0.5f * Vector3.one;
        Vector3 maxBounds = 0.5f * Vector3.one;

        Vector3 tmin = (minBounds - targetPos).Divide(direction);  // Element-wise division
        Vector3 tmax = (maxBounds - targetPos).Divide(direction);
        (tmin, tmax) = (Vector3.Min(tmin, tmax), Vector3.Max(tmin, tmax));
        float max_length = tmax.MaxComponent() - tmin.MinComponent();

        float dt = 0.001f;
        Vector3 pos = targetPos + Vector3.one * 0.5f;
        float distance = 0;
        uint maxIndex = 0;
        byte maxIntensity = 0;

        int offset = 1;
        for (int t = 0; t < 100000; t++)
        {
            int x = (int)(pos.x * sz0);
            int y = (int)(pos.y * sz1);
            int z = (int)(pos.z * sz2);

            for (int offsetX = -offset; offsetX <= offset; offsetX++)
            {
                for (int offsetY = -offset; offsetY <= offset; offsetY++)
                {
                    for (int offsetZ = -offset; offsetZ <= offset; offsetZ++)
                    {
                        int w = x + offsetX;
                        int h = y + offsetY;
                        int d = z + offsetZ;
                        if (w >= 0 && w < sz0 && h >= 0 && h < sz1 && d >= 0 && d < sz2)
                        {
                            uint index = (uint)(w + (h * sz0) + (d * sz0 * sz1));
                            byte intensity = volumeData[index];
                            if (intensity > maxIntensity)
                            {
                                maxIntensity = intensity;
                                maxIndex = index;
                            }
                        }
                    }
                }
            }
                pos += direction * dt;
                distance += dt;
                if (distance > max_length) break;
            }

        return maxIntensity >= bkgThresh ? (int)maxIndex : -1;
    }

    private void IterateNeighborhood(Vector3 position, int sz0, int sz1, int sz2, byte[] volumeData, int offset, ref byte maxIntensity, ref uint maxIndex)
    {

    }

    private double FitDistribution()
    {
        double[] pathLength = new double[100];
        double[] frequency = new double[100];
        for (int i = 0; i < 100; i++)
        {
            frequency[i] = (double)scanPathLengthCount[i] / scanPoints.Count;
            pathLength[i] = 1 / ((double)(i + 1.0d) * (double)(i + 1.0d));
            //Debug.Log("x:" + pathLength[i] + " y:" + frequency[i]);
        }

        var s = Fit.LineThroughOrigin(pathLength, frequency);
        //Debug.Log("fitting A:" + s);
        return s;
    }

    private void AddScanPoint(Vector3 localHitPosition, float timeStamp)
    {
        //scanPoints[scanCount++] = localHitPosition;
        scanSum += localHitPosition;
        int length = (int)(Vector3.Distance(localHitPosition, preLocalHitPos) * 100);
        length = Math.Min(99, length);
        scanPathLengthCount[length]++;

        scanPoints.Enqueue(new ScanPoint(timeStamp, length, localHitPosition));
    }

    private void RemoveExpiredScanPoint(float curTimeStamp)
    {
        while(curTimeStamp - scanPoints.Peek().timeStamp> 0.667f)
        {
            var peek = scanPoints.Peek();
            scanPathLengthCount[peek.length]--;
            scanSum -= peek.position;
            scanPoints.Dequeue();
        }
    }

    public void AddHitPoint(Vector3 pos)
    {
        hitPoints.Add(pos.x);
        hitPoints.Add(pos.y);
        hitPoints.Add(pos.z);
        //Debug.Log("add hit:" + pos.ToString("f4"));

        Vector3Int dim = config.scaledDim;
        ComputeShader computeShader = Resources.Load("ComputeShaders/ErrorHeatMap") as ComputeShader;
        int kernel = computeShader.FindKernel("CSMain");


        string assetName = config.imageName + "_eyeData";
        ComputeBuffer hitsBuffer = new(hitPoints.Count, sizeof(float), ComputeBufferType.Default);
        hitsBuffer.SetData(hitPoints.ToArray());

        computeShader.SetTexture(kernel, "Result", eyeHeatMap);
        computeShader.SetBuffer(kernel, "_Hits", hitsBuffer);
        computeShader.SetInts("dim", new int[] { dim.x, dim.y, dim.z });
        computeShader.SetInt("_HitCount", hitPoints.Count / 3); 

        computeShader.Dispatch(kernel, Mathf.CeilToInt(dim.x / 8), Mathf.CeilToInt(dim.y / 8), Mathf.CeilToInt(dim.z / 8));
        MeshRenderer meshRenderer = GameObject.Find("EyeHeatMap").GetComponent<MeshRenderer>();
        Material material = meshRenderer.material;
        material.SetTexture("_ErrorWeights", eyeHeatMap);

        //AssetDatabase.DeleteAsset($"Assets/Textures/HeatMap/{assetName}.Asset");
        //AssetDatabase.CreateAsset(result, $"Assets/Textures/HeatMap/{assetName}.Asset");
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh
        hitsBuffer.Release();
        //result.Release();
    }

    Vector3 IndexToPos(int index)
    {
        int i = index % config.scaledDim.x;
        int j = (index / config.scaledDim.x) % config.scaledDim.y;
        int k = (index / (config.scaledDim.x * config.scaledDim.y) % config.scaledDim.z);
        Vector3 pos = new()
        {
            x = i / (float)config.scaledDim.x,
            y = j / (float)config.scaledDim.y,
            z = k / (float)config.scaledDim.z
        };
        pos -= new Vector3(.5f, .5f, .5f);
        pos = cube.transform.TransformPoint(pos);
        return pos;
    }
}
