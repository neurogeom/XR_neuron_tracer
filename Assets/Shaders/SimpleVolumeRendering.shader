Shader "Unlit/SimpleVolumeRendering"
{
    Properties
    {
        _VolumeScale("VolumeScale", Vector) = (1,1,1,1)
        _Volume("Volume", 3D) = "white" {}
        _IsDepth("IsDepth",Range(0.0,1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100 
        Cull Front
        
        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 vray_dir : TEXCOORD0;
                float3 transformed_eye : float3;
                float3 position : TEXCOORD1;
            };

            float4 _VolumeScale;
            sampler3D _Volume;
            float4x4 _Rotation;
            float4x4 _Translation;
            float4x4 _Scale;
            float _IsDepth;

            v2f vert (appdata v)
            {
                v2f o;
                float3 volume_translation = float3(0.5,0.5,0.5) - _VolumeScale.xyz * 0.5;
                //o.vertex = UnityObjectToClipPos(float4(v.vertex.xyz *  _VolumeScale.xyz + volume_translation,1));
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.position = v.vertex.xyz;
                //o.transformed_eye = mul(_Scale, mul(_Rotation, mul(_Translation, float4(_WorldSpaceCameraPos, 1)))).xyz;
                o.transformed_eye = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.vray_dir = v.vertex.xyz - o.transformed_eye;
                return o;
            }

            float2 intersect_box(float3 start, float3 dir){
                float3 box_min = float3(-0.5f,-0.5f,-0.5f);
                float3 box_max = float3(0.5f,0.5f,0.5f);
                float3 inverse_dir = 1.0f / dir;
                float3 tmin_temp = (box_min - start) * inverse_dir;
                float3 tmax_temp = (box_max - start) * inverse_dir;
                float3 tmin = min(tmin_temp, tmax_temp);
                float3 tmax = max(tmin_temp, tmax_temp);
                float t0 = max(tmin.x, max(tmin.y, tmin.z));
                float t1 = min(tmax.x, min(tmax.y, tmax.z));
                return float2(t0, t1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                
                float3 ray_dir = normalize(i.vray_dir);
                float2 t_hit = intersect_box(i.transformed_eye, ray_dir);
                if(t_hit.x>t_hit.y) discard;
                
                t_hit.x = max(t_hit.x, 0);
                //
                //float3 u_front = (i.transformed_eye + t_hit.x * ray_dir) * float3(512,512,512);
                //float3 u_back = (i.transformed_eye + t_hit.y * ray_dir) * float3(512, 512, 512);
                //float u_distance = distance(u_front, u_back);
                //float t_distance = distance(i.transformed_eye + t_hit.x * ray_dir, i.transformed_eye + t_hit.y * ray_dir);
                //float dt = t_distance / u_distance;
                float3 dt_vec = 1 / float3(float3(512,512,512) * abs(ray_dir));
                float dt = min(dt_vec.x, min(dt_vec.y, dt_vec.z));


                float col = -1;
                float alpha = 0;
                float3 p = i.transformed_eye + t_hit.x * ray_dir;
                float depth = 1;

                for(int t = 0; t< 50000; t ++){
                    float4 uv = float4(p + 0.5, 1);
                    float val = tex3Dlod(_Volume, uv);
                    if(val>0.1&&val>col)
                    {
                        float3 worldPos = mul(unity_ObjectToWorld,p).xyz;
                        depth = distance(worldPos,_WorldSpaceCameraPos);
                    }
                    col = max(col,val);
                    alpha = max(alpha,val);
                    if(alpha>=0.98||t_hit.x+t*dt>t_hit.y){
                        break;
                    }

		            p += ray_dir * dt;
                }
                if(_IsDepth<0.5f) return float4(col,col,col,1);
                else 
                {
                    depth -= 6;
                    return float4((1-float3(depth,depth,depth))*2,1);
                }
            }
            ENDCG
        }
    }
}
