
Shader "FR Legend/SkidMarks" {
Properties {
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
}

SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
    Lighting off
	Offset -5, -5	//避免深度和地面太接近引起的闪烁
    Cull Back

    Pass {
        ZWrite off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                return col;
            }
        ENDCG
    }
}

}
