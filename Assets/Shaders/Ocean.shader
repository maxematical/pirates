// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Ocean"
{
	Properties
	{
		_FresnelStart("Fresnel Start", Range(0,5)) = 0.0
		_FresnelCoeff("Fresnel Factor", Range(0,5)) = 1.0

		_BaseColor("Base Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_EdgeColor("Edge Color", Color) = (1.0, 1.0, 1.0, 1.0)

		_FoamAngleMin("Foam Angle Min", float) = 4 // in degrees
		_FoamAngleMax("Foam Angle Max", float) = 6
	}
	SubShader
	{
		Pass {
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			// Physically based Standard lighting model, and enable shadows on all light types
			//#pragma surface surf Standard fullforwardshadows
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			// Use shader model 3.0 target, to get nicer looking lighting
			//#pragma target 3.0

			//sampler2D _MainTex;

			/*struct Input
			{
				float2 uv_MainTex;
				float3 viewDir;
				float3 worldNormal;
			};*/

			struct v2f
			{
				float4 pos : SV_POSITION;
				half3 worldRefl : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float fresnel : TEXCOORD2;
				float4 worldPos : TEXCOORD3;
			};

			static const float pi = 3.141592653589793238462;
			static const float deg2Rad = 3.141592653589793238462 / 180.0;
			static const float one = 1.0;

			/*half _Glossiness;
			half _Metallic;
			fixed4 _Color;*/
			float _FresnelStart;
			float _FresnelCoeff;

			float4 _BaseColor;
			float4 _EdgeColor;

			float _FoamAngleMin; // in degrees
			float _FoamAngleMax; // in degrees

			//Add instancing support for this shader. You need to check Enable Instancing on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
				// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)

			//     void surf (Input IN, inout SurfaceOutputStandard o)
			//     {
			//         //o.Albedo = saturate(1 - 1 + dot(IN.worldNormal, IN.viewDir));
					//o.Albedo = IN.worldNormal;
			//     }

			v2f vert(float4 vertex : POSITION, float3 normal : NORMAL)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(vertex);
				// compute world space position of the vertex
				float3 worldPos = mul(unity_ObjectToWorld, vertex).xyz;
				// compute world space view direction
				float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
				// world space normal
				float3 worldNormal = UnityObjectToWorldNormal(normal);
				// world space reflection vector
				o.worldRefl = reflect(-worldViewDir, worldNormal);
				
				o.normal = worldNormal;
				//o.fresnel = dot(normalize(ObjSpaceViewDir(vertex)), normal);
				o.fresnel = dot(normalize(ObjSpaceViewDir(vertex)), normal);
				o.worldPos = vertex;
				return o;
			}

			fixed4 frag(v2f i) : SV_TARGET
			{
				//float3 viewDir = normalize(UnityWorldSpaceViewDir(i.pos));
				float3 viewDir = normalize(ObjSpaceViewDir(i.pos));

				float foam = i.normal.y;
				float foamMin = sin((90.0 - _FoamAngleMin) * deg2Rad);
				float foamMax = sin((90.0 - _FoamAngleMax) * deg2Rad);
				//if (foam >= foamMin && foam < foamMax)
				{
					//return fixed4(1.0, 1.0, 1.0, 1.0);
				}
				//float foamFactor = saturate(foamMin + (foam - foamMin) / (foamMax - foamMin));
				float foamFactor = saturate((foam - foamMin) / (foamMax - foamMin));
				foamFactor *= foamFactor;
				foamFactor *= foamFactor;

				foamFactor += saturate(i.worldPos.y * 2.0 - 0.5);
				//return fixed4(sin(0.5 * one), 0.0, 0.0, 1.0);

				//float fresnel = saturate(1.0 - dot(UnityObjectToWorldNormal(i.normal), viewDir));
				float fresnel = saturate(_FresnelStart - _FresnelCoeff * i.fresnel);
				float4 waterColor = lerp(_BaseColor, _EdgeColor, fresnel);

				float4 foamColor = fixed4(1.0, 1.0, 1.0, 1.0);
				float4 waterAndFoam = lerp(waterColor, foamColor, foamFactor);

				return waterAndFoam;
				//return fixed4(fresnel, fresnel, fresnel, 1.0);
				
				//float3 normal = i.normal;
				//normal.z = 0;
				//return fixed4(normal, 1.0);
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
