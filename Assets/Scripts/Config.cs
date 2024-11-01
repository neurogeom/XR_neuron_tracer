using CommandStructure;
using Fusion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Fusion.Allocator;
using static Microsoft.MixedReality.GraphicsTools.MeshInstancer;

public class Config : Singleton<Config>
{
    public ConfigScriptableOject configuration;
    public string path;
    public string savePath;
    public bool needImport;
    public bool scale = true;
    public bool gaussianSmoothing = true;
    public bool forceRootCenter = false;
    public string imageName;
    [SerializeField] private Texture3D scaledVolume;
    [SerializeField] private Texture3D origin;
    [SerializeField] private RenderTexture _filtered;
    public Vector3Int scaledDim;
    public Vector3Int originalDim;
    public Texture3D _occupancy;
    [SerializeField] private int bkgThresh;
    public int _blockSize;
    public GameObject seed;
    public GameObject cube;
    public GameObject paintingBoard;
    public Vector3Int _rootPos;
    public float somaRadius = -1;
    [SerializeField] private int viewThresh;
    public uint curIndex = 0;
    public int thresholdBlockSize = 2;
    [SerializeField] private ShaderType _vrShaderType = ShaderType.Base;
    private PostProcessVolume postProcessVolume;
    public bool useBatch = true;
    public bool useKeyBoard = true;
    public int customThresh = 30;
    public NetworkRunner runner;
    public BaseVolumeRendering volumeRendering;

    private byte[] volumeData;

    public Tracer tracer;
    public GestureController gestureController;
    public GazeController gazeController;
    public CMDInvoker invoker;

    public Material originMaterial;
    public Material bkgThresholdMaterial;
    public Material fixedBkgThresholdMaterial;

    public int thresholdOffset = 0;
    public float viewRadius = 5.0f;

    public bool volumeRenderingWithChebyshev = false;

    public Texture3D occupancyMap;
    public Texture3D distanceMap;

    public float radiusScale=1;
    public uint selectedIndex = 0;
    public bool isIsolating = false;
    public int resampleFactor = 5;

    public bool needFilter = true;


    public enum ShaderType {
        Base, FlexibleThreshold, FixedThreshold, BaseAccelerated
    }

    public int BkgThresh
    {
        get { return bkgThresh; }
        set
        {
            if (value < 0 || value > 255)
            {
                throw new ArgumentException("The background threshold must be set correctly at 0 to 255");
            }
            bkgThresh = value;
            viewThresh = value;
        }
    }

    public int ViewThresh
    {

        get { return viewThresh; }
        set
        {
            if (value < 0 || value > 255)
            {
                throw new ArgumentException("The backgroundview threshold must be set correctly at 0 to 255");
            }
            viewThresh = value;

        }
    }

    public Texture3D ScaledVolume
    {
        get => scaledVolume;
        set => scaledVolume = value;
    }

    public Texture3D Origin
    {
        get => origin;
        set => origin = value;
    }

    public byte[] VolumeData
    {
        get => volumeData;
    }

    public ShaderType VRShaderType
    {
        get => _vrShaderType;
        set {
            _vrShaderType = value;
        }
    }

