
Shader "FR Legend/Mountain Fog" {
	Properties {
		_Color ("Color Tint", Color) = (1, 1, 1, 1)
		_MainTex ("Main Tex", 2D) = "white" {}
	}
    SubShader {
		Tags { "RenderType"="Opaque" "IgnoreProjector"="True" "Queue"="Geometry"}
    	Lighting off
		
		Pass {
			Tags { "LightMode"="ForwardBase" }
			
			Cull Back
		
			CGPROGRAM
		
			#pragma vertex vert
			#pragma fragment frag
            #pragma multi_compile_fog
			
			#pragma multi_compile_fwdbase nolightmap
		
			#include "UnityCG.cginc"
			// #include "Lighting.cginc"
			// #include "AutoLight.cginc"
			// #include "UnityShaderVariables.cginc"
			
			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
		
			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
			}; 
		
			struct v2f {
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
			};
			
			v2f vert (a2v v) {
				v2f o;
				o.pos = UnityObjectToClipPos( v.vertex);
				o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
				
				return o;
			}
			
			float4 frag(v2f i) : SV_Target { 
				fixed4 col = tex2D (_MainTex, i.uv) * _Color;
				col.rgb = UNITY_LIGHTMODEL_AMBIENT.xyz * col.rgb * 2.0;
                UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
		
			ENDCG
		}
	}
	FallBack "Diffuse"
}