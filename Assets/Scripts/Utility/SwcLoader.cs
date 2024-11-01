using IntraXR;
using Cysharp.Threading.Tasks;
using Fusion.Profiling;
using MixedReality.Toolkit.SpatialManipulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using static Fusion.Allocator;
using Random = UnityEngine.Random;

public class SwcLoader : Singleton<SwcLoader>
{
    public enum RadiusMode { line, origin, generate}
    public enum RenderingMode { SDF, Mesh}
    private Dictionary<int, Vector3> swcDict;
    private Dictionary<int, float> radiusDict;
    private Dictionary<int, int> parentDict;
    private Dictionary<int, int> typeDict;
    private Dictionary<int, float> batchDict;
    //private Dictionary<int, int> swc_map;
    public string filePath;
    public string directoryPath;
    public int nodeType;
    public bool useBatch;
    private int batchMax = 0;
    public float updateTime = 0.18f;
    public RadiusMode radiusMode = RadiusMode.generate;
    public RenderingMode renderingMode = RenderingMode.SDF;

    public Texture3D[] textures = new Texture3D[10];
    public Vector3[] postions = new Vector3[10];
    public int annotateNumber = 0;

    public bool loadNext = false;
    string[] swcFiles;
    
    //Start is called before the first frame update
    async void Start()
    {

        //for (int i = 0; i < 10; i++)
        //{
        //    LoadPath(dictionaryPath + "\\" + i + ".swc");
        //    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //    //cube.transform.position = postions[i];
        //    GameObject temp = new GameObject();
        //    temp.transform.SetParent(config._paintingBoard.transform);

        //    cube.transform.position = postions[i % 10];
        //    float maxDim = Math.Max(Math.Max(textures[i].width, textures[i].height), textures[i].depth);
        //    cube.transform.localScale = new Vector3(textures[i].width / maxDim, textures[i].height / maxDim, textures[i].depth / maxDim);
        //    cube.transform.SetParent(temp.transform);
        //    cube.AddComponent<BoxCollider>();
        //    cube.GetComponent<MeshRenderer>().enabled = false;
        //    temp.AddComponent<ObjectManipulator>();

        //    CreateNeuron(cube.transform, temp.transform, i ,new Vector3Int(textures[i].width, textures[i].height, textures[i].depth), textures[i]);
        //}


        //LoadDirectory(directoryPath);

    }

    [InspectorButton]
    async void ReLoadSwc()
    {
        await LoadSwc(filePath);
        CreateNeuron(Config.Instance.cube.transform, Config.Instance.originalDim, Config.Instance.Origin);
    }

    [InspectorButton]
    private void Reset()
    {
        batch = 0;
    }

    public float deltaTime = 0;
    public int batch = 0;
    private void Update()
    {
        if(!useBatch) { return; }
        deltaTime += Time.deltaTime;
        if (deltaTime > updateTime)
        {
            deltaTime = 0;
            GameObject reconstruction = GameObject.Find("Reconstruction");
            for (int i = 0; i < reconstruction.transform.childCount; i++)
            {
                GameObject oj = reconstruction.transform.GetChild(i).gameObject;
                int node_batch = oj.GetComponent<NodeInformation>().batch;
                if (node_batch > batch)
                {
                    oj.SetActive(false);
                }
                else
                {
                    oj.SetActive(true);
                }

            }
            batch++;
        }
    }

    private async Task LoadSwc(string path)
    {
        if (!File.Exists(path))
        {
            Debug.Log("Error on reading swc file!");
            return;
        }
        string[] strs = File.ReadAllLines(path);
        swcDict = new();
        radiusDict = new();
        parentDict = new();
        typeDict = new();
        batchDict = new();
        for (int i = 0; i < strs.Length; ++i)
        {
            if (strs[i].StartsWith("#")) continue;
            string[] words = strs[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int index = int.Parse(words[0]);

            typeDict[index] = nodeType >= 0 ? nodeType : int.Parse(words[1]);
            //typeDict[index] = nodeType;
            
            Vector3 swc = new(float.Parse(words[2]), float.Parse(words[3]), float.Parse(words[4]));
            //swcList[index] = swc.Div(config._originalDim).Mul(config._scaledDim);
            swcDict[index] = swc;
            radiusDict[index] = float.Parse(words[5]);
            parentDict[index] = int.Parse(words[6]);
            if (useBatch)
            {
                batchDict[index] = int.Parse(words[7]);
                batchMax = (int)Math.Max(batchMax, batchDict[index]);
            }
        }

        print($"load {Path.GetFileName(path)} success");

        //await Config.Instance.ReplaceTexture(Path.GetFileNameWithoutExtension(path));
    }

    private void LoadDirectory(string path)
    {
        swcFiles = Directory.GetFiles(path);
        RandomGenerate();
    }

    private void CreateNeuron(Transform transform, Vector3Int dim, Texture3D volume)
    {
        byte[] data = volume.GetPixelData<byte>(0).ToArray();
        Dictionary<int ,Marker> markers = new();
        foreach (var pair in swcDict)
        {
            Marker marker = new(pair.Value);
            switch(radiusMode)
            {
                case RadiusMode.origin:
                    {
                        marker.radius = radiusDict[pair.Key];
                        break;
                    }
                case RadiusMode.generate:
                    {
                        marker.radius = marker.MarkerRadius(data, dim.x, dim.y, dim.z, Config.Instance.BkgThresh);
                        // marker.radius = Math.Min(2*marker.radius, 4);
                        marker.radius = 1.5f * marker.radius;
                        break;
                    }
                case RadiusMode.line:
                    marker.radius = 1;
                    break;
            }
            if (pair.Key == 1) marker.radius = Config.Instance.somaRadius;
            if (pair.Key == 1) marker.radius = Math.Max(marker.radius,marker.MarkerRadius(data, dim.x, dim.y, dim.z, Config.Instance.BkgThresh));
            markers[pair.Key] = marker;
        }

        Debug.Log(swcDict.Count);
        foreach (var pair in swcDict)
        {
            int pid = parentDict[pair.Key];
            if (pid >=0) markers[pair.Key].parent = markers[pid];
            markers[pair.Key].type = parentDict.ContainsValue(pair.Key)?typeDict[pair.Key]:-typeDict[pair.Key];
            //markers[pair.Key].type = typeDict[pair.Key];
            if (useBatch)
            {
                markers[pair.Key].batch = (int)batchDict[pair.Key];
                batchDict[pair.Key] = batchDict[pair.Key] / 50;
            }
        }

        if(useBatch)
        {
            Primitive.CreateTree(markers.Values.ToList(), transform, dim, batchDict.Values.ToList());
        }
        else
        {
            switch (renderingMode)
            {
                case RenderingMode.SDF:
                    Primitive.CreateTreeSDF(markers.Values.ToList(), transform, dim);
                    break;
                case RenderingMode.Mesh:
                    Primitive.CreateTree(markers.Values.ToList(), transform, dim);
                    break;
            }
            // Primitive.CreateTreeSDF(markers.Values.ToList(), transform, dim);
            
        }
    }
    private void CreateNeuron(Transform transform, Transform reconstruction, int colorTemp, Vector3Int dim, Texture3D volume)
    {
        byte[] data = volume.GetPixelData<byte>(0).ToArray();
        Dictionary<int ,Marker> markers = new();
        foreach (var pair in swcDict)
        {
            Marker marker = new(pair.Value);
            switch(radiusMode)
            {
                case RadiusMode.origin:
                    {
                        marker.radius = radiusDict[pair.Key];
                        break;
                    }
                case RadiusMode.generate:
                    {
                        marker.radius = marker.MarkerRadius(data, dim.x, dim.y, dim.z, Config.Instance.BkgThresh);
                        // marker.radius = Math.Max(marker.radius, 5);
                        break;
                    }
                case RadiusMode.line:
                    marker.radius = 1;
                    break;
            }
            if (pair.Key == 1) marker.radius = Config.Instance.somaRadius;
            if (pair.Key == 1) marker.radius = Math.Max(marker.radius, marker.MarkerRadius(data, dim.x, dim.y, dim.z, Config.Instance.BkgThresh));
            markers[pair.Key] = marker;
        }

        Debug.Log(swcDict.Count);
        foreach (var pair in swcDict)
        {
            int pid = parentDict[pair.Key];
            if (pid >=0) markers[pair.Key].parent = markers[pid];
            markers[pair.Key].type = typeDict[pair.Key];
            // markers[pair.Key].type = 1;
            if (useBatch)
            {
                markers[pair.Key].batch = (int)batchDict[pair.Key];
                batchDict[pair.Key] = batchDict[pair.Key] / 50;
            }
        }

        if(useBatch)
        {
            Primitive.CreateTree(markers.Values.ToList(), transform, dim, batchDict.Values.ToList());
        }
        else
        {
            //Primitive.CreateTree(markers.Values.ToList(), transform, dim);
            Primitive.CreateTreeTemp(markers.Values.ToList(), transform,reconstruction, colorTemp, dim);
        }
    }

    private async void RandomGenerate()
    {
        Random.InitState(1802);
        List<int> seedList = new();
        GameObject chevron = GameObject.Find("Chevron");

        for (int i = 0; i < swcFiles.Length; i++)
        {
            seedList.Add(Random.Range(1, 10000));
        }

        for (int i = 0; i < swcFiles.Length; i++)
        {
            Random.InitState(seedList[i]);
            Debug.Log(seedList[i]);
            await LoadSwc(swcFiles[i]);

            imageIndex = i;
            BoardManager.Instance.ClearTargets();
            BoardManager.Instance.ClearReconstruction();

            Config.Instance.gazeController.enabled = true;
            Config.Instance.gazeController.interactionType = GazeController.EyeInteractionType.LabelRefine;

            //CreateNeuron(Config.Instance.cube.transform, Config.Instance.originalDim, Config.Instance.Origin);

            await UniTask.WaitUntil(() => loadNext);
            loadNext = false;
            
            List<TargetInfo> filtered = FilterBranchAndLeaf();
            for (int j = 0; j < 10; j++)
            {
                int index = Random.Range(0, filtered.Count - j);
                GameObject targetGO = BoardManager.Instance.CreatePoint(filtered[index].position, Config.Instance.originalDim, new Color(1, 0.4f, 0.0f,1));
                labelIndex = j;
                target = filtered[index];
                preTime = Time.realtimeSinceStartup;
                filtered.SwapAtIndices(index, filtered.Count - 1 - j);
                chevron.GetComponent<DirectionalIndicator>().DirectionalTarget = targetGO.transform;
                await UniTask.WaitUntil(() => loadNext);
                loadNext = false;
                GameObject.Destroy(targetGO);
                BoardManager.Instance.ClearTargets();
            }
            //await UniTask.WaitUntil(() => loadNext);
            target = null;
            loadNext = false;
        }
        LabelInfo[] labelInfoArray = labelInfoList.ToArray();
        string listJson = JsonUtility.ToJson(new SerializableLabelInfoArray(labelInfoArray), true);
        DirectoryInfo folder = new DirectoryInfo("./LabelInfo");
        FileInfo fileInfo = new($"./LabelInfo/{folder.GetFiles().Length}.json");
        StreamWriter sw= fileInfo.CreateText();
        sw.WriteLine(listJson);
        sw.Close();
        sw.Dispose();
    }

    [InspectorButton]
    public void NextLabel()
    {
        loadNext = true;
    }

    [InspectorButton]
    public void ResetTime()
    {
        preTime = Time.realtimeSinceStartup;
    }

    private List<TargetInfo> FilterBranchAndLeaf()
    {
        Vector3 rootCoordinate = swcDict[0].Divide(Config.Instance.originalDim).Multiply(Config.Instance.scaledDim);
        List<TargetInfo> result = new();
        Dictionary<int,int> childCount = new();
        foreach(var pair in parentDict)
        {
            if (pair.Value >= 0)
            {
                if (!childCount.ContainsKey(pair.Key)) childCount[pair.Key] = 0;
                if (childCount.ContainsKey(pair.Value)) childCount[pair.Value]++;
                else childCount[pair.Value] = 0;
            }
        }
        foreach(var pair in childCount)
        {
            if (pair.Value == 0 || pair.Value == 2)
            {
                Vector3 position = swcDict[pair.Key];
                Vector3 cubicCoordinate = position.Divide(Config.Instance.originalDim);
                Vector3 scaledCoordinate = cubicCoordinate.Multiply(Config.Instance.scaledDim);
                int index = Utils.CoordinateToIndex(scaledCoordinate, Config.Instance.scaledDim);
                if (Config.Instance.VolumeData[index] >= 30 && Vector3.Distance(rootCoordinate, scaledCoordinate) >= 40)
                {
                    result.Add(new(position, Config.Instance.VolumeData[index], pair.Value==0));
                }
            }
        }
        return result;
    }

    public void AddLabel(Vector3 position)
    {
        if (target == null) return;
        LabelInfo labelInfo = new LabelInfo(imageIndex, labelIndex, target.position, position, Time.realtimeSinceStartup - preTime, target.intensity, target.isLeaf);
        labelInfoList.Add(labelInfo);

        Debug.Log($"distance:{Vector3.Distance(target.position,position)},time:{Time.realtimeSinceStartup - preTime}, intensity:{target.intensity}");
    }

    [Serializable]
    public class SerializableLabelInfoArray
    {
        public LabelInfo[] array;

        public SerializableLabelInfoArray(LabelInfo[] array)
        {
            this.array = array;
        }
    }

    public List<LabelInfo> labelInfoList = new();
    public int imageIndex;
    public int labelIndex;
    public TargetInfo target;
    public float preTime;

    public class TargetInfo
    {
        public Vector3 position;
        public int intensity;
        public bool isLeaf;

        public TargetInfo(Vector3 position, int intensity, bool isLeaf)
        {
            this.position = position;
            this.intensity = intensity;
            this.isLeaf = isLeaf;
        }
    }

    [Serializable]
    public class LabelInfo
    {
        public int imageIndex;
        public int labelIndex;
        public Vector3 targetPosition;
        public Vector3 labelPosition;
        public float time;
        public int targetIntensity;
        public bool isLeaf;

        public LabelInfo(int imageIndex, int labelIndex, Vector3 targetPosition, Vector3 labelPosition, float time, int targetIntensity, bool isLeaf)
        {
            this.imageIndex = imageIndex;
            this.labelIndex = labelIndex;
            this.targetPosition = targetPosition;
            this.labelPosition = labelPosition;
            this.time = time;
            this.targetIntensity = targetIntensity;
            this.isLeaf = isLeaf;
        }
    }
}
