//FR Legend outline

#ifndef FR_OUTLINE
#define FR_OUTLINE

			#pragma multi_compile_fog

			float _Outline;
			fixed4 _OutlineColor;
			
			struct a2v {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			}; 
			
			struct v2f {
			    float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
			};
			
			v2f vert (a2v v) {
				v2f o;
				
				float4 pos = mul(UNITY_MATRIX_MV, v.vertex); 
				float3 normal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);  
				normal.z = -0.5;
				pos = pos + float4(normalize(normal), 0) * _Outline;
				o.pos = mul(UNITY_MATRIX_P, pos);
				UNITY_TRANSFER_FOG(o, o.pos);
				
				return o;
			}
			
			float4 frag(v2f i) : SV_Target { 
				fixed4 col = float4(_OutlineColor.rgb, 1.0);
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col; 
			}

#endif // FR_OUTLINE