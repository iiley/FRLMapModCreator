Shader "FR Legend/Car Outline" {
	Properties {
		_Color ("Color Tint", Color) = (1, 1, 1, 1)
		_MainTex ("Main Tex", 2D) = "white" {}
		_LiveryTex ("Livery Tex", 2D) = "white" {}
		_DecalTex ("Decal Tex", 2D) = "black" {}
		_ScratchTex ("Scratch Tex", 2D) = "black" {}
		_Ramp ("Ramp Texture", 2D) = "white" {}
		_Outline ("Outline", Range(0, 1)) = 0.1
		_OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
		_Specular ("Specular", Color) = (1, 1, 1, 1)
		_SpecularScale ("Specular Scale", Range(0, 0.1)) = 0.01
      	_ReflectionScale ("Reflection Scale", Range(0, 1)) = 0.4
      	_ReflectionSaturation ("Reflection Saturation", Range(0, 1)) = 0.5
	}
    SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry"}
		
		Pass {
			Tags { "LightMode"="ForwardBase" }
			
			Cull Back
		
			CGPROGRAM
		
			#pragma vertex vert
			#pragma fragment frag
			
			#pragma multi_compile_fwdbase nolightmap SHADOWS_OFF
		
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			#include "UnityShaderVariables.cginc"
			#include "FRCG.cginc"
			
			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _LiveryTex;
			float4 _LiveryTex_ST;
            sampler2D _DecalTex;
			sampler2D _ScratchTex;
			sampler2D _Ramp;
			fixed4 _Specular;
			fixed _SpecularScale;
			fixed _ReflectionScale;
			fixed _ReflectionSaturation;
		
			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
				// float4 tangent : TANGENT;
				fixed4 color : COLOR;
			}; 
		
			struct v2f {
				float4 pos : POSITION;
				fixed4 color : COLOR0;
				float2 uv : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float2 uv2 : TEXCOORD3;
				float3 normalDir : TEXCOORD4;
				float3 viewDir : TEXCOORD5;
				// SHADOW_COORDS(6)
			};
			
			v2f vert (a2v v) {
				v2f o;
				
				o.color = v.color;
				o.pos = UnityObjectToClipPos( v.vertex);
				o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
				o.uv2 = TRANSFORM_TEX (v.texcoord, _LiveryTex);
				o.worldNormal  = UnityObjectToWorldNormal(v.normal);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				
				float4x4 modelMatrix = unity_ObjectToWorld;
				float4x4 modelMatrixInverse = unity_WorldToObject; 
	
				o.viewDir = mul(modelMatrix, v.vertex).xyz - _WorldSpaceCameraPos;
				o.normalDir = normalize(mul(float4(v.normal, 0.0), modelMatrixInverse).xyz);

				// TRANSFER_SHADOW(o);
				
				return o;
			}
			
			float4 frag(v2f i) : SV_Target { 
				fixed3 worldNormal = normalize(i.worldNormal);
				fixed3 worldLightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
				fixed3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
				fixed3 worldHalfDir = normalize(worldLightDir + worldViewDir);
				
				fixed4 livery = tex2D (_LiveryTex, i.uv2);
				fixed3 albedo = _Color.rgb * livery.rgb;
                fixed4 decal = tex2D (_DecalTex, i.uv);
                albedo = (decal.rgb*decal.a + (1.0-decal.a)*albedo);
                albedo *= tex2D (_MainTex, i.uv).rgb;
				
				fixed4 scratch = tex2D (_ScratchTex, i.uv);
				scratch.a *= (1 - i.color.a);
				albedo = scratch.rgb * scratch.a + (1-scratch.a) * albedo;
				
				fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * albedo * 1.0;
				
				fixed atten = 1.0;//UNITY_SHADOW_ATTENUATION(i, i.worldPos);
				
				fixed diff =  dot(worldNormal, worldLightDir);
				diff = (diff * 0.5 + 0.5) * atten;
				
            	float3 reflectedDir = reflect(i.viewDir, normalize(i.normalDir));
            	fixed3 refc = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectedDir);
				#ifdef SHADER_API_GLES
				#else
					refc = changeSaturation(refc, _ReflectionSaturation);
				#endif
				fixed ffactor = min(_ReflectionScale, 1.0-_ReflectionScale);
				fixed downDamp = (1 + dot(worldNormal, fixed3(0, 1, 0))) * 0.5;
				fixed fresnel = _ReflectionScale + downDamp * ffactor * pow(1 - saturate(dot(worldViewDir, worldNormal)), 5);
				fresnel = fresnel * (1-scratch.a);
				
				fixed3 diffuse = _LightColor0.rgb * albedo * tex2D(_Ramp, float2(diff, diff)).rgb * 1.0;
				
				fixed spec = dot(worldNormal, worldHalfDir);
				fixed w = fwidth(spec) * 2.0;
				fixed3 specular = _Specular.rgb * lerp(0, 1, smoothstep(-w, w, spec + _SpecularScale - 1)) * step(0.0001, _SpecularScale);
				diffuse += specular * (1-scratch.a);
				diffuse = lerp(diffuse, refc, saturate(fresnel));

				diffuse = ambient + diffuse;
				
				return fixed4(diffuse, 1.0);
			}
		
			ENDCG
		}
		
		Pass {
			NAME "OUTLINE"
			
			Cull Front
			
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "Outline.cginc"
			
			ENDCG
		}
	}
	FallBack "Diffuse"
}