
#ifndef TOON_LIB
#define TOON_LIB

			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _Ramp;
			fixed4 _Specular;
			fixed _SpecularScale;
            
            struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
				// float4 tangent : TANGENT;
			}; 
		
			struct v2f {
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float3 normalDir : TEXCOORD3;
				float3 viewDir : TEXCOORD4;
				// SHADOW_COORDS(5)
			};
			
			v2f vert (a2v v) {
				v2f o;
				
				o.pos = UnityObjectToClipPos( v.vertex);
				o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
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
				
				fixed4 c = tex2D (_MainTex, i.uv) * _Color;
				fixed3 albedo = c.rgb;
				
				fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * albedo * 2.0;
				
				fixed atten = 1.0;//UNITY_SHADOW_ATTENUATION(i, i.worldPos);
				
				fixed diff =  dot(worldNormal, worldLightDir);
				diff = (diff * 0.5 + 0.5) * atten;
				
				fixed3 diffuse = _LightColor0.rgb * albedo * tex2D(_Ramp, float2(diff, diff)).rgb * 1.0;
				
				fixed spec = dot(worldNormal, worldHalfDir);
				fixed w = fwidth(spec) * 2.0;
				fixed3 specular = _Specular.rgb * lerp(0, 1, smoothstep(-w, w, spec + _SpecularScale - 1)) * step(0.0001, _SpecularScale);
				
				diffuse = ambient + diffuse + specular;
				
				return fixed4(diffuse, 1.0);
			}
            
#endif // TOON_LIB