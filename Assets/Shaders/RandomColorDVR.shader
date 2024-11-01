Shader "Unlit/RandomColorDVR"
{
    Properties
    {
        _VolumeScale("VolumeScale", Vector) = (1,1,1,1)
        _Volume("Volume", 3D) = "white" {}
        _Divided("Divided",3D) = "while"{}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100 
        Cull Front
        CGINCLUDE
        float3 RGB2HSV(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            float e = 1.0e-10;
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
        }
        float3 HSV2RGB(float3 c)
        {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }
        ENDCG

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
            sampler3D _Divided;
            float4x4 _Rotation;
            float4x4 _Translation;
            float4x4 _Scale;

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

            float Random1DTo1D(float value, float a, float b) {
                //make value more random by making it bigger
                float random = frac(sin(value + b) * a);
                return random;
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


                float col = 0;
                float alpha = 0;
                float3 p = i.transformed_eye + t_hit.x * ray_dir;
                int first_id = -1;
                
                for(int t = 0; t< 500000; t ++){
                    float4 uv = float4(p + 0.5, 1);
                    float val = tex3Dlod(_Volume, uv);
                    float id_val = tex3Dlod(_Divided, uv);
                    int id = round(id_val * 255);
                    if (first_id == -1 && id > 0) {
                        first_id = id;
                        col = max(col, val);
                        alpha = max(alpha, val);
                    }
                    if (id > 0 && id != first_id) break;
                    col = max(col, val);
                    alpha = max(alpha, val);
                    //col = (1 - alpha)*val + col;
                    //alpha = (1 - alpha) * val + alpha;
                    
                    if(alpha>=0.98||t_hit.x+t*dt>t_hit.y){
                        break;
                    }
                    
		            p += ray_dir * dt;
                }
                if (col <= 10/255.0f) return fixed4(col, col, col, alpha);
                float3 hsv = RGB2HSV(float3(col, col, col));
                //hsv.x += Random1DTo1D(hsv.x,first_id,first_id);
                hsv.r =  0.5+Random1DTo1D(first_id,first_id,first_id);
                hsv.g = 0.5+col;
                float3 c = HSV2RGB(hsv);
                return fixed4(c,alpha);
            }
            ENDCG
        }
    }
}
