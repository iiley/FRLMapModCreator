// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)


//Modified to support toon lighting.

Shader "FR Legend/Hard Edge Unlit" {
Properties {
    _Color ("Main Color", Color) = (1, 1, 1, 1)
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
	_Ramp ("Ramp Texture", 2D) = "white" {}
    _Cutoff ("Base Alpha cutoff", Range (0,.9)) = .5
}

SubShader {
    // Tags { "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }
	Tags { "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True"}
    // Lighting off

    // Render both front and back facing polygons.
    Cull Off

    // first pass:
    //   render any pixels that are more than [_Cutoff] opaque
    Pass {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			#include "UnityShaderVariables.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
				float3 normal : NORMAL;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
			sampler2D _Ramp;
            fixed _Cutoff;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
				o.worldNormal  = UnityObjectToWorldNormal(v.normal);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 _Color;
            fixed4 frag (v2f i) : SV_Target
            {
				fixed3 worldNormal = normalize(i.worldNormal);
				fixed3 worldLightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                fixed4 col = _Color * tex2D(_MainTex, i.texcoord);
                clip(col.a - _Cutoff);

				fixed3 albedo = col.rgb;
				fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * albedo * 2.0;

				UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
				fixed diff =  dot(worldNormal, worldLightDir);
				diff = (diff * 0.5 + 0.5) * atten;
				
				fixed3 diffuse = _LightColor0.rgb * albedo * tex2D(_Ramp, float2(diff, diff)).rgb * 1.0;
				col.rgb = diffuse + ambient;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
        ENDCG
    }

}

}
