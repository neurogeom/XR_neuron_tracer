// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Volume Rendering/CubeVolumeRenderShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VolumeTexture("VolumeTexture", 3D) = "white"{}
        _FrontDepth("FrontDepth",Rect) = "white"{}
        _BackDepth("BackDepth",Rect) = "white"{}
        _threshold("Threshold",float) = 0.1
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag
            

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler3D _VolumeTexture;
            sampler2D _FrontDepth;
            sampler2D _BackDepth;
            float _threshold;
            


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata_img v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            bool intersect(float3 origin, float3 direction, float3 aabbMin, float3 aabbMax, out float t0, out float t1) {
                float3 invR = 1.0 / direction;
                float3 tbot = invR * (aabbMin - origin);
                float3 ttop = invR * (aabbMax - origin);
                float3 tmin = min(ttop, tbot);
                float3 tmax = max(ttop, tbot);
                float2 t = max(tmin.xx, tmin.yz);
                t0 = max(t.x, t.y);
                t = min(tmax.xx, tmax.yz);
                t1 = min(t.x, t.y);
                return t0 <= t1;
            }

            float4 frag (v2f i):SV_Target
            {
                fixed4 frontPos = (tex2D(_FrontDepth, i.uv)-0.5)*2;
                fixed4 backPos = (tex2D(_BackDepth, i.uv)-0.5)*2;
                if(distance(frontPos,backPos)<0.0001f) return tex2D(_MainTex,i.uv);

                float3 aabbMin = float3(-1.0f,-1.0f,-1.0f);
                float3 aabbMax = float3(1.0f,1.0f,1.0f);
                float3 hitnear = frontPos;
                float3 hitfar = backPos;
                float dist = distance(hitnear, hitfar);
                float3 p = hitnear;
                float alpha_acc = 0;
                float color_acc = 0;
                float stepsize = 0.01;
                float3 voxelCoord = p-aabbMin;
                float3 ds = normalize(hitfar - hitnear) * stepsize;
                float length = 0;
                float bgColor = 0;
                for (int iter = 0; iter<50; iter++) {
                    float3 uv = (p - aabbMin) / (aabbMax - aabbMin);
                    float v = tex3D(_VolumeTexture,uv);
                    color_acc = max(color_acc,v);
                    alpha_acc = max(alpha_acc,v);
                    p += ds;
                    length +=stepsize;
                    if(length>dist){
                        break;
                    }
                }
                return float4(color_acc,color_acc,color_acc,alpha_acc);           
            }
            ENDCG
        }
    }
}