    override public void Awake()
    {
        base.Awake();

        path = configuration.path;
        savePath = configuration.savePath;
        needImport = configuration.needImport;
        scale = configuration.scale;
        gaussianSmoothing = configuration.gaussianSmoothing;
        forceRootCenter = configuration.forceRootCenter;
        origin = configuration.volume;
        scaledDim = configuration.scaledDim;
        somaRadius = configuration.somaRadius;
        bkgThresh = configuration.bkgThresh;
        viewThresh = configuration.viewThresh;
        useBatch = configuration.useBatch;
        useKeyBoard = configuration.useKeyBoard;
        customThresh = configuration.customThresh;
        fixedBkgThresholdMaterial = configuration.fixedBkgThresholdMaterial;
        thresholdOffset = configuration.thresholdOffset;
        viewRadius = configuration.viewRadius;
        volumeRenderingWithChebyshev = configuration.volumeRenderingWithChebyshev;

        if (path.Length > 0 && needImport)
        {
            origin = new Importer().Load(path);
        }
        if (origin == null) return;
        SetTexture(origin);

        tracer = gameObject.AddComponent<Tracer>();
        gestureController = gameObject.GetComponent<GestureController>();
        gazeController = gameObject.GetComponent<GazeController>();

        invoker = gameObject.AddComponent<CMDInvoker>();
        invoker.tracer = tracer;
        invoker.savePath = savePath + "\\commands.json";

        //volume rendering post process
        postProcessVolume = GameObject.Find("volume").GetComponent<PostProcessVolume>(); 
        volumeRendering = postProcessVolume.profile.GetSetting<BaseVolumeRendering>();
        if (volumeRendering == null) volumeRendering = postProcessVolume.profile.AddSettings<BaseVolumeRendering>();
        volumeRendering.volume.overrideState = true;
        volumeRendering.volume.value = origin;

        if (volumeRenderingWithChebyshev)
        {
            Debug.Log("yes");
            var computer = gameObject.AddComponent<OccupancyMapCompute>();
            computer.ComputeOccupancyMap();
            computer.ComputeDistanceMap();
            volumeRendering.occupancyMap.overrideState = true;
            volumeRendering.distanceMap.overrideState = true;
            volumeRendering.dimension.overrideState = true;
            volumeRendering.blockSize.overrideState = true;
            volumeRendering.occupancyMap.value = computer.occupancyMap;
            volumeRendering.distanceMap.value = computer.distanceMap;
            volumeRendering.dimension.value = computer.dimension;
            volumeRendering.blockSize.value = computer.blockSize;
            VRShaderType = ShaderType.BaseAccelerated;
        }
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Z))
        {
            invoker.Undo();
        }
        if (Input.GetKey(KeyCode.X))
        {
            invoker.Redo();
        }
        if(Input.GetKeyDown(KeyCode.C) && useKeyBoard)
        {
            invoker.Execute(new AdjustCommand(tracer,curIndex));
        }
        if (Input.GetKey(KeyCode.G))
        {
            cube.GetComponent<BoxCollider>().enabled = !cube.GetComponent<BoxCollider>().enabled;
        }

        Camera.main!.clearFlags = CameraClearFlags.Color;
        Camera.main!.backgroundColor = Color.black;;
    }

    public void ApplyMask(RenderTexture mask, byte[] maskedVolumeData)
    {
        postProcessVolume.profile.GetSetting<BaseVolumeRendering>().mask.value = mask;
        volumeData = maskedVolumeData;
    }

    public void ApplySelection(RenderTexture selection)
    {
        postProcessVolume.profile.GetSetting<BaseVolumeRendering>().selection.value = selection;
    }

    public void SetTexture(Texture3D tex)
    {
        origin = tex;
        imageName = origin.name;
        //savePath = CreateSavePath($"./MyResult/{imageName}/");
        savePath = CreateSavePath($"C:\\Users\\80121\\Desktop\\MyResult\\{imageName}\\");
        originalDim = new Vector3Int(origin.width, origin.height, origin.depth);
        scaledDim = configuration.scaledDim;
        if (scale)
        {
            scaledVolume = TextureScaler.Scale(origin, scaledDim, gaussianSmoothing);   //Scale volume
            scaledDim = new Vector3Int(scaledVolume.width, scaledVolume.height, scaledVolume.depth);
        }
        else
        {
            scaledDim = originalDim;
            scaledVolume = origin;
        }
        Debug.Log($"{scaledVolume.width},{scaledVolume.height},{scaledVolume.depth}");

        volumeData = scaledVolume.GetPixelData<byte>(0).ToArray();
    }

    public async Task ReplaceTexture(string name)
    {
        var texLoadHandle = Addressables.LoadAssetAsync<Texture3D>(name);
        Debug.Log("Waiting");
        await texLoadHandle.Task;
        Texture3D tex = texLoadHandle.Result;
        var volumeRendering = postProcessVolume.profile.GetSetting<BaseVolumeRendering>();
        Debug.Log("Done");
        SetTexture(tex);
        volumeRendering.volume.value = tex;

        float maxComponent = new Vector3(originalDim.x, originalDim.y, originalDim.z).MaxComponent();
        cube.transform.localScale = new Vector3(originalDim.x / maxComponent, originalDim.y / maxComponent, originalDim.z / maxComponent);
    }

    private string CreateSavePath(string path)
    {
        if(!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        int existingNumber = -1;
        foreach (string directory in Directory.GetDirectories(path))
        {
            string directoryName = new DirectoryInfo(directory).Name;
            if (int.TryParse(directoryName, out int number))
            {
                existingNumber = Math.Max(existingNumber, number);
            }
        }

        string newDirectoryPath = Path.Combine(path, $"{existingNumber:D3}");
        Debug.Log(newDirectoryPath);
        if (Directory.Exists(newDirectoryPath) && Directory.GetFiles(newDirectoryPath).Length > 0 || existingNumber == -1)
        {
            string newDirectoryName = $"{existingNumber + 1:D3}"; 
            newDirectoryPath = Path.Combine(path, newDirectoryName);  

            Directory.CreateDirectory(newDirectoryPath);
        }

        return newDirectoryPath;

    }
}
