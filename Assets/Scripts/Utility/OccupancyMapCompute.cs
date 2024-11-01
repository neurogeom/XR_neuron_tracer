using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OccupancyMapCompute : MonoBehaviour
{
    public ComputeShader occupancyCompute;
    public ComputeShader distanceCompute;
    public int blockSize = 4;
    public Vector3Int dimension;
    public Texture3D volume;
    public Texture3D occupancyMap;
    public Texture3D distanceMap;
    public int bkgThreshold;

    private RenderTexture resultTexture;
    private RenderTexture dist_swap;
    private Vector3Int dMap;
    // Start is called before the first frame update

    public  void ComputeOccupancyMap()
    {
        Config config = gameObject.GetComponent<Config>();
        ComputeShader occupancyCompute = Resources.Load("ComputeShaders/OccupancyMap") as ComputeShader;
        
        volume = config.Origin;
        blockSize = config._blockSize;
        bkgThreshold = config.BkgThresh;
        dimension = new Vector3Int(volume.width, volume.height, volume.depth);
        Debug.Log(dimension);
        // bkgThreshold = 45;
        blockSize = 4;
        dMap = dimension / blockSize;
        int kernelKey = occupancyCompute.FindKernel("OccupancyMap");
        occupancyCompute.SetInt("BlockSize", blockSize);
        occupancyCompute.SetFloats("Dimensions", dimension.x, dimension.y, dimension.z);
        occupancyCompute.SetTexture(kernelKey, "Volume", volume);
        occupancyCompute.SetInt("bkgThreshold", bkgThreshold);

        resultTexture = new RenderTexture(dMap.x, dMap.y, 0, RenderTextureFormat.R8);
        resultTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        resultTexture.volumeDepth = dMap.z;
        resultTexture.enableRandomWrite = true;

        dist_swap = new RenderTexture(dMap.x, dMap.y, 0, RenderTextureFormat.RInt);
        dist_swap.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        dist_swap.volumeDepth = dMap.z;
        dist_swap.enableRandomWrite = true;

        occupancyCompute.SetTexture(kernelKey, "Result", resultTexture);
        occupancyCompute.SetTexture(kernelKey, "dist_swap", dist_swap);
        occupancyCompute.Dispatch(kernelKey, Mathf.CeilToInt(dMap.x / 8.0f), Mathf.CeilToInt(dMap.y / 8.0f), Mathf.CeilToInt(dMap.z / 8.0f));

        RenderTexture.active = resultTexture;
        Texture2D tex2d = new Texture2D(dMap.x, dMap.y, TextureFormat.R8, false);

        var texture3D = readRenderTexture3D(resultTexture);

        string name = volume.name;
#if UNITY_EDITOR
        AssetDatabase.DeleteAsset("Assets/Resources/Textures/_occupancy" + ".Asset");
        AssetDatabase.CreateAsset(texture3D, "Assets/Resources/Textures/_occupancy" + ".Asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
        Debug.Log("SavingTexture succeeded!");
        occupancyMap = texture3D;
    }

    public void ComputeDistanceMap()
    {
        ComputeShader distanceCompute = Resources.Load("ComputeShaders/ParallelDistanceMap") as ComputeShader;
        var dist = new RenderTexture(dMap.x, dMap.y, 0, RenderTextureFormat.RInt);
        dist.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        dist.volumeDepth = dMap.z;
        dist.enableRandomWrite = true;

        var result = new RenderTexture(dMap.x, dMap.y, 0, RenderTextureFormat.R8);
        result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        result.volumeDepth = dMap.z;
        result.enableRandomWrite = true;

        int kernelKey = distanceCompute.FindKernel("DistanceMap");
        distanceCompute.SetInts("dM", dMap.x, dMap.y, dMap.z);
        distanceCompute.SetTexture(kernelKey, "dist_swap", dist_swap);
        distanceCompute.SetTexture(kernelKey, "dist", dist);

        distanceCompute.SetInt("stage", 0);
        distanceCompute.Dispatch(kernelKey, Mathf.CeilToInt(dMap.y / 8.0f), Mathf.CeilToInt(dMap.z / 8.0f), 1);
        distanceCompute.SetInt("stage", 1);
        distanceCompute.Dispatch(kernelKey, Mathf.CeilToInt(dMap.x / 8.0f), Mathf.CeilToInt(dMap.z / 8.0f), 1);
        distanceCompute.SetInt("stage", 2);
        distanceCompute.Dispatch(kernelKey, Mathf.CeilToInt(dMap.x / 8.0f), Mathf.CeilToInt(dMap.y / 8.0f), 1);

        kernelKey = distanceCompute.FindKernel("ReadTexture");
        distanceCompute.SetTexture(kernelKey, "Result", result);
        distanceCompute.SetTexture(kernelKey, "dist", dist);
        distanceCompute.Dispatch(kernelKey, Mathf.CeilToInt(dMap.x / 8.0f), Mathf.CeilToInt(dMap.y / 8.0f), Mathf.CeilToInt(dMap.z / 8.0f));

        distanceMap = readRenderTexture3D(result);
#if UNITY_EDITOR
        AssetDatabase.DeleteAsset("Assets/Resources/Textures/_distanceMap" + ".Asset");
        AssetDatabase.CreateAsset(distanceMap, "Assets/Resources/Textures/_distanceMap" + ".Asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
        Debug.Log("Calculated DistanceMap");
        //AssetDatabase.DeleteAsset("Assets/Textures/" + "Distance2" + ".Asset");
        //AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + "Distance2" + ".Asset");
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();
        //Debug.Log("SavingTexture succeeded!");


    }

    //void computeDistanceMap()
    //{
    //    int kernelKey = distanceCompute.FindKernel("DistanceMap");
    //    distanceCompute.SetFloats("dM", dMap.x, dMap.y, dMap.z);
    //    distanceCompute.SetTexture(kernelKey, "OccupancyMap", OccupancyMap);

    //    Texture3D texture3D = new Texture3D(dMap.x, dMap.y, dMap.z, TextureFormat.ARGB32, false);

    //    Texture2D[] texture2Ds = new Texture2D[dMap.z];
    //    List<Color> tex = new List<Color>();

    //    for (int i = 0; i < dMap.z; i++)
    //    {
    //        resultTexture = new RenderTexture(dMap.x, dMap.y, 1, RenderTextureFormat.ARGB32);
    //        resultTexture.enableRandomWrite = true;
    //        resultTexture.Create();
    //        distanceCompute.SetTexture(kernelKey, "Result", resultTexture);
    //        distanceCompute.SetInt("depth", i);
    //        distanceCompute.Dispatch(kernelKey, dMap.x / 8, dMap.y / 8, 1);

    //        RenderTexture.active = resultTexture;
    //        texture2Ds[i] = new Texture2D(dMap.x, dMap.y, TextureFormat.ARGB32, false);
    //        texture2Ds[i].ReadPixels(new Rect(0, 0, dMap.x, dMap.y), 0, 0);
    //        texture2Ds[i].Apply();
    //        Color[] temp = texture2Ds[i].GetPixels();
    //        tex.AddRange(temp);
    //        Debug.Log("compute depth:" + i);
    //    }
    //    texture3D.SetPixels(tex.ToArray());
    //    texture3D.Apply();
    //    AssetDatabase.DeleteAsset("Assets/Textures/" + "Distance_compare_new" + ".Asset");
    //    AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + "Distance_compare_new" + ".Asset");
    //    AssetDatabase.SaveAssets();
    //    AssetDatabase.Refresh();
    //    Debug.Log("SavingTexture succeeded!");


    //}

    Texture3D readRenderTexture3D(RenderTexture source)
    {
        RenderTexture.active = source;
        Texture2D tex2d = new Texture2D(dMap.x, dMap.y, TextureFormat.R8, false);
        Texture3D texture3D = new Texture3D(dMap.x, dMap.y, dMap.z, TextureFormat.R8, false);
        List<Color> texColors = new List<Color>();

        for (int i = 0; i < dMap.z; i++)
        {
            var target = new RenderTexture(dMap.x, dMap.y, 0, RenderTextureFormat.R8);
            //tmp.Create();
            Graphics.CopyTexture(source, i, target, 0);
            RenderTexture.active = target;
            tex2d.ReadPixels(new Rect(0, 0, dMap.x, dMap.y), 0, 0);
            tex2d.Apply();
            texColors.AddRange(tex2d.GetPixels());
        }

        texture3D.SetPixels(texColors.ToArray());
        texture3D.filterMode = FilterMode.Point;
        texture3D.wrapMode = TextureWrapMode.Clamp;
        texture3D.Apply();

        return texture3D;
    }

}
