// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Volume Rendering/NewImageEffectShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LumiPathTex1 ("LumiPath1", 2D) = "white" {}
        _LumiPathTex2 ("LumiPath2", 2D) = "white" {}
        _PointsTex("PointsTex",Rect) = "white"{}
        _FrontDepth("FrontDepth",Rect) = "white"{}
        _BackDepth("BackDepth",Rect) = "white"{}
        _EntryPointNum("EntryPointNum", int) = 0
        _ExitPointNum("ExitPointNum", int) = 0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
            //#pragma exclude_renderers gles
            #pragma enable_d3d11_debug_symbols
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_nicest
            

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler1D _PointsTex;
            sampler2D _FrontDepth;
            sampler2D _BackDepth;
            sampler2D_float _LumiPathTex1;
            sampler2D_float _LumiPathTex2;
            int _EntryPointNum;
            int _ExitPointNum;

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            //float4x2 inverseSF(float3 p, float n) {
            //    float phi = min(atan2(p.y, p.x), UNITY_PI);
            //    float cosTheta = p.z;
            //    float k = max(2, floor(
            //    log(n * UNITY_PI * sqrt(5) * (1 - cosTheta*cosTheta))/ log(PHI+1.0f)));
            //    float Fk = pow(PHI, k)/sqrt(5);
            //    float F0 = round(Fk);
            //    float F1 = round(Fk * PHI);
            //    float2x2 B = float2x2(
            //    2*UNITY_PI*madfrac(F0+1, PHI-1) - 2*UNITY_PI*(PHI-1),
            //    2*UNITY_PI*madfrac(F1+1, PHI-1) - 2*UNITY_PI*(PHI-1),
            //    -2*F0/n,
            //    -2*F1/n); 
            //    //float2x2 invB = (1/(B[0][0]*B[1][1]-B[0][1]*B[1][0])*float2x2(
            //    //B[1][1],-B[0][1],-B[1][0],B[0][0]));
            //    float det = (B[0][0] * B[1][1] - B[0][1] * B[1][0]);
            //    float2x2 invB;
	           // invB[0][0] = B[1][1] / det;
	           // invB[0][1] = -B[0][1] / det;
	           // invB[1][0] = -B[1][0] / det;
	           // invB[1][1] = B[0][0] / det;
            //    float2 c = floor(mul(invB, float2(phi, cosTheta - (1-1/n))));
            //    float d = INFINITY, j = 0;

            //    float4x2 result;
            //    for (uint s = 0; s < 4; ++s) {
            //        float cosTheta = dot(B[1], float2(s/2, s%2) + c) + (1-1/n);
            //        cosTheta = clamp(cosTheta, -1, +1)*2 - cosTheta;
            //        float i = floor(n*0.5 - cosTheta*n*0.5);
            //        float phi = 2*UNITY_PI*madfrac(i, PHI-1);
            //        cosTheta = 1 - (2*i + 1)*rcp(n);
            //        float sinTheta = sqrt(1 - cosTheta*cosTheta);
            //        float3 q = float3(cos(phi)*sinTheta, sin(phi)*sinTheta,cosTheta);
            //        float squaredDistance = dot(q-p,q-p);
            //        //if (squaredDistance < d) {
            //        //    d = squaredDistance;
            //        //    j = i;
            //        //}
            //        result[s][0]=i;
            //        result[s][1]=squaredDistance;
            //    }
            //    return result;
            //}


            //#define UNITY_PI 3.1415926535897932384626433832795028841971693993751058209749445923
            #define PHI (sqrt(5)*0.5 + 0.5)
            #define INFINITY 1024.0

            #define madfrac(A,B) mad((A), (B), -floor((A)*(B)))

            float2x2 inverse(float2x2 A) {
	            return float2x2(A[1][1], -A[0][1], -A[1][0], A[0][0]) / determinant(A);
            }

            float inverseSF2(float3 p, float n) {
                
	            float phi = min(atan2(p.y, p.x), UNITY_PI), cosTheta = p.z;

	            float k = max(2, floor(
		            log(n * UNITY_PI * sqrt(5) * (1 - cosTheta*cosTheta))
			        / log(PHI*PHI)));
    
	            float Fk = pow(PHI, k)/sqrt(5);
	            float F0 = round(Fk), F1 = round(Fk * PHI);

	            float2x2 B = float2x2(
		            2*UNITY_PI*madfrac(F0+1, PHI-1) - 2*UNITY_PI*(PHI-1),
		            2*UNITY_PI*madfrac(F1+1, PHI-1) - 2*UNITY_PI*(PHI-1),
		            -2*F0/n,
		            -2*F1/n);
	            float2x2 invB = inverse(B);
	            float2 c = floor(
		            mul(invB, float2(phi, cosTheta - (1-1/n))));

	            float d = INFINITY, j = 0;
	            for (uint s = 0; s < 4; ++s) {
		            float cosTheta =
			            dot(B[1], float2(s%2, s/2) + c) + (1-1/n);
		                cosTheta = clamp(cosTheta, -1, +1)*2 - cosTheta;

		            float i = floor(n*0.5 - cosTheta*n*0.5);
		            float phi = 2*UNITY_PI*madfrac(i, PHI-1);
		            cosTheta = 1 - (2*i + 1)*rcp(n);
		            float sinTheta = sqrt(1 - cosTheta*cosTheta);

		            float3 q = float3(
			            cos(phi)*sinTheta,
			            sin(phi)*sinTheta,
			            cosTheta);

		            float squaredDistance = dot(q-p, q-p);
		            if (squaredDistance < d) {
			            d = squaredDistance;
			            j = i;
		            }
	            }

	            return j;
            }

            float inverseSF(float3 p,float kNum){
                const float kTau = 6.28318530718;
                const float kPhi = (1.0+sqrt(5.0))/2.0;
                //const float kNum = 5000.0;

                float k  = max(2.0, floor(log(kNum*kTau*0.5*sqrt(5.0)*(1.0-p.z*p.z))/log(kPhi+1.0)));
                float Fk = pow(kPhi, k)/sqrt(5.0);
                float2  F  = float2(round(Fk), round(Fk*kPhi)); // |Fk|, |Fk+1|
    
                
                float2  kb = kTau*(frac((F+1.0)*kPhi)-(kPhi-1.0)); 
                float2  ka = 2.0*F/kNum;

                float2x2 iB = float2x2( ka.y, kb.y, -ka.x, -kb.x  ) / (ka.y*kb.x - ka.x*kb.y);
                float2 c = floor(mul(iB,float2(atan2(p.y,p.x),p.z-1.0+1.0/kNum)));

                float d = 1024.0;
                float j = 0.0;
                for( int s=0; s<4; s++ ) 
                {
                    float2  uv = float2((s-2*(s/2)),(s/2));
                    float id = clamp(dot(F, uv+c),0.0,kNum-1.0); // all quantities are integers
        
                    float phi      = kTau*frac(id*kPhi);
                    float cosTheta = 1.0 - (2.0*id+1.0)/kNum;
                    float sinTheta = sqrt(1.0-cosTheta*cosTheta);
        
                    float3 q = float3( cos(phi)*sinTheta, sin(phi)*sinTheta, cosTheta );
                    float tmp = dot(q-p, q-p);
                    if( tmp<d ) 
                    {
                        d = tmp;
                        j = id;
                     }
                }
                return  j;
            }

            float3 SF(float i, float n) {
	            float phi = 2*UNITY_PI*madfrac(i, PHI-1);
	            float cosTheta = 1 - (2*i + 1)*rcp(n);
	            float sinTheta = sqrt(saturate(1 - cosTheta*cosTheta));
	            return float3(
		            cos(phi)*sinTheta,
		            sin(phi)*sinTheta,
		            cosTheta);
            }

            v2f vert (appdata_img v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float hash1( float n ) { return frac(sin(n)*158.5453123); }

            float4 frag (v2f i):SV_Target
            {
                float4 frontPos = (tex2D(_FrontDepth, i.uv)-0.5)*2;
                float4 backPos = (tex2D(_BackDepth, i.uv)-0.5)*2;
                if(distance(frontPos,backPos)<0.0001f) return tex2D(_MainTex,i.uv);

                float indexFront = inverseSF2(frontPos.xyz,_EntryPointNum);
                float indexBack = inverseSF2(backPos.xyz,_ExitPointNum);
                float col;
                //if(indexBack<_ExitPointNum/2){
                //    col = tex2D(_LumiPathTex1,float2((indexFront+0.5)/(_EntryPointNum),(indexBack+0.5)/(_ExitPointNum/2))).x;
                //}else{
                //    col = tex2D(_LumiPathTex1,float2((indexFront+0.5)/(_EntryPointNum),(indexBack-_ExitPointNum/2+0.5)/(_ExitPointNum/2))).y;
                //}
                col = tex2D(_LumiPathTex1, float2((indexFront + 0.5) / (_EntryPointNum), (indexBack + 0.5) / (_ExitPointNum))).x;
                
                //col = indexBack / _ExitPointNum;
                float4 result = float4(col.x,col.x,col.x,col.x);

                //if(result.x==0) return tex2D(_MainTex,i.uv);
                
                
                //float4 result = 0.5 + 0.5*sin( hash1(indexFront*13.0)*3.0 + 1.0 + float4(0.0,1.0,1.0,1.0));
                //float4 result =  float4(indexFront/1000,indexFront/1000, indexFront/1000, 1);
                float3 pos = SF(indexFront,_EntryPointNum);
                float dist = dot(pos-frontPos,pos-frontPos);
                //if(dist<0.00001) result = float4(1,1,1,1);
                //if(index.y<0.015) col = fixed4(1,0,0,1);
                

                return result;
                
            }
            ENDCG
        }
    }
}

//float4x2 indexFront = inverseSF(frontPos.xyz,_EntryPointNum);
                //float4x2 indexBack = inverseSF(backPos.xyz,_ExitPointNum);

                //float weightall=0;
                //for(int i=0;i<4;i++){
                //    for(int j=0;j<4;j++){
                //        weightall+=1/(indexFront[i].y*indexFront[i].y)+1/(indexBack[j].y*indexBack[j].y);
                //    }
                //}



                //float4 result = float4(0,0,0,0);
                //for(int i=0;i<4;i++){
                //    for(int j=0;j<4;j++){
                //        float weight = 1/(indexFront[i].y*indexFront[i].y)+1/(indexBack[j].y*indexBack[j].y);
                //        result += weight/weightall*tex2D(_LumiPathTex,float2((indexFront[i].x)/(_EntryPointNum-1),(indexBack[j].x)/(_ExitPointNum-1)));

                //    }
                //}
                //fixed2 col = tex2D(_LumiPathTex,float2((indexFront)/(_EntryPointNum-1),(indexBack)/(_ExitPointNum-1)));
                //float4 result = float4(col.x,col.x,col.x,col.y);
                //float4 col = float4(result.x,result.x,result.x,result.y);