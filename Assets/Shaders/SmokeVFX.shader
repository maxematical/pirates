Shader "Unlit/SmokeVFX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        /*Pass
        {*/
            CGPROGRAM
            /*#pragma vertex vert
            #pragma fragment frag*/
			#pragma surface surf Standard
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

			struct Input
			{
				float2 uv_MainTex;
				float3 viewDir;
				float3 worldNormal;
				float3 worldPos;
				float3 originalPos;
				half3 worldRefl;
				float crest;
				INTERNAL_DATA
			};

            sampler2D _MainTex;
            float4 _MainTex_ST;

			void surf(Input i, inout SurfaceOutputStandard o)
			{
				o.Albedo = 1;
				o.Smoothness = 0;
				o.Metallic = 0;
				//o.Gloss = 0;
				//o.Specular = 0;
			}
            ENDCG
        //}
    }
}
