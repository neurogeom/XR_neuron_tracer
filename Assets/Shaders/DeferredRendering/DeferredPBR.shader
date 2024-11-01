Shader "Unlit/deferredLight"
{
    SubShader
    {
        // 第一个pass用于合成灯光
        Pass
        {
            // 由于像素信息已经经过深度测试，所以可以关闭深度写入
			ZWrite Off
            // 如果开启了LDR混合方案就是DstColor zero（当前像素 + 0），
            // 如果开启了HDR混合方案就是One One（当前像素 + 缓冲像素），由于延迟渲染就是等于把灯光渲染到已存在的gbuffer上，所以使用one one
			Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            // 定义运行平台
			#pragma target 3.0
            // 我们需要所有的关于灯光的变体，使用multi_compile_lightpass
			#pragma multi_compile_lightpass 
            // 不使用nomrt着色器
			#pragma exclude_renderers nomrt
            //定义UNITY_HDR_ON关键字
			//在c# 中 Shader.EnableKeyword("UNITY_HDR_ON"); Shader.DisableKeyword("UNITY_HDR_ON");
			// 设定hdr是否开启 
			#pragma multi_compile __ UNITY_HDR_ON
            // 定义顶点渲染器和片元渲染器的输入参数
            #pragma vertex vert
            #pragma fragment frag
            // 引入shader 相关宏宏
			#include "UnityCG.cginc"
			#include "UnityDeferredLibrary.cginc"
			#include "UnityGBuffer.cginc"
            //定义从 Deferred模型对象输入的屏幕像素数据 
			sampler2D _CameraGBufferTexture0;// 漫反射颜色
			sampler2D _CameraGBufferTexture1;// 高光、平滑度
			sampler2D _CameraGBufferTexture2;// 世界法线
            //顶点渲染器输出参数结构，包含顶点坐标、法线
			struct a2v
			{
				float4 pos:POSITION;
				float3 normal:NORMAL;
			};
            //片元渲染器输出结构，包含像素坐标、uv坐标
			struct Deffred_v2f
			{
				float4 pos: SV_POSITION;
				float4 uv:TEXCOORD;
				float3 ray : TEXCOORD1;
			};
            // 顶点渲染器
			Deffred_v2f vert(a2v v)
			{
				Deffred_v2f o;
                //将顶点坐标从模型坐标转化为裁剪坐标
				o.pos = UnityObjectToClipPos(v.pos);
                // 获取屏幕上的顶点坐标
				o.uv = ComputeScreenPos(o.pos);
                // 模型空间转 视角空间做i奥
				o.ray =	UnityObjectToViewPos(v.pos) * float3(-1,-1,1);
                // 插值
				o.ray = lerp(o.ray, v.normal, _LightAsQuad);
				return o;
			}

#define PI 3.14159265358

// D
float Trowbridge_Reitz_GGX(float NdotH, float a)
{
    float a2     = a * a;
    float NdotH2 = NdotH * NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

// F
float3 SchlickFresnel(float HdotV, float3 F0)
{
    float m = clamp(1-HdotV, 0, 1);
    float m2 = m * m;
    float m5 = m2 * m2 * m; // pow(m,5)
    return F0 + (1.0 - F0) * m5;
}

// G
float SchlickGGX(float NdotV, float k)
{
    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}
            
float3 PBR(float3 N, float3 V, float3 L, float3 albedo, float3 radiance, float roughness, float metallic)
{
    roughness = max(roughness, 0.05); 

    float3 H = normalize(L+V);
    float NdotL = max(dot(N, L), 0);
    float NdotV = max(dot(N, V), 0);
    float NdotH = max(dot(N, H), 0);
    float HdotV = max(dot(H, V), 0);
    float alpha = roughness * roughness;
    float k = ((alpha+1) * (alpha+1)) / 8.0;
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

    float  D = Trowbridge_Reitz_GGX(NdotH, alpha);
    float3 F = SchlickFresnel(HdotV, F0);
    float  G = SchlickGGX(NdotV, k) * SchlickGGX(NdotL, k);

    float3 k_s = F;
    float3 k_d = (1.0 - k_s) * (1.0 - metallic);
    float3 f_diffuse = albedo / PI;
    float3 f_specular = (D * F * G) / (4.0 * NdotV * NdotL + 0.0001);

	f_diffuse *=PI;
	f_specular *= PI;

    float3 color = (k_d * f_diffuse + f_specular) * radiance * NdotL;

    return color;
}

            
            //片段渲染器
            //设置片段渲染器输出结果的数据格式。如果开始hdr就使用half4,否则使用fixed4
			#ifdef UNITY_HDR_ON
			    half4
			#else
			    fixed4
			#endif
			frag(Deffred_v2f i) : SV_Target
			{
                // 定义光照属性
				float3 worldPos;//像素的世界位置
				float2 uv;//uv
				half3 lightDir;//灯光方向
				float atten;// 衰减
				float fadeDist;// 衰减距离
                //计算灯光数据，并填充光照属性数据，返回灯光的坐标，uv、方向衰减等等
				UnityDeferredCalculateLightParams(i, worldPos, uv, lightDir, atten, fadeDist);

				// 灯光颜色
				half3 lightColor = _LightColor.rgb * atten;
                //gbuffer与灯光合成后的像素数据
				half4 diffuseColor = tex2D(_CameraGBufferTexture0, uv);// 漫反射颜色
				half4 specularColor = tex2D(_CameraGBufferTexture1, uv);// 高光颜色
				float gloss = specularColor.a;//平滑度
				half4 gbuffer2 = tex2D(_CameraGBufferTexture2, uv);// 法线
				float3 worldNormal = normalize(gbuffer2.xyz * 2 - 1);// 世界法线

                // 视角方向 = 世界空间的摄像机位置 - 像素的位置
				fixed3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                // 计算高光的方向 = 灯光方向与视角方向中间的点
				fixed3 halfDir = normalize(lightDir + viewDir);
				float3 L = normalize(_WorldSpaceLightPos0.xyz);
				float3 V = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
				float3 color = PBR(worldNormal,V,L,diffuseColor.xyz,lightColor,1-specularColor.a,specularColor.r);
				
    //             // 漫反射 = 灯光颜色 * 漫反射颜色 * max（dot（像素世界法线， 灯光方向））
				// half3 diffuse = lightColor * diffuseColor.rgb * max(0,dot(worldNormal, lightDir));
    //             // 高光 =  灯光颜色 * 高光色  * pow(max(0,dot(像素世界法线，计算高光的方向)), 平滑度);
				// half3 specular = lightColor * specularColor.rgb * pow(max(0,dot(worldNormal, halfDir)),gloss);
    //             // 像素颜色 = 漫反射+高光，透明度为1
				// half4 color = float4(diffuse + specular,1);
				return float4(color,1);
    //             //如果开启了hdr则使用exp2处理颜色
				// #ifdef UNITY_HDR_ON
				//     return color;
				// #else 
				//     return exp2(-color);
				// #endif
			}

            ENDCG
        }

		//转码pass,主要用于LDR转码
		Pass
		{
            //使用深度测试，关闭剔除
			ZTest Always
			Cull Off
			ZWrite Off
            //模板测试
			Stencil
			{
				ref[_StencilNonBackground]
				readMask[_StencilNonBackground]

				compback equal
				compfront equal
			}
			CGPROGRAM
            //输出平台
			#pragma target 3.0
			#pragma vertex vert
            #pragma fragment frag
            // 剔除渲染器
			#pragma exclude_renderers nomrt
            //
			#include "UnityCG.cginc"
            //缓冲区颜色
			sampler2D _LightBuffer;
            struct a2v
			{
				float4 pos:POSITION;
				float2 uv:TEXCOORD0;
			};
			struct v2f
			{
				float4 pos:SV_POSITION;
				float2 uv:TEXCOORD0;

			};
            //顶点渲染器
			v2f vert(a2v v)
			{
				v2f o;
                // 坐标转为裁剪空间
				o.pos = UnityObjectToClipPos(v.pos);
				o.uv = v.uv;
                // 通常用于判断D3D平台，在开启抗锯齿的时候图片采样会用到
				#ifdef  UNITY_SINGLE_PASS_STEREO
				    o.uv = TransformStereoScreenSpaceTex(o.uv,1.0);
				#endif
				return o;
			}
            //片段渲染器
			fixed4 frag(v2f i): SV_Target
			{
				return -log2(tex2D(_LightBuffer,i.uv));
			}

			ENDCG
		}
    }
}



