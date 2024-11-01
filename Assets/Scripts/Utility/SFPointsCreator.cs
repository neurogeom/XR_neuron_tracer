using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;
/*
Created by BlackFJ
*/

///<summary>
///
///</summary>
public class SFPointsCreator : MonoBehaviour
{
    int numPoints;
    Vector3[] pointCoordinateArray;
    Color[] texture;
    Vector3 aabbMin = new Vector3(-0.5f, -0.5f, -0.5f);
    Vector3 aabbMax = new Vector3(0.5f, 0.5f, 0.5f);
    float step;
    float intensity, threshold;
    float[] texture3D;
    int width, height, depth;


    private void readTexture3d(string path)
    {
        width = 512;
        height = 512;
        depth = 512;
        texture3D = new float[width * height * depth];
        string[] words = File.ReadAllText(path).Split();
        for(int i = 0; i < width * height * depth; ++i)
        {
            texture3D[i] = float.Parse(words[i]) / 255.0f;
        }
        Debug.Log("Reading Succeeded!");
    }

    private void Start()
    {
        //readTexture3d("C:\\Users\\Black\\Desktop\\unet\\16416.000_15064.000_3092.000.v3draw.txt");
        texture3D = new float[512 * 512 * 512];
        numPoints = 512;
        intensity = 5.0f;
        threshold = 1.0f;
        pointCoordinateArray = new Vector3[numPoints];
        for(int i = 0; i < numPoints; ++i)
        {
            float z = 1.0f - (2.0f * i + 1.0f) / numPoints;
            float goldenRation = (Mathf.Sqrt(5.0f) + 1) / 2;
            float p = 2 * Mathf.PI * i/goldenRation;
            pointCoordinateArray[i] = C(p, Mathf.Acos(z));
            GameObject cur = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cur.transform.position = pointCoordinateArray[i];
            cur.transform.localScale = new Vector3(0.05f,0.05f,0.05f);
            cur.transform.parent = transform;
            //Debug.Log("Creating points set Succeeded!");
        }
        texture = new Color[numPoints * numPoints];

        //for(int i = 0; i < numPoints; ++i)
        //{
        //    for(int j = 0; j < numPoints; ++j)
        //    {
        //        if (i == j) continue;
        //        Vector3 start = pointCoordinateArray[i];
        //        Vector3 end = pointCoordinateArray[j];
        //        Vector3 direction = end - start;
        //        Ray r = new Ray(start, direction);
        //        float tnear, tfar;
        //        if (intersect(r, aabbMin, aabbMax, out tnear, out tfar))
        //        {
        //            Vector4 color_acc = new Vector4(0,0,0,0);
        //            float alpha_acc = 0;
        //            int x, y, z;
        //            Vector3 hit = start;
        //            float distance = (tfar - tnear) * direction.magnitude;
        //            float step_size = distance / Iterations;
        //            Vector3 ds = direction.normalized * step_size;
        //            for(int iter = 0; iter < Iterations; iter++)
        //            {
        //                world2VoxelIndex(hit, aabbMin, numPoints, out x, out y, out z);
                        
        //                if(x + y * width + z * width * height >= width * height * depth)
        //                {
        //                    Debug.Log("Out of boundary: ("+x.ToString() + "," + y.ToString() + "," + z.ToString()+")");
        //                }
        //                //float curColor = texture3D[x + y * width + z * width * height]*intensity;
        //                float curColor = 1.0f;
        //                float curAlpha = curColor * stepsize;
        //                color_acc += (1 - alpha_acc) * new Vector4(3 * curColor * curAlpha, 3 * curColor * curAlpha, 3 * curColor * curAlpha, 3 * curColor * curAlpha);
        //                alpha_acc += curAlpha;
        //                hit += ds;
        //                if (color_acc.w > threshold) break;
        //            }
        //            Vector4 result=new Vector4(color_acc.x * 2 + 0.2f, color_acc.y * 2 + 0.2f, color_acc.z * 2 + 0.2f, color_acc.w);
        //            if (result.x > 1.0f) result.x = 1.0f;
        //            if (result.x < 0.0f) result.x = 0.0f;
        //            if (result.y > 1.0f) result.y = 1.0f;
        //            if (result.y < 0.0f) result.y = 0.0f;
        //            if (result.z > 1.0f) result.z = 1.0f;
        //            if (result.z < 0.0f) result.z = 0.0f;
        //            if (result.w > 1.0f) result.w = 1.0f;
        //            if (result.w < 0.0f) result.w = 0.0f;
        //            texture[j + i * numPoints] = result;

        //        }
        //        else
        //        {
        //            texture[j + i * numPoints] = new Color(0.0f, 0.0f, 0.0f);
        //        }
        //    }
        //    Debug.Log(i.ToString());
        //}

        //Texture2D texture2d = new Texture2D(numPoints, numPoints, TextureFormat.RGBA32, true);
        //texture2d.SetPixels(texture);
        //texture2d.Apply();
        //AssetDatabase.CreateAsset(texture2d, "Assets/texture2d_test.asset");
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();
    }

    private Vector3 C(float theta,float phi)
    {
        return new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi));
    }

    private bool intersect(Ray r,Vector3 aabbMin,Vector3 aabbMax,out float tnear,out float tfar)
    {
        Vector3 rDirection = r.direction;
        Vector3 rOrigin = r.origin;
        Vector3 invR = new Vector3(1.0f/rDirection.x,1.0f/rDirection.y,1.0f/rDirection.z);
        Vector3 rMin = aabbMin - rOrigin;
        Vector3 rMax = aabbMax - rOrigin;
        Vector3 tbot = new Vector3(invR.x * rMin.x, invR.y * rMin.y, invR.z * rMin.z);
        Vector3 ttop = new Vector3(invR.x * rMax.x, invR.y * rMax.y, invR.z * rMax.z);
        Vector3 tmin = Vector3.Min(ttop, tbot);
        Vector3 tmax = Vector3.Max(ttop, tbot);
        float tx = Mathf.Max(tmin.x, tmin.y);
        float ty = Mathf.Max(tmin.x, tmin.z);
        tnear = Mathf.Max(tx, ty);
        tx = Mathf.Min(tmax.x, tmax.y);
        ty = Mathf.Min(tmax.x, tmax.z);
        tfar = Mathf.Min(tx, ty);
        return tnear <= tfar;

    }

    private void world2VoxelIndex(Vector3 hit,Vector3 aabbMin,int num,out int x,out int y,out int z)
    {
        float len=1f;
        Vector3 diff = hit - aabbMin;
        Vector3 diff_normalized = diff / len;
        x = Mathf.FloorToInt(diff_normalized.x * (num-1) + 0.5f);
        y = Mathf.FloorToInt(diff_normalized.y * (num-1) + 0.5f);
        z = Mathf.FloorToInt(diff_normalized.z * (num-1) + 0.5f);
    }
}
