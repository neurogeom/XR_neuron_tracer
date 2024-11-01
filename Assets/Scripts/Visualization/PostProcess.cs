using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System;

[Serializable]
[PostProcess(typeof(BaseVolumeRenderer), PostProcessEvent.AfterStack, "Unity/BaseVolumeRendering")]
public class BaseVolumeRendering : PostProcessEffectSettings
{
    public TextureParameter volume = new TextureParameter { value = null };
    public TextureParameter mask = new TextureParameter { value = null };
    public TextureParameter selection = new TextureParameter { value = null };
    public TextureParameter threshold = new TextureParameter { value = null };
    public TextureParameter connection = new TextureParameter { value = null };
    public TextureParameter occupancyMap = new TextureParameter { value = null };
    public TextureParameter distanceMap = new TextureParameter { value = null };
    public TextureParameter depth = new TextureParameter() { value = null };
    public FloatParameter viewThreshold = new FloatParameter { value = 0f };
    public Vector3Parameter dimension = new Vector3Parameter {  value = Vector3.zero };
    public FloatParameter blockSize = new FloatParameter { value = 0f };
    public Vector3Parameter position = new Vector3Parameter { value = Vector3.zero };
    public FloatParameter viewParam = new FloatParameter { value = 0f };
}

public sealed class BaseVolumeRenderer : PostProcessEffectRenderer<BaseVolumeRendering>
{
    public override void Render(PostProcessRenderContext context)
    {
        var cmd = context.command;
        cmd.BeginSample("BaseVolumeRendering");

        Config config =  GameObject.FindGameObjectWithTag("Config").GetComponent<Config>();

        Shader[] shaders = new Shader[4];
        shaders[0] = Shader.Find("VolumeRendering/Base");
        shaders[1] = Shader.Find("VolumeRendering/FlexibleThreshold");
        shaders[2] = Shader.Find("VolumeRendering/FixedThreshold");
        shaders[3] = Shader.Find("VolumeRendering/BaseAccelerated");

        var sheet = context.propertySheets.Get(shaders[(int)config.VRShaderType]);
        //var sheet = context.propertySheets.Get(shaders[0]);

        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(context.camera.projectionMatrix, false);

        sheet.properties.SetMatrix(Shader.PropertyToID("_InverseProjectionMatrix"), projectionMatrix.inverse);
        sheet.properties.SetMatrix(Shader.PropertyToID("_InverseViewMatrix"), context.camera.cameraToWorldMatrix);
        sheet.properties.SetMatrix(Shader.PropertyToID("_WorldToLocalMatrix"), config.cube.transform.worldToLocalMatrix);
        //sheet.properties.SetMatrix(Shader.PropertyToID("_WorldToLocalMatrix"), GameObject.Find("Cube").transform.worldToLocalMatrix);
        sheet.properties.SetTexture(Shader.PropertyToID("_Volume"), settings.volume.value);

        switch (config.VRShaderType)
        {
            case Config.ShaderType.Base:
                sheet.properties.SetFloat(Shader.PropertyToID("_viewParam"), settings.viewParam.value);
                break;
            case Config.ShaderType.FlexibleThreshold:
                sheet.properties.SetTexture(Shader.PropertyToID("_Threshold"), settings.threshold.value);
                sheet.properties.SetTexture(Shader.PropertyToID("_Connection"), settings.connection.value);
                break;
            case Config.ShaderType.FixedThreshold:
                sheet.properties.SetTexture(Shader.PropertyToID("_Mask"), settings.mask.value);
                sheet.properties.SetTexture(Shader.PropertyToID("_Selection"), settings.selection.value);
                sheet.properties.SetFloat(Shader.PropertyToID("_viewThreshold"), settings.viewThreshold.value);
                break;
            case Config.ShaderType.BaseAccelerated:
                sheet.properties.SetTexture(Shader.PropertyToID("_OccupancyMap"), settings.occupancyMap.value);
                sheet.properties.SetTexture(Shader.PropertyToID("_DistanceMap"), settings.distanceMap.value);
                sheet.properties.SetVector(Shader.PropertyToID("_Dimensions"), settings.dimension.value);
                sheet.properties.SetFloat(Shader.PropertyToID("_BlockSize"), settings.blockSize.value);
                break;
        }

        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);

        cmd.EndSample("BaseVolumeRendering");
    }
}

