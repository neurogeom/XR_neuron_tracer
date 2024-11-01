Shader "Volume Rendering/Cube"
{
	SubShader{
		Tags {"RenderType"="Volume"}
		ZWrite off

		Pass{
			ColorMask 0
		}
	}
}