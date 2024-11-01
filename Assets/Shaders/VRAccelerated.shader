Shader "VolumeRendering/BaseAccelerated"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex VertDefault
            #pragma fragment Frag
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            float _BlendMultiply;
            float4 _Color;
            float4x4 _InverseProjectionMatrix;
            float4x4 _InverseViewMatrix;
            float4x4 _WorldToLocalMatrix;
            float4x4 _TransposeObjectToWorldMatrix;

            TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
			TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
			sampler3D _Volume; 
            sampler3D _OccupancyMap;
            sampler3D _DistanceMap;
            float3 _Dimensions;
            float _BlockSize;

            float4 _CameraDepthTexture_TexelSize;

            float4 GetWorldSpacePosition(float depth, float2 uv)
            {
                // screen space --> view space 
                float4 view_vector = mul(_InverseProjectionMatrix, float4(2.0 * uv - 1.0, depth, 1.0));
                view_vector.xyz /= view_vector.w;
                //view space --> world space
                float4x4 l_matViewInv = _InverseViewMatrix;
                float4 world_vector = mul(l_matViewInv, float4(view_vector.xyz, 1));
                return world_vector;
            }

            float4 RayMarching(float3 startPoint, float3 direction, float dstToBox, float dstLimit) 
            {
                float3 p = startPoint + direction * dstToBox;
                float3 start = p;
                float alpha = 0;
                float3 col = 0;
                float dt = 0.001f;

                //float3 dt_vec = 1 / float3(float3(700,700,128) * abs(direction));
                //float dt = min(dt_vec.x, min(dt_vec.y, dt_vec.z));
                float3 deltaT = direction * dt;
                float3 deltaM = deltaT;
                float3 deltauM = deltaM * _Dimensions / _BlockSize ;
                float3 deltauM_inv = 1 / deltauM;
                float3 dM = _Dimensions / _BlockSize;
                float max_d = max(dM.x, max(dM.y, dM.z));

                float count = 0;
                for(int t = 0; t< 500000; t ++)
                {
                    float4 uv = float4(p + 0.5, 1);
                    float isOccupied = tex3Dlod(_OccupancyMap,float4(uv));
                    if(isOccupied>0)
                    {
                        count++;
                        float val = tex3Dlod(_Volume, uv);
                        val = max(val,0.1);
                        col.r = max(col.r,val);
                        // alpha = max(alpha,val);
                        
                        //if(alpha >= 0.98 || distance(p,start) > dstLimit) break;
                        
                
		                p += deltaM;
                    }
                    else
                    {
                        count++;
                        float3 uM = uv * _Dimensions / _BlockSize;
                        float3 rM = -frac(uM);
                        float D = tex3Dlod(_DistanceMap, float4(uv)) * max_d;
                        //float D = tex3D(_DistanceMap, uv) * 64;
                        //float D = 1;
                        
                        float3 deltai = ceil((step(0,-deltauM)+sign(deltauM)*D+rM)/deltauM);
                        //float3 deltai = ceil((step(0,-deltauM)+sign(deltauM)*D+rM)*deltauM_inv);
                        float step = max(min(min(deltai.x,deltai.y),deltai.z),1);
                        // step = 1;
                        p +=  step * deltaM;
                    }
                    // if( distance(p,start) > dstLimit) break;
                    if(alpha >= 0.98 || distance(p,start) > dstLimit) break;
                }

                // for(int t = 0; t< 500000; t ++){
                //         float4 uv = float4(p + 0.5, 1);
                //         float val = tex3Dlod(_Volume, uv);
                //         col.r = max(col.r,val);
                //         alpha = max(alpha,val);
                //     
                //         if(alpha >= 0.98 || t * dt > dstLimit){
                //             break;
                //         }
                //
		              //   p += direction * dt;
                // }
                // col.r = count / 500;
                return float4(col.r,col.r,col.r,col.r);
            }

            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, 
                            //local camera position      ray direction inverse
                            float3 rayOrigin, float3 invRaydir) 
            {
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                float dstA = max(max(tmin.x, tmin.y), tmin.z); 
                float dstB = min(tmax.x, min(tmax.y, tmax.z)); 

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            float4 Frag(VaryingsDefault i) : SV_Target
            {
                //screen depth
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoordStereo);

                float4 worldPos = GetWorldSpacePosition(depth, i.texcoord);
                float4 localPos = mul(_WorldToLocalMatrix, float4(worldPos.xyz,1));

                float3 rayPos = _WorldSpaceCameraPos;
                float3 localRayPos = mul(_WorldToLocalMatrix,float4(rayPos,1)).xyz;

                float3 localViewDir = normalize(localPos.xyz - localRayPos.xyz);

                float3 boundsMin = -0.5f * float3(1,1,1);
                float3 boundsMax = 0.5f * float3(1,1,1);

                float depthEyeLinear = distance(localPos.xyz,localRayPos);
                
                float2 rayToContainerInfo = rayBoxDst(boundsMin, boundsMax, localRayPos, (1 / localViewDir));
                float dstToBox = rayToContainerInfo.x; // distance from camera to box in local space
                float dstInsideBox = rayToContainerInfo.y; //distance of ray inside box
                //float dstLimit = min(depthEyeLinear - dstToBox, dstInsideBox);//actual ray distance in box
                float dstLimit = min(depthEyeLinear - dstToBox, dstInsideBox);//actual ray distance in box
                //dstLimit = dstInsideBox;

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
                if(dstLimit>0)
                {
                    float4 result =  RayMarching(localRayPos,localViewDir,dstToBox,dstLimit);
                    // return result;
                    //result.a = min(0.5,result.a);
                    //if(color.r>0||color.b>0||color.g>0)
                    //if(depthEyeLinear - dstToBox > dstInsideBox)
                    //{
                    //    color = result;
                    //}
                    //else
                    //{
                    //    color = float4(result.rgb * result.a + color.rgb * (1-result.a),1);
                    //}
                    //color = float4(float3(1,1,1) * result.a + color.rgb * (1-result.a),1);

                    //if(result.a>0.0001) 
                    //{
                    //    float thresh = 20;
                    //    //result.a = max(thresh/255.0f,result.a);
                    //}
                    //color = float4(float3(1,1,1) * result.a + color.rgb * (1-result.a),result.a);
 
                    result.a = min(1,result.a);
                    //result.a = max(result.a,25/255.0f);

                    if(result.a<0.1)
                    {
                        float3 base = lerp(float3(1,1,1),float3(0.5,0.7,0.9),(result.a)/0.1);
                        color = float4(result.a * base + (1-result.a)*color.rgb,1);
                    }
                    else if(result.a>0.1 && result.a<0.5)
                    {
                        float3 base = lerp(float3(0.5,0.7,0.9),float3(0.6,0.7,1),(result.a-0.1)/0.4);
                        color = float4(result.a * base + (1-result.a)*color.rgb,1);
                    }
                    else
                    {
                        float3 base = lerp(float3(0.6,0.7,1),float3(1,1,1),(result.a-0.5)/0.5);
                        color = float4(result.a * base + (1-result.a)*color.rgb,1);
                    }

                }
                return color;
            }
            ENDHLSL
        }
    }
}