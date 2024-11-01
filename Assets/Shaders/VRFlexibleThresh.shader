Shader "VolumeRendering/FlexibleThreshold"
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
            sampler3D _Threshold;
            sampler3D _Connection;

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
                float alpha = 0;
                float3 col = 0;
                float dt = 0.001;
                float depth_connection = 99999;
                float depth_bkgThresh = 99999;
                int step = (dstLimit-dstToBox)/dt;
             
                for(int t = 0; t< 500000; t ++)
                {
                    float4 uv = float4(p + 0.5, 1);
                    float val = tex3Dlod(_Volume, uv);
                    //float connection = 0;
                    //float bkgThresh = 0;
                    float connection = tex3Dlod(_Connection,uv);
                    float bkgThresh = tex3Dlod(_Threshold,uv);
                    if(connection>0.01){
                        col.g = max(val,col.g);
                        depth_connection = min(depth_connection,t*dt);
                    }
                    if(val>=bkgThresh){
                        col.b = max(val,col.b);
                        depth_bkgThresh = min(depth_bkgThresh,t*dt);
                    }
                    else{
                        col.r = max(val,col.r);
                    }
                    alpha = max(alpha,val);
                    
                    if(alpha >= 0.98 || t * dt > dstLimit)
                    {
                        break;
                    }
		            p += direction * dt;
                }
                if(depth_connection > depth_bkgThresh + 0.05)  col.g=0;
                return float4(col.r,col.g,col.b,alpha);
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
                //return float4(depth*10,0,0,1);

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
                float dstLimit = min(depthEyeLinear - dstToBox, dstInsideBox);//actual ray distance in box

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
                if(dstLimit>0)
                {
                    float4 result =  RayMarching(localRayPos,localViewDir,dstToBox,dstLimit);
                    //result.a = clamp(result.a,25/255.0f, 0.85);
                    //result.r = clamp(result.r,25/255.0f, 0.85);
                    //result.g = clamp(result.g,25/255.0f, 0.85);
                    //result.b = clamp(result.b,25/255.0f, 0.85);

                    //color = float4(result.a * float4(1,1,1,1) + (1-result.a)*color.rgb,1);

                    //return color;
                    result.a = max(result.a,25/255.0f);
                    //result.a = max(result.a,25/255.0f);
                    //color = float4(result.a * float4(1,1,1,1) + (1-result.a)*color.rgb,1);
                    //return color;
                    float3 base;
                    if(result.a<0.1)
                    {
                        base = lerp(float3(1,1,1),float3(0.5,0.7,0.9),(result.a)/0.1);                    
                    }
                    else if(result.a>0.1 && result.a<0.5)
                    {
                        base = lerp(float3(0.5,0.7,0.9),float3(0.6,0.7,1),(result.a-0.1)/0.4);
                    }
                    else
                    {
                        base = lerp(float3(0.6,0.7,1),float3(1,1,1),(result.a-0.5)/0.5);
                    }
                    color = float4(result.a * base + max(0,(1-result.a))*color.rgb,1);

                    // color = max(color,15/255.0f);
                }
                return color;
            }
            ENDHLSL
        }
    }
}