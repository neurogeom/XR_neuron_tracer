using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

public class Tracer : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("setting")]
    public bool isMsfm = true;
    public int bkg_thresh = 30;

    public double SR_ratio = 9.0 / 9.0;
    public float lengthFactor = 4;
    public float lengthThreshold = 3f;
    public int tracingType = -1;
    public bool generateControlPoint = false;

    public HashSet<int> targets = new();
    
    byte[] occupancyData;
    List<Marker> filteredTree = new List<Marker>();
    Marker root;
    Marker msfm_root;
    List<Marker> outTree;

    private FastMarching fm = new();
    private HierarchyPruning hp = new(); 

    private List<Marker> completeTree;
    List<Marker> resampledTree;
    List<Marker> filteredBranch;
    List<Marker> resampledBranch;
    public bool trace = false;

    public FIM fim;
    private Config config;

    Dictionary<int,int> types = new();
    Dictionary<int,int> batches = new();

    public List<uint> foreground;
    float time;

    Coroutine curCoroutine;

    private void Start()
    {
        this.config = GetComponent<Config>();
        this.fim = gameObject.AddComponent<FIM>();
    }

    public void Initial(int bkgThreshold,float somaRadius,Vector3Int rootPos)
    {
        config.BkgThresh = bkgThreshold;
        config.somaRadius = somaRadius;
        config._rootPos = rootPos;
    }

    public void ClearBkgOffset()
    {

    }

    /// <summary>
    /// calculate the min geodesic distance of all voxel to the soma
    /// </summary>
    /// <param name="type">current operation type:initial,sweep,eye tracing</param>
    public void Trace(int type = 1)
    {
        FIMFI(type);
    }

    /// <summary>
    /// only calculate the part of trunk
    /// </summary>
    /// <param name="type">current operation type</param>
    public void TraceTrunk(int type = 1)
    {
        if(curCoroutine != null)StopCoroutine(curCoroutine);
        curCoroutine = StartCoroutine(FIMAsync(type));
        // FMM();
    }

    /// <summary>
    /// trace the remedy part
    /// </summary>
    /// <param name="type">current operation type</param>
    public void TraceBranch(int type = 1)
    {
        if (tracingType >= 0) type = tracingType;
        if (curCoroutine != null) StopCoroutine(curCoroutine);
        curCoroutine = StartCoroutine(FIMRemedyCoroutine(type));
    }

    public void DeleteBranch(HashSet<uint> modified, int type = 0)
    {
        FIMDelete(type, modified);
    }

    public void FIMFI(int type)
    {
        time = Time.realtimeSinceStartup;
        fim.DistanceTransform();
        Debug.Log($"DT complete in  {Time.realtimeSinceStartup - time}s");

        time = Time.realtimeSinceStartup;
        completeTree = fim.PotentialPathCalculation();

        Debug.Log(completeTree.Count);
        Debug.Log($"Initial restruction complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;
          PostProcess(type);
    }

    public void FIMRemedy(int type)
    {
        time = Time.realtimeSinceStartup;

        fim.DistanceTransform();
        Debug.Log($"DT complete in  {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;

        completeTree = fim.FIMRemedy();
        Debug.Log($"Initial restruction complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;
        PostProcess(type);
    }
    
    public IEnumerator FIMRemedyCoroutine(int type)
    {
        time = Time.realtimeSinceStartup;

        yield return StartCoroutine(fim.DistanceTransformAsync());
        Debug.Log($"DT complete in  {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;

        completeTree = new();
        yield return StartCoroutine(fim.TraceBranch(completeTree));
        Debug.Log($"Initial restruction complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;
        PostProcess(type);
    }

    private void FIMDelete(int type, HashSet<uint> modified)
    {
        time = Time.realtimeSinceStartup;

        fim.DistanceTransform();
        Debug.Log($"DT complete in  {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup; 

        completeTree = fim.FIMErase(modified);
        Debug.Log($"Initial restruction complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;
        PostProcess(type);
    }
    
    public IEnumerator FIMAsync(int type)
    {
        time = Time.realtimeSinceStartup;

        yield return StartCoroutine(fim.DistanceTransformAsync());

        Debug.Log($"DT complete in  {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;

        completeTree = new();

        yield return StartCoroutine(fim.InitialReconstructionAsync(completeTree));

        Debug.Log($"Initial reconstruction complete in {Time.realtimeSinceStartup - time}s complete Tree nums:{completeTree.Count} ");
        time = Time.realtimeSinceStartup;

        PostProcess(type);
    }

    public void FMM()
    {
        ClearResult();
        bkg_thresh = config.BkgThresh;
        byte[] img1d = config.VolumeData;
        Vector3Int dim = config.scaledDim;
        var cube = config.cube;
        float time = Time.realtimeSinceStartup;
        float calculationTime = 0;

        float[] gwdt = fm.FastMarching_dt_parallel(img1d, dim.x, dim.y, dim.z, bkg_thresh);
        
        Vector3 pos =
            (GameObject.Find("soma").transform.localPosition + new Vector3(0.5f, 0.5f, 0.5f)).Multiply(
                config.scaledDim);
        config._rootPos = pos.ToVector3Int();
        
        Marker root = new(config._rootPos);

        Debug.Log($"DT complete in  {Time.realtimeSinceStartup - time}s");
        calculationTime += Time.realtimeSinceStartup - time;
        time = Time.realtimeSinceStartup;

        if (isMsfm)
        {
            double[] msfm = fm.MSFM_dt_parallel(img1d, dim.x, dim.y, dim.z, bkg_thresh);
            fm.MSFM_tree(root, msfm, dim.x, dim.y, dim.z, 3, bkg_thresh, false);
            Debug.Log($"FI cost {Time.realtimeSinceStartup - time}");
            time = Time.realtimeSinceStartup;
        }

        completeTree = fm.FastMarching_tree(root, gwdt, dim.x, dim.y, dim.z, targets, 3, bkg_thresh, true);
        Debug.Log($"Initial restruction complete in {Time.realtimeSinceStartup - time}s");
        calculationTime += Time.realtimeSinceStartup - time;
        Debug.Log(completeTree.Count);
        time = Time.realtimeSinceStartup;

        Debug.Log($"FMM Reconstruction cost:{calculationTime}");

        filteredTree = hp.HierarchyPrune(completeTree, img1d, dim.x, dim.y, dim.z, ref config.somaRadius, bkg_thresh, SR_ratio);
        Debug.Log(filteredTree.Count);
        Debug.Log($"filter complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;

        resampledTree = hp.Resample(filteredTree, img1d, dim.x, dim.y, dim.z, 5);
        Debug.Log(resampledTree.Count);
        Debug.Log($"resample complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;

        Primitive.CreateTree(resampledTree, cube.transform, dim);
        Debug.Log($"create tree complete in {Time.realtimeSinceStartup - time}s");
    }

    /// <summary>
    /// including setting type, pruning, resampling and creating
    /// </summary>
    /// <param name="type"></param>
    private void PostProcess(int type)
    {
        time = Time.realtimeSinceStartup;
        //config.VolumeData = config.ScaledVolume.GetPixelData<byte>(0).ToArray();

        if ( type == 0)
        {
            types.Clear();
        }
        var newtypes = new Dictionary<int, int>(completeTree.Count);
        foreach (var item in completeTree)
        {
            int index = (int)item.img_index(config.scaledDim.x, config.scaledDim.x * config.scaledDim.y);
            if (!types.ContainsKey(index))
            {
                newtypes[index] = type;
            }
            else
            {
                newtypes[index] = types[index];
            }
            item.type = newtypes[index];
        }
        types = newtypes;

        var newbathces = new Dictionary<int, int>(completeTree.Count);
        foreach (var item in completeTree)
        {
            int index = (int)item.img_index(config.scaledDim.x,config.scaledDim.x * config.scaledDim.y);
            if(!batches.ContainsKey(index))
            {
                newbathces[index] = currentSequence+1;
                //Debug.Log(index + ": batches:" + currentSequence);
            }
            else
            {
                newbathces[index] = batches[index];
            }
            item.batch = newbathces[index];
        }
        batches = newbathces;
        filteredTree = hp.HierarchyPrune(completeTree, config.VolumeData, config.scaledDim.x, config.scaledDim.y, config.scaledDim.z, ref config.somaRadius, bkg_thresh, SR_ratio, lengthFactor, lengthThreshold);
        Debug.Log($"filtered Tree nums:{filteredTree.Count} filter complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup;

        var cloneTree = config.needFilter? CloneTree(filteredTree):completeTree;

        resampledTree = hp.Resample(cloneTree, config.VolumeData, config.scaledDim.x, config.scaledDim.y, config.scaledDim.z, config.needFilter ? config.resampleFactor:20);
        Debug.Log($"resample Tree nums:{resampledTree.Count} resample complete in {Time.realtimeSinceStartup - time}s");
        time = Time.realtimeSinceStartup; 

        List<float> growth = new(resampledTree.Count);
        foreach(var marker in resampledTree)
        {
            float value = (float)batches[(int)marker.img_index(config.scaledDim.x, config.scaledDim.x * config.scaledDim.y)] / (currentSequence+1);
            growth.Add(value);
        }

        //if (config.useBatch) Primitive.CreateTree(resampledTree, cube.transform, dim, growth);
        //else Primitive.CreateTree(resampledTree, cube.transform, dim);
        // Primitive.RpcCreateTree(config.runner, resampledTree, config.cube.transform, config.scaledDim);
        //Primitive.RpcCreateTreeSDF(config.runner, resampledTree, config.cube.transform, config.scaledDim);
        Primitive.CreateTreeSDF(resampledTree, config.cube.transform, config.scaledDim, generateControlPoint);
        Debug.Log($"create tree complete in {Time.realtimeSinceStartup - time}s");

    }

    public void CreateTree()
    {
        if (resampledTree == null || resampledTree.Count == 0) return;
        // Primitive.RpcCreateTree(config.runner, resampledTree, config.cube.transform, config.scaledDim);
        //Primitive.RpcCreateTreeSDF(config.runner, resampledTree, config.cube.transform, config.scaledDim);
        Primitive.CreateTreeSDF(resampledTree, config.cube.transform, config.scaledDim);
    }
    public void Pruning(CancellationToken token)
    {
        if (completeTree == null || completeTree.Count == 0) return;
        filteredTree = hp.HierarchyPrune(completeTree, config.VolumeData, config.scaledDim.x, config.scaledDim.y, config.scaledDim.z, ref config.somaRadius, bkg_thresh, SR_ratio, lengthFactor, lengthThreshold);

        var cloneTree = CloneTree(filteredTree);
        if(token.IsCancellationRequested)
            return;
        resampledTree = hp.Resample(cloneTree, config.VolumeData, config.scaledDim.x, config.scaledDim.y, config.scaledDim.z, config.resampleFactor);
    }

    public void AdjustIntensity(List<Vector3> track, float intensity)
    {
        var cube = config.cube;
        Vector3Int dim = config.scaledDim;
        Debug.Log(track.Count);
        float time = Time.realtimeSinceStartup;
        int radius = 1;
        if (track.Count == 0) return;
        HashSet<uint> targets = new();
        foreach (Vector3 v in track)
        {
            var pos = cube.transform.InverseTransformPoint(v) + new Vector3(.5f, .5f, .5f);
            pos = new Vector3(pos.x * dim.x, pos.y * dim.y, pos.z * dim.z);
            int x = (int)pos.x;
            int y = (int)pos.y;
            int z = (int)pos.z;
            int index = x + y * dim.x + z * dim.x * dim.y;
            targets.Add((uint)index);
            for (int i = x - radius; i <= x + radius; i++)
                for (int j = y - radius; j <= y + radius; j++)
                    for (int k = z - radius; k <= z + radius; k++)
                    {
                        if (i < 0 || i >= dim.x || j < 0 || j >= dim.y || k < 0 || k >= dim.z) continue;
                        if (Vector3Int.Distance(new(x, y, z), new(i, j, k)) <= 2)
                        {
                            int indexNeighbour = i + dim.x * j + k * dim.x * dim.y;
                            targets.Add((uint)indexNeighbour);
                        }
                    }
        }
        Debug.Log($"num need to be adjusted: {targets.Count}");


        fim.AdjustIntensity(targets.ToList(),intensity);
        Trace();
    }

    public void ModifyMask(List<uint> target, bool undo, int type)
    {
        if(target.Count == 0 || target.Count>5000) return;
        var (mask,volumeData) = fim.ModifyMask(target, undo);
        config.ApplyMask(mask,volumeData);
        if (type>0)
        {
            //type blocker
            Trace(type);
        }
    }

    public void ModifySelection(List<uint> target)
    {
        var selection = fim.ModifySelection(target);
        config.ApplySelection(selection);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="indexes">index of voxels to be adjusted</param>
    /// <param name="intensity">0 indicates automatic branch adjustment, otherwise indicates the magnitude of adjustment </param>
    /// <param name="undo"></param>
    public void AdjustIntensity(List<uint> indexes, float intensity, bool undo)
    {
        if (intensity == 0)  //branch
        {
            fim.AdjustIntensity(indexes,undo);
        }
        else
        {
            fim.AdjustIntensity(indexes, undo ? -intensity : intensity);
        }
    }

    public List<uint> GetBranch(uint index)
    {
        return fim.GetBranch(index);
    }

    public RenderTexture ConnectedPart(bool view)
    {
        return fim.ConnectedPart(view);
    }

    public bool Contained(uint index)
    {
        return fim.trunk.Contains(index);
    }

    public void SaveTree(int sequence, bool resampled, List<Marker> tree, bool withGrowth = false)
    {
        List<string> lines = new();
        lines.Add("#n type x y z radius parent");

        var relocatedTree = Relocation(tree);

        var soma = relocatedTree.Find((Marker marker) => { return marker.parent == null; });
        Dictionary<Marker, List<Marker>> dict = new();
        foreach (var node in relocatedTree)
        {
            dict[node] = new();
        }
        foreach (var node in relocatedTree)
        {
            if (node.parent == null) continue;
            dict[node.parent].Add(node);
        }
        Queue<Marker> queue = new();
        queue.Enqueue(soma);
        int index = 0;
        Dictionary<Marker, int> indexes = new();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            indexes[node] = index;
            var parentId = node.parent == null ? -1 : indexes[node.parent];
            string line = $"{index++} {node.type} {node.position.x} {node.position.y} {node.position.z} {node.radius} {parentId}";
            if(withGrowth)
            {
                line += $" {node.batch}";
            }
            lines.Add(line);
            foreach (var child in dict[node])
            {
                queue.Enqueue(child);
            }
        }
        if (!Directory.Exists(config.savePath))
        {

            Directory.CreateDirectory(config.savePath);
        }

        string savePath = config.savePath + $"\\{sequence}";
        if (resampled)
        {
            savePath += "_resampled";
        }
        if (withGrowth)
        {
            savePath += "_growth";
        }
        savePath += ".swc";
        System.IO.File.WriteAllLines(savePath, lines);
        //Debug.Log($"Save success to {savePath}");
    }

    int currentSequence = 0;
    public void Save(int sequence = -1)
    {
        if (sequence != -1) currentSequence = sequence;
        currentSequence = sequence;
        SaveTree(currentSequence, true, resampledTree,false);
        SaveTree(currentSequence, false, filteredTree,false);
        SaveTree(currentSequence, true, resampledTree,true);
        SaveTree(currentSequence, false, filteredTree,true);
    }

    public List<uint> GetCluster(uint seed)
    {
        var cluster = fim.GetCluster(seed);
        return cluster;
    }

    public void CloseToTrack(List<Vector3> postions)
    {
        Debug.Log(postions.Count > 0);
        ClearResult();
        int count = 0;
        foreach(var node in resampledTree)
        {
            var posA = node.position.Div(config.scaledDim).Mul(config.originalDim);
            foreach(var posB in postions)
            {
                if (Vector3.Distance(posA, posB) <= 20)
                {
                    node.type = -1;
                    count++;
                    break;
                }
            }
        }
        Debug.Log(count);
        // Primitive.CreateTree(resampledTree, config.cube.transform, dim);
        Primitive.CreateTreeSDF(resampledTree, config.cube.transform, config.scaledDim, generateControlPoint);

    }
    
    public void ClearResult()
    {
        GameObject reconstruction = GameObject.Find("Reconstruction");
        for (int i = 0; i < reconstruction.transform.childCount; i++)
        {
            GameObject.Destroy(reconstruction.transform.GetChild(i).gameObject);
        }
    }

    public List<Marker> CloneTree(List<Marker> origin)
    {
        Dictionary<Marker, Marker> markerCopy = new();
        List<Marker> treeCopy = new();
        foreach (Marker marker in origin)
        {
            markerCopy[marker] = new Marker(marker);
        }
        foreach(Marker marker in origin)
        {
            var copy = markerCopy[marker];
            if (marker.parent != null)
            {
                copy.parent = markerCopy[marker.parent];
            }
            treeCopy.Add(copy);
        }
        return treeCopy;
    }

    public float Confidence(List<uint> indexes)
    {
        byte[] volumeData = config.VolumeData;
        var dim = config.scaledDim;
        int foregroundCount = 0;
        foreach (uint index in indexes)
        {
            int intensity = volumeData[index];
            if (intensity >= config.ViewThresh) foregroundCount++;
        }
        return (float)(foregroundCount)/ (float)(indexes.Count);
    }

    (int, int, int) IndexToVec(int index, int width, int height, int depth)
    {
        int x = index % width;
        int y = index / width % height;
        int z = index / width / height % depth;
        return (x, y, z);
    }

    public void CullingForeground()
    {
        foreground = fim.GetForegroundExtension();
    }

    /// <summary>
    /// adjust the threshold of background with eye sweeping
    /// </summary>
    /// <param name="localHitPos">intersection of eye sight and volume in local space</param>
    /// <param name="localdirection">direction of eye sight in local space</param>
    internal void AdjustThreshold(Vector3 localHitPos, Vector3 localdirection)
    {
        fim.AdjustThreshold(localHitPos,localdirection);
    }

    internal RenderTexture InitThreshold()
    {
        return fim.InitThreshold();
    }

    private List<Marker> Relocation(List<Marker> origin)
    {
        List<Marker> relocated = new();
        Dictionary<Marker, Marker> map = new();
        foreach (Marker marker in origin)
        {
            Marker relocatedMarker = new Marker(marker);
            map[marker] = relocatedMarker;
            relocatedMarker.position = marker.position.Div(config.scaledDim).Mul(config.originalDim);
            relocated.Add(relocatedMarker);
        }

        foreach(Marker marker in relocated)
        {
            if(marker.parent != null)
            {
                marker.parent = map[marker.parent];
            }
        }
        return relocated;
    }

    public void HighlightNoise()
    {
        List<uint> indexes = GetCluster(Config.Instance.selectedIndex);

        ModifySelection(indexes);
    }

    public void LogTime()
    {

    }
}
