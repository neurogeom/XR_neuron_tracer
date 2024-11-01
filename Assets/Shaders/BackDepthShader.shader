Shader "Volume Rendering/Render Back Depth"
{
	SubShader
	{
			Tags{"RenderType"="Volume"}
			
			Pass
			{
			Cull Front
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma vertex vert
			#pragma fragment frag

			struct v2f
			{
				float4 pos : SV_POSITION;
				float3 localPos : TEXCOORD0;
			};

			v2f vert(appdata_base v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				//o.localPos = (v.vertex.xyz + 1)*0.5;
				o.localPos = (v.vertex.xyz+0.5);
				return o;
			}
			
			float4 frag(v2f i) : SV_Target
			{
				return float4(i.localPos, 1);
			}
			ENDCG
			}
	}
}