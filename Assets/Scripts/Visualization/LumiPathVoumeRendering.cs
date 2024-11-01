using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LumiPathVoumeRendering : MonoBehaviour
{
    Camera DepthCamera;
    public Shader renderFrontDepthShader;
    public Shader renderBackDepthShader;
    public Material LumiPathVR;
    // Start is called before the first frame update
    void Start()
    {
        //Screen.SetResolution(540, 540, false);
    }

    // Update is called once per frame
    void Update()
    {

    }


    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (DepthCamera == null)
        {
            var go = new GameObject("DepthCamera");
            DepthCamera = go.AddComponent<Camera>();
            DepthCamera.enabled = false;
        }
        DepthCamera.CopyFrom(GetComponent<Camera>());//模拟一个眼睛

        RenderTexture frontDepth = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
        RenderTexture backDepth = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
        RenderTexture volumeTarget = RenderTexture.GetTemporary(source.width, source.height, 0);

        // Render 
        DepthCamera.targetTexture = backDepth;
        DepthCamera.RenderWithShader(renderBackDepthShader, "RenderType");
        DepthCamera.targetTexture = frontDepth;
        DepthCamera.RenderWithShader(renderFrontDepthShader, "RenderType");


        //// Render volume
        LumiPathVR.SetTexture("_FrontDepth", frontDepth);
        LumiPathVR.SetTexture("_BackDepth", backDepth);//将正反向渲染的深度图传入到Ray Marching Shader中
        Graphics.Blit(source, destination, LumiPathVR);//屏幕特效
        ////GameObject.Find("Plane").GetComponent<MeshRenderer>().material = _rayMarchMaterial;附在物体材质上
        //Release
        RenderTexture.ReleaseTemporary(volumeTarget);
        RenderTexture.ReleaseTemporary(frontDepth);
        RenderTexture.ReleaseTemporary(backDepth);
    }

}
