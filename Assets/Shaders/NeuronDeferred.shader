Shader "RayMarching/NeuronDeferred"
{
    Properties
    {
        [Header(PBS)]
        _Color1("Color1", Color) = (1.0, 1.0, 1.0, 1.0)
        _Color2("Color2", Color) = (1.0, 1.0, 1.0, 1.0)
        _Color3("Color3", Color) = (1.0, 1.0, 1.0, 1.0)
        _Color4("Color4", Color) = (1.0, 1.0, 1.0, 1.0)
        _Color5("Color5", Color) = (1.0, 1.0, 1.0, 1.0)
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.5
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        
        [Header(Raymarching)]
        _BlendDistance("BlendDistance", Range(0.00000001,1)) = 0.01
        _MaxLoop("MaxLoop", Range(1, 256)) = 30
        _RayHitThreshold("Ray Hit Threshold", Range(0.00000001, 0.5)) = 0.01
        _NormalPrecise("Normal Precise", Range(0.000001, 0.01)) = 0.0001
        _SDFPrecise("SDF Precise", Range(0, 0.01)) = 0.000001
        _MaxRayDistance ("Max Ray Distance", Range(1, 2000)) = 1000.0
        _DistanceMultiplier("Distance Multiplier", Range(0.001, 2.0)) = 1.0
        _MissColor("Color", Color) = (1.0, 0.0, 0.0, 1.0)

        _ShadowLoop("Shadow Loop", Range(1, 256)) = 30
        _ShadowMinDistance("Shadow Minimum Distance", Range(0.000001, 0.1)) = 0.01
        _ShadowExtraBias("Shadow Extra Bias", Range(0.0, 0.1)) = 0.0
        
        _SomaPos("Soma Position", Vector) = (0,0,1)
        _SomaRadius("Soma Radius", Float) = 0.00
        _SomaBlend("Soma Blend", Float) = 0.01

        // @block Properties
        _Distortion("Distortion", Range(0.0, 1.0)) = 0.2
        // @endblock
    }
    SubShader
    {

        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100
        Cull Front
        Pass
        {
            Tags
            {
                "LightMode"="Deferred"
            }
            ZWrite On

            CGPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma target 5.0
            #pragma vertex Vert
            #pragma fragment Frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "./include/Raymarching.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"
            #include "./include/Structs.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            int _MaxLoop;
            float _RayHitThreshold;
            float _MaxRayDistance;
            float _MaxShapesPerRay;
            fixed4 _Color;
            float _Glossiness;
            float _Metallic;



            struct VertOutput
            {
                float4 pos : SV_POSITION;
                float4 projPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            struct DeferredOutput
            {
                // RGB存储漫反射颜色，A通道存储遮罩
                float4 gBuffer0:SV_TARGET0;
                // RGB存储高光（镜面）反射颜色，A通道存储高光反射的指数部分，也就是平滑度
                float4 gBuffer1:SV_TARGET1;
                // RGB通道存储世界空间法线，A通道没用
                float4 gBuffer2:SV_TARGET2;
                // Emission + lighting + lightmaps + reflection probes (高动态光照渲染/低动态光照渲染)用于存储自发光+lightmap+反射探针深度缓冲和模板缓冲
                float4 gBuffer3:SV_TARGET3;
                float depth: SV_Depth;
            };

            VertOutput Vert(appdata_full v)
            {
                VertOutput o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.projPos = ComputeNonStereoScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);

                return o;
            }

            DeferredOutput Frag(VertOutput i)
            {
                DeferredOutput o;

                RaymarchInfo ray;
                INITIALIZE_RAYMARCH_INFO(ray, i, _MaxLoop, _RayHitThreshold);
                if (!march(ray)) discard;

                o.depth = ray.depth;
                o.gBuffer0 = ray.color;
                o.gBuffer1.r =  _Metallic;
                o.gBuffer1.a = _Glossiness;
                o.gBuffer2 = float4(ray.normal, 0);
                o.gBuffer3 = float4(ray.color.rgb/5, 1);
                return o;
            }
            ENDCG
        }

    }
    Fallback Off
}