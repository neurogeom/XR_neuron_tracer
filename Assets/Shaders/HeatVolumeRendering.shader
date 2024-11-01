Shader "Unlit/HeatVolumeRendering"
{
    Properties
    {
        _Color0("Color 0",Color) = (0,0,0,1)
        _Color1("Color 1",Color) = (0,.9,.2,1)
        _Color2("Color 2",Color) = (.9,1,.3,1)
        _Color3("Color 3",Color) = (.9,.7,.1,1)
        _Color4("Color 4",Color) = (1,0,0,1)

        _Range0("Range 0",Range(0,5)) = 0.
        _Range1("Range 1",Range(0,5)) = 0.25
        _Range2("Range 2",Range(0,5)) = 0.5
        _Range3("Range 3",Range(0,5)) = 0.75
        _Range4("Range 4",Range(0,5)) = 1

        _Diameter("Diameter",Range(0,1)) = 1.0
        _Strength("Strength",Range(.1,4)) = 1.0
        _PulseSpeed("Pulse Speed",Range(0,5)) = 0
    }
    SubShader
    {
		Tags { "RenderType" = "Transparent" }
        LOD 100 
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
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

			float4 _Color0;
			float4 _Color1;
			float4 _Color2;
			float4 _Color3;
			float4 _Color4;

			float _Range0;
			float _Range1;
			float _Range2;
			float _Range3;
			float _Range4;
			float _Diameter;
			float _Strength;
			float _PulseSpeed;

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

            float4 colors[5]; //colors for point ranges
			float pointranges[5];  //ranges of values used to determine color values
			float _Hits[1024*3]; //passed in array of pointranges 3floats/point, x,y,intensity 
			int _HitCount = 0;

			
			void initalize()
			{
			    colors[0] = _Color0;
			    colors[1] = _Color1;
			    colors[2] = _Color2;
			    colors[3] = _Color3;
			    colors[4] = _Color4;
			    pointranges[0] = _Range0;
			    pointranges[1] = _Range1;
			    pointranges[2] = _Range2;
			    pointranges[3] = _Range3;
			    pointranges[4] = _Range4;
			  
			}

            float4 getHeatForVoxel(float weight)
			{
				if (weight <= pointranges[0])
				{
				return colors[0];
				}
				if (weight >= pointranges[4])
				{
				return colors[4];
				}
				for (int i = 1; i < 5; i++)
				{
					if (weight < pointranges[i]) //if weight is between this point and the point before its range
					{
						float dist_from_lower_point = weight - pointranges[i - 1];
						float size_of_point_range = pointranges[i] - pointranges[i - 1];

						float ratio_over_lower_point = dist_from_lower_point / size_of_point_range;

						//now with ratio or percentage (0-1) into the point range, multiply color ranges to get color

						float4 color_range = colors[i] - colors[i - 1];

						float4 color_contribution = color_range * ratio_over_lower_point;

						float4 new_color = colors[i - 1] + color_contribution;
						return new_color;

					}
				}
				return colors[0];
			}

            float distsq(float3 a, float3 b)
			{
				float area_of_effect_size = _Diameter;

				return  pow(max(0.0, 1.0 - distance(a, b) / area_of_effect_size), 2.0);
			}

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                initalize();
                float3 ray_dir = normalize(i.vray_dir);
                float2 t_hit = intersect_box(i.transformed_eye, ray_dir);
                if(t_hit.x>t_hit.y) discard;
                
                t_hit.x = max(t_hit.x, 0);
                
                float3 dt_vec = 1 / float3(float3(512,512,512) * abs(ray_dir));
                float dt = min(dt_vec.x, min(dt_vec.y, dt_vec.z));


                //float3 col = float3(-1,-1,-1);
                float col;
                float alpha = 0;
                float3 p = i.transformed_eye + t_hit.x * ray_dir;

                float distance_to_tip = 99999;
                float totalWeight = 0.0;
                for(int t = 0; t< 500000; t ++){
                    float4 uv = float4(p + 0.5, 1);
                    
                    totalWeight = 0.0;
			        for (int i = 0; i < _HitCount; i++) 
			        {
				        float3 work_pt = float3(_Hits[i*3],_Hits[i*3+1],_Hits[i*3+2]);
				        //float pt_intensity = _Hits[i * 3 + 2];

				        //totalWeight += 0.1 * distsq(pos, work_pt) * _Strength * (1 + sin(_Time.y * _PulseSpeed));
				        totalWeight += 0.1 * distsq(p+0.5, work_pt) * _Strength;
			        }
                    //float4 val = getHeatForVoxel(totalWeight);

                    col = (1 - alpha) * totalWeight + col;
                    
                    alpha = (1 - alpha) * totalWeight + alpha;
                    //col = max(col,val);
                    //alpha = max(alpha,val.a);
                    //col = (1 - alpha)*val + col;
                    //alpha = (1 - alpha) * val + alpha;
                    
                    if(alpha>=0.98||t_hit.x+t*dt>t_hit.y){
                        break;
                    }

		            p += ray_dir * dt;
                }
                return fixed4(getHeatForVoxel(col).xyz,0.2f);
            }
            ENDCG
        }
    }
}
