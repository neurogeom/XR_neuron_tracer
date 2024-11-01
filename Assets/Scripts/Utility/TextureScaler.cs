using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public class TextureScaler
{
	static ComputeShader scaleCS = (ComputeShader)Resources.Load("ComputeShaders/Texture3DScaler");

	public static Texture3D Scale(Texture3D src,Vector3Int dims, bool smoothing)
    {
		int kernelKey;
		RenderTexture filtered = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.R8);
		filtered.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		filtered.volumeDepth = src.depth;
		filtered.enableRandomWrite = true;
		filtered.filterMode = FilterMode.Trilinear;
		filtered.wrapMode = TextureWrapMode.Clamp;
		if (smoothing)
		{
			kernelKey = scaleCS.FindKernel("Smooth");
			scaleCS.SetFloats("dims",src.width,src.height,src.depth);
            scaleCS.SetTexture(kernelKey, "Source", src);
            scaleCS.SetTexture(kernelKey, "Destination", filtered);
            scaleCS.SetFloat("sigma",1);
            scaleCS.SetInt("kernelSize",3);
			scaleCS.Dispatch(kernelKey, Mathf.CeilToInt(src.width / 4.0f), Mathf.CeilToInt(src.height / 4.0f), Mathf.CeilToInt(src.depth / 4.0f));         
        }
		else
		{
            Debug.Log("copy");
            kernelKey = scaleCS.FindKernel("Copy");
            scaleCS.SetFloats("dims", src.width, src.height, src.depth);
            scaleCS.SetTexture(kernelKey, "Source", src);
            scaleCS.SetTexture(kernelKey, "Destination", filtered);
            scaleCS.Dispatch(kernelKey, Mathf.CeilToInt(src.width / 4.0f), Mathf.CeilToInt(src.height / 4.0f), Mathf.CeilToInt(src.depth / 4.0f));
        }

		int width = Mathf.Min(src.width,dims.x);
		int height = Mathf.Min(src.height,dims.y);
		int depth = Mathf.Min(src.depth,dims.z);
        RenderTexture res = new(width, height, 0, RenderTextureFormat.R8)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = depth,
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        kernelKey = scaleCS.FindKernel("Scale");
        scaleCS.SetFloats("dims", width, height, depth);
		scaleCS.SetTexture(kernelKey, "Volume", filtered);
		scaleCS.SetTexture(kernelKey, "Result", res);
		scaleCS.Dispatch(kernelKey, Mathf.CeilToInt(width / 4.0f), Mathf.CeilToInt(height / 4.0f), Mathf.CeilToInt(depth / 4.0f));

		Texture2D tex2d = new(width, height, TextureFormat.R8, false);
		List<Color> data = new();
		for (int i = 0; i < depth; i++)
		{
			var target = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
			//tmp.Create();
			Graphics.CopyTexture(res, i, target, 0);
			RenderTexture.active = target;
			tex2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
			tex2d.Apply();
			data.AddRange(tex2d.GetPixels());
		}

		Texture3D dst = new Texture3D(width, height, depth, TextureFormat.R8, false);
		dst.SetPixels(data.ToArray());
		dst.filterMode = FilterMode.Point;
		dst.wrapMode = TextureWrapMode.Clamp;
		dst.Apply();
#if UNITY_EDITOR
        AssetDatabase.DeleteAsset("Assets/Textures/" + "scaled" + ".Asset");
		AssetDatabase.CreateAsset(dst, "Assets/Textures/" + "scaled" + ".Asset");
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
#endif
        return dst;
	}

    static Texture3D readRenderTexture3D(RenderTexture source)
    {
        RenderTexture.active = source;
		int width = source.width;
		int height = source.height;
		int depth = source.volumeDepth;
        Texture2D tex2d = new Texture2D(width, height, TextureFormat.R8, false);
        List<Color> data = new List<Color>();
        for (int i = 0; i < depth; i++)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
            //tmp.Create();
            Graphics.CopyTexture(source, i, target, 0);
            RenderTexture.active = target;
            tex2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex2d.Apply();
            data.AddRange(tex2d.GetPixels());
        }

        Texture3D dst = new Texture3D(width, height, depth, TextureFormat.R8, false);
        dst.SetPixels(data.ToArray());
        dst.filterMode = FilterMode.Point;
        dst.wrapMode = TextureWrapMode.Clamp;
        dst.Apply();
        return dst;
    }
}
