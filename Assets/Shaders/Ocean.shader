﻿// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Ocean"
{
	Properties
	{
		_FresnelStart("Fresnel Start", Range(0,5)) = 0.0
		_FresnelCoeff("Fresnel Factor", Range(0,5)) = 1.0

		_BaseColor("Base Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_EdgeColor("Edge Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_EdgeStart("Edge Bias", float) = -1.7
		_EdgeIncrease("Edge Increase", float) = 2

		_FoamAngleMin("Foam Angle Min", float) = 4 // in degrees
		_FoamAngleMax("Foam Angle Max", float) = 6

		_NormalMap("Normal Map", 2D) = "" {}
		_NoiseMap("Noise", 2D) = "" {}
		_Voronoi("Voronoi", 2D) = "" {}
		_SeaDistortion("Sea Distortion Noise", 2D) = "" {}

		_SubsurfDelta("Subsurface Scattering Delta", float) = 1
		_SubsurfFactor("Subsurface Scattering Factor", float) = 2
		_LambertFactor("Lambert Factor", float) = 0.45
		_SpecularPower("Specular Power", float) = 192
		_SpecularFactor("Specular Factor", float) = 0.4
		_EmissionFactor("Emission Factor", float) = 0
	}
	SubShader
	{
		//Pass {
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		//#pragma surface surf Standard fullforwardshadows
		//#pragma vertex vert
		//#pragma fragment frag
		#pragma surface surf Subsurf vertex:vert addshadow
		#include "UnityCG.cginc"

		// Use shader model 3.0 target, to get nicer looking lighting
		//#pragma target 3.0

		//sampler2D _MainTex;

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

		struct v2f
		{
			float4 pos : SV_POSITION;
			half3 worldRefl : TEXCOORD0;
			float3 normal : TEXCOORD1;
			float fresnel : TEXCOORD2;
			float4 worldPos : TEXCOORD3;
		};

		struct SurfaceOutputCustom
		{
			float3 Albedo;
			float3 Normal;
			float Alpha;
			float Emission;

			float4 foam;
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
		float _EdgeStart;
		float _EdgeIncrease;

		float _FoamAngleMin; // in degrees
		float _FoamAngleMax; // in degrees

		// Initialized via script
		static const float WAVES_CAPACITY = 8;
		int _WavesLength = 0;
		float4 _WavesData[WAVES_CAPACITY]; // steepness, amplitude, frequency, speed
		float4 _WavesDirection[WAVES_CAPACITY]; // directionXYZ, phase constant
		float4 _OceanPosition; // XYZ position of the ocean GameObject

		sampler2D _NormalMap;
		sampler2D _NoiseMap;
		sampler2D _Voronoi;
		sampler2D _SeaDistortion;

		// Misc settings
		float _SubsurfDelta; // =1
		float _SubsurfFactor; // =0.75

		float _LambertFactor = 0.45;

		float _SpecularPower; // 192
		float _SpecularFactor; // 0.4

		float _EmissionFactor;

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

		void vert(inout appdata_full v, out Input toSurf)
		{
			//v2f o;
			//o.pos = UnityObjectToClipPos(vertex);
			//float3 worldPos = mul(unity_ObjectToWorld, vertex).xyz;
			//float3 worldNormal = UnityObjectToWorldNormal(normal);
			float3 worldViewDir = normalize(UnityWorldSpaceViewDir(v.vertex));
			//
			//o.normal = worldNormal;
			////o.fresnel = dot(normalize(ObjSpaceViewDir(vertex)), normal);
			//o.fresnel = dot(normalize(ObjSpaceViewDir(vertex)), normal);
			//o.worldPos = vertex;
			half3 worldRefl = reflect(-worldViewDir, v.normal);
			//return o;

			float3 xz = v.vertex.xyz;
			xz.y = 0;

			float crest = 0;

			float4 pos = v.vertex;
			pos.y = 0;
			float3 normal = float3(0, 1, 0);
			for (int i = 0; i < _WavesLength; i++)
			{
				float4 data = _WavesData[i];
				float4 direction = _WavesDirection[i];
				float steepness = data[0];
				float amplitude = data[1];
				float frequency = data[2];
				float speed = data[3];
				float phaseConstant = direction[3];
				float time = _Time[1];

				pos.x += steepness * amplitude * direction.x * cos(dot(frequency * direction, xz) + phaseConstant * time);
				pos.z += steepness * amplitude * direction.z * cos(dot(frequency * direction, xz) + phaseConstant * time);
				pos.y += amplitude * sin(dot(frequency * direction, xz) + phaseConstant * time);

				crest += (i == 0) * (abs(pos.x - v.vertex.x) + abs(pos.z - v.vertex.z)) * (pos.y - v.vertex.y) / amplitude;
				//crest += (i == 0) * sin(dot(frequency * direction, xz) + phaseConstant * time);
				//crest -= (i == 1) * 0.5 * sin(dot(frequency * direction, xz) + phaseConstant * time);
			}

			for (i = 0; i < _WavesLength; i++)
			{
				float4 data = _WavesData[i];
				float4 direction = _WavesDirection[i];
				float steepness = data[0];
				float amplitude = data[1];
				float frequency = data[2];
				float speed = data[3];
				float phaseConstant = direction[3];
				float time = _Time[1];

				float s = sin(dot(frequency * direction, pos) + phaseConstant * time);
				float c = cos(dot(frequency * direction, pos) + phaseConstant * time);
				float wa = frequency * amplitude;

				normal.x -= direction.x * wa * c;
				normal.z -= direction.z * wa * c;
				normal.y -= steepness * wa * s;

				if (i == 0) crest = 1 - normal.y;
			}
			float normalY = normalize(normal).y;
			crest = saturate(pow(10 * (1 - normalY), 1)) * 2;

			float4 normalUv = 0.05 * float4(xz.x, 0, xz.z, 0);
			float3 normalMap = tex2Dlod(_NormalMap, normalUv);
			//pos.y += normalMap.x;

			// TODO find some way to do this without the if statement
			// maybe lerp(pos, v.vertex, saturate(_WaveLength)) ?
			if (_WavesLength == 0)
			{
				pos = v.vertex;
				normal = v.normal;
			}

			v.vertex = pos;
			v.normal = normal;

			UNITY_INITIALIZE_OUTPUT(Input, toSurf);
			toSurf.originalPos = xz;
			toSurf.worldRefl = worldRefl;
			toSurf.crest = saturate(crest);
		}

		inline fixed3 colorBlend(fixed3 a, fixed3 b)
		{
			float fac = step(3.0, dot(b, b));
			return lerp(b, a, fac);
		}

		float greaterThan(float n, float atLeast, float m = 9999)
		{
			/*if (atLeast == -1) return n;
			return n >= atLeast ? 1 : 0;*/
			return saturate(m * (n - atLeast));
		}

		void surf(Input i, inout SurfaceOutputCustom o)
		{
			float3 objectPos = i.worldPos - _OceanPosition;

			float2 normalUv = 0.02 * (i.originalPos.xz + i.worldPos.xz) * 0.5;
			normalUv = normalUv.yx;
			
			float3 normalMap = tex2D(_NoiseMap, normalUv) - float3(0.5, 0, 0.5);
			normalMap.y = 0;
			normalMap = normalize(normalMap);
			//o.Normal += normalMap * 0.25;

			/*half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, i.worldRefl);
			half3 skyColor = DecodeHDR(skyData, unity_SpecCube0_HDR);*/

			float3 base = lerp(_BaseColor, _EdgeColor, saturate(_EdgeStart + _EdgeIncrease * objectPos.y));

			float3 seaUvOffset = tex2D(_SeaDistortion, i.originalPos.xz * 0.01 + i.worldPos.xz * 0.01 + _Time[1] * 0.4 * float2(0.01, 0.02));
			
			float2 voronoiUv1 = (i.originalPos.xz * 0.025 * 0.25 + i.worldPos.xz * 0.025 * 0.75);
			float2 voronoiUv2 = (i.originalPos.xz * 0.004 + i.worldPos.xz * 0.028) + float2(0.5, 0.4) + seaUvOffset.rb * 0.075;
			float3 voronoi1 = (greaterThan(tex2D(_Voronoi, voronoiUv1), 0.45)) * 0.4 * 2.6;
			float3 voronoi2 = (greaterThan(tex2D(_Voronoi, voronoiUv2), 0.4)) * 0.4 * 2 * 0.2;

			float foamAmount = max(0, 0.2 * objectPos.y + 1);
			//float foamAmount = 1;
			float texturedFoamAmount = greaterThan(foamAmount * voronoi1 + foamAmount * voronoi2, 1.05) * 0.8;
			o.Albedo = lerp(base, 1.0, texturedFoamAmount);
			//o.Albedo = voronoi1;
			//o.Albedo = voronoi2;
			//o.Albedo = foamAmount;
			o.foam = texturedFoamAmount;
		}

		half4 foamLighting(SurfaceOutputCustom s, half3 lightDir, half3 viewDir, half atten)
		{
			float3 normal = s.Normal;

			// Lambert lighting
			half NdotL = saturate(dot(normal, lightDir)) * 0.5;

			// Specular lighting
			float specularity = 48.0;
			float3 H = normalize(lightDir + viewDir);
			float specularIntensity = pow(saturate(dot(H, normal)), specularity) * 0.45;

			// Combine and return result
			half4 c;
			c.rgb = s.Albedo * _LightColor0.rgb * NdotL * atten +
				_LightColor0.rgb * specularIntensity * atten;
			c.a = s.Alpha;

			return c;
		}

		half4 LightingSubsurf(SurfaceOutputCustom s, half3 lightDir, half3 viewDir, half atten)
		{
			/*float3 normalMap = tex2D(_NormalMap, float2(i.worldPos.x, i.worldPos.z));
			float3 normal = normalize(s.Normal + normalMap * 0.25);*/
			float3 normal = s.Normal;

			// lambert lighting
			half NdotL = saturate(dot(normal, lightDir)) * _LambertFactor;

			// specular lighting
			float3 H = normalize(lightDir + viewDir);
			float specularIntensity = pow(saturate(dot(H, normal)), _SpecularPower) * _SpecularFactor;

			// subsurface lighting
			half VdotL = dot(viewDir, -(lightDir + normal * _SubsurfDelta));
			half IBack = pow(saturate(VdotL), 2) * _SubsurfFactor;

			// combine lighting and return result
			half4 c;
			c.rgb = s.Albedo * _LightColor0.rgb * NdotL * atten +
				_LightColor0.rgb * specularIntensity * atten +
				s.Albedo * _LightColor0.rgb * atten * IBack +
				s.Albedo * _LightColor0.rgb * _EmissionFactor;
			c.a = s.Alpha;

			return lerp(c, foamLighting(s, lightDir, viewDir, atten), s.foam);
			//return c;
		}

		ENDCG
		//}
	}
	FallBack "Diffuse"
}
