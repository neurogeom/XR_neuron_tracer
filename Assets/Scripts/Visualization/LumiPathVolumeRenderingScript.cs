using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;
using UnityEditor;
using System.Linq;
/*
Created by BlackFJ
*/

///<summary>
///
///</summary>
///
public class Node
{
    public float thetaC;
    public float theta;
    public float phiC;
    public float phi;
    public float xC;
    public float yC;//idx=y*256+x

    public Node(float _t,float _p,int idx)
    {
        theta = _t;
        phi = _p;
        int x = idx % 256;
        int y = idx / 256;
        int thetaTimes = Mathf.FloorToInt(theta / Mathf.PI);
        theta -= thetaTimes * Mathf.PI;
        int phiTimes = Mathf.FloorToInt(phi / Mathf.PI / 2);
        phi -= phiTimes * Mathf.PI * 2;
        thetaC = theta / Mathf.PI;
        phiC = phi / 2 / Mathf.PI;
        xC = x / 256.0f;
        yC = y;
    }
}

public class LumiPathVolumeRenderingScript : MonoBehaviour
{
    [SerializeReference]public ComputeShader shader;
    [SerializeReference] public Texture3D VolumeData;
    private ComputeBuffer pointsBuffer;
    private ComputeBuffer texture3d;
    private RenderTexture resultTexture;

    Vector3[] pointsData;
    float[] texture3dData;
    int width, height, depth;
    int entryPointNum,exitPointNum;
    Vector3 aabbMin = new Vector3(-0.5f, -0.5f, -0.5f);
    Vector3 aabbMax = new Vector3(0.5f, 0.5f, 0.5f);
    float intensity = 5.0f;
    float threshold = 1.0f;

    private void readTextureData(string path,int w,int h,int d)
    {
        width = w;
        height = h;
        depth = d;
        texture3dData = new float[width * height * depth];
        string[] words = File.ReadAllText(path).Split();
        for (int i = 0; i < width * height * depth; ++i)
        {
            texture3dData[i] = float.Parse(words[i]) / 255.0f;
        }
        Debug.Log("ReadingTextureData succeeded!");

        Texture3D VolumeData = new Texture3D(512, 512, 512, TextureFormat.RFloat, false);
        VolumeData.SetPixelData(texture3dData, 0, 0);
        VolumeData.Apply();
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(VolumeData, "Assets/Textures/Volume.Asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
    }

    private Vector3 C(float theta, float phi)
    {
        return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi));
    }

    private void createPointsData(int num)
    {
        int pointNum = num;
        pointsData = new Vector3[pointNum];
        for (int i = 0; i < pointNum; ++i)
        {
            float z = 1.0f - (2.0f * i + 1.0f) / pointNum;
            float goldenRation = (Mathf.Sqrt(5.0f) + 1) / 2;
            float p = 2 * Mathf.PI * i / goldenRation;
            pointsData[i] = C(p, Mathf.Acos(z));
            Debug.Log(i + ": " + pointsData[i].x + " " + pointsData[i].y + " " + pointsData[i].z);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = new Vector3(pointsData[i].x, pointsData[i].y, pointsData[i].z);
            sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            sphere.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Volume Rendering/Cube"));

        }
        
        Debug.Log("CreatingPointsData succeeded!");
    }

    private void createLumiPathTexture(int EntryPointNum, int ExitPointNum)
    {   
        int kernelKey = shader.FindKernel("VolumeRendering");
        entryPointNum = EntryPointNum;
        exitPointNum = ExitPointNum;

        shader.SetInt("width", width);
        shader.SetInt("height", height);
        shader.SetInt("depth", depth);
        shader.SetInt("entryPointNum", entryPointNum);
        shader.SetInt("exitPointNum", exitPointNum);

        shader.SetVector("aabbMin", aabbMin);
        shader.SetVector("aabbMax", aabbMax);

        shader.SetFloat("intensity", intensity);
        shader.SetFloat("threshold", threshold);

        //Texture3D VolumeData = new Texture3D(512, 512, 512, TextureFormat.RFloat, false);
        //VolumeData.SetPixelData(texture3dData, 0, 0);
        //VolumeData.Apply();
        shader.SetTexture(kernelKey, "VolumeData", VolumeData);

        resultTexture = new RenderTexture(entryPointNum, exitPointNum, 1, RenderTextureFormat.R8);
        resultTexture.enableRandomWrite = true;
        resultTexture.Create();
        shader.SetTexture(kernelKey, "Result", resultTexture);

        shader.Dispatch(kernelKey, entryPointNum / 8, exitPointNum / 8, 1);

        Debug.Log("CreatingLumiPathTexture succeeded!");
    }

    private void saveTexture(string name)
    {
        Texture2D texture2D = new Texture2D(entryPointNum, exitPointNum, TextureFormat.R8, false);
        RenderTexture.active = resultTexture;
        texture2D.ReadPixels(new Rect(0, 0, entryPointNum, exitPointNum), 0, 0);
        texture2D.Apply();
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(texture2D, "Assets/Textures/"+name+".Asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SavingTexture succeeded!");
#endif
    }

    private void Start()
    {
        //createPointsData(512);
        //readTextureData("C:\\Users\\x\\Desktop\\Volume\\16416.000_15064.000_3092.000.v3draw.txt", 512,512,512);
        //createLumiPathTexture(12288,6144);
        createLumiPathTexture(16384 , 16384);
        saveTexture("LumiPathTexture");
        //createPointsTexture(512, "SFPointsTexture");
    }

    //void createPointsTexture(int num,string name)
    //{
    //    pointNum = num;
    //    List<OctNode> pointsSphereData = new List<OctNode>();
    //    float goldenRation = (Mathf.Sqrt(5.0f) + 1) / 2;
    //    for (int i = 0; i < pointNum; ++i)
    //    {
    //        float z = 1.0f - (2.0f * i + 1.0f) / pointNum;   
    //        float p = 2 * Mathf.PI * i / goldenRation;
    //        OctNode curNode = new OctNode(Mathf.Acos(z), p, i);
    //        pointsSphereData.Add(curNode);
    //    }
    //    var sortedEnumerable = pointsSphereData.OrderBy(p=>p.thetaC);
    //    Color[] tmpdata = new Color[pointNum];
    //    int idx = 0;
    //    foreach(OctNode cur in sortedEnumerable)
    //    {
    //        tmpdata[idx] = new Color(cur.thetaC, cur.phiC, cur.xC, cur.yC);
    //        idx++;
    //    }
    //    Texture2D texture2D = new Texture2D(pointNum, 1, TextureFormat.RGBA32, false);
    //    texture2D.SetPixels(tmpdata);
    //    texture2D.Apply();
    //    //AssetDatabase.CreateAsset(texture2D, "Assets/Textures/" + name + ".Asset");
    //    //AssetDatabase.SaveAssets();
    //    //AssetDatabase.Refresh();


    //}

}
