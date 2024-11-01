using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.XR.ARSubsystems.XRCpuImage;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
using System.IO;
using System;

public class ErrorHeatMapCompute : MonoBehaviour
{
    ComputeShader computeShader;
    public Config config;
    public string path;
    new public string name;

    private void Start()
    {

    }

    [InspectorButton]
    private void ComputeHeatMap()
    {
        config = Config.Instance;
        Vector3Int dim = config.originalDim;
        computeShader = Resources.Load("ComputeShaders/ErrorHeatMap") as ComputeShader;
        int kernel = computeShader.FindKernel("CSMain");
        RenderTexture result = new(dim.x, dim.y, 0, RenderTextureFormat.R8)
        {
            //graphicsFormat = GraphicsFormat.R8_UInt,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = dim.z,
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        float[] hits = getHits(path);
        Debug.Log(hits.Length);
        name = path.Substring(path.LastIndexOf('\\') + 1, path.LastIndexOf('.') - path.LastIndexOf('\\') - 1);
        ComputeBuffer hitsBuffer = new(hits.Length, sizeof(float), ComputeBufferType.Default);
        hitsBuffer.SetData(hits);

        computeShader.SetTexture(kernel, "Result", result);
        computeShader.SetBuffer(kernel, "_Hits", hitsBuffer);
        computeShader.SetInts("dim", new int[] { config.originalDim.x, config.originalDim.y, config.originalDim.z });
        computeShader.SetInt("_HitCount", hits.Length/3);

        computeShader.Dispatch(kernel, Mathf.CeilToInt(dim.x / 8) , Mathf.CeilToInt(dim.y / 8), Mathf.CeilToInt(dim.z / 8));

#if UNITY_EDITOR
        AssetDatabase.DeleteAsset($"Assets/Textures/HeatMap/{name}.Asset");
        AssetDatabase.CreateAsset(result, $"Assets/Textures/HeatMap/{name}.Asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
    }

    private float[] getHits(string path)
    {
        List<float> hits = new();
        if (!File.Exists(path))
        {
            Debug.Log("Error on reading swc file!");
            return null;
        }
        string[] strs = File.ReadAllLines(path);
        for (int i = 0; i < strs.Length; ++i)
        {
            if (strs[i].StartsWith("#")) continue;
            string[] words = strs[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int index = int.Parse(words[0]);
            Vector3 pos = new(float.Parse(words[2]), float.Parse(words[3]), float.Parse(words[4]));
            int type = int.Parse(words[1]);
            if (type != 3)
            {
                hits.AddRange(new float[] { pos.x, pos.y, pos.z });
            }

        }
        return hits.ToArray();
    }
}
