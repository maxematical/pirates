// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

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

		_EmissionStart("Emission Start", float) = 0
		_EmissionFactor("Emission Factor", float) = 0
		_EmissionColor("Emission Color", Color) = (0.0, 0.0, 0.0, 1.0)
	}
	SubShader
	{
		//Pass {
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry+501" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Subsurf vertex:vert
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

		sampler2D _NormalMap;
		sampler2D _NoiseMap;
		sampler2D _Voronoi;
		sampler2D _SeaDistortion;

		// Misc settings
		float _SubsurfDelta;
		float _SubsurfFactor;

		float _LambertFactor;

		float _SpecularPower;
		float _SpecularFactor;

		float _EmissionStart;
		float _EmissionFactor;
		float4 _EmissionColor;

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
			// Calculate reflection (this can be used for skybox reflections in the surface shader)
			float3 worldViewDir = normalize(UnityWorldSpaceViewDir(v.vertex));
			half3 worldRefl = reflect(-worldViewDir, v.normal);

			// Compute Gerstner Waves position
			float4 pos = mul(unity_ObjectToWorld, v.vertex);
			pos.y = 0;

			float3 xz = pos.xyz;
			xz.y = 0;

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
			}

			// Compute Gerstner Waves normal
			float3 normal = float3(0, 1, 0);
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
			}

			// Apply vertex offset and normal
			v.vertex = mul(unity_WorldToObject, pos);
			v.normal = normal;

			// Send additional data to surface shader
			UNITY_INITIALIZE_OUTPUT(Input, toSurf);
			toSurf.originalPos = xz;
			toSurf.worldRefl = worldRefl;
		}

		inline fixed3 colorBlend(fixed3 a, fixed3 b)
		{
			float fac = step(3.0, dot(b, b));
			return lerp(b, a, fac);
		}

		float greaterThan(float n, float atLeast, float m = 9999)
		{
			return n;
			/*if (atLeast == -1) return n;
			return n >= atLeast ? 1 : 0;*/
			return saturate(m * (n - atLeast));
		}

		void surf(Input i, inout SurfaceOutputCustom o)
		{
			float3 objectPos = mul(unity_WorldToObject, i.worldPos);

			/*float2 normalUv = 0.02 * (i.originalPos.xz + i.worldPos.xz) * 0.5;
			normalUv = normalUv.yx;
			
			float3 normalMap = tex2D(_NoiseMap, normalUv) - float3(0.5, 0, 0.5);
			normalMap.y = 0;
			normalMap = normalize(normalMap);
			o.Normal += normalMap * 0.25;*/

			/*half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, i.worldRefl);
			half3 skyColor = DecodeHDR(skyData, unity_SpecCube0_HDR);*/

			float3 base = lerp(_BaseColor, _EdgeColor, saturate(_EdgeStart + _EdgeIncrease * objectPos.y));

			float3 seaUvOffset = tex2D(_SeaDistortion, i.originalPos.xz * 0.005 + i.worldPos.xz * 0.005 + _Time[1] * 1.0 * float2(0.01, 0.02));
			
			float2 voronoiUv1 = 0.4 * (i.originalPos.xz * 0.025 * 0.25 + i.worldPos.xz * 0.025 * 0.75);
			float2 voronoiUv2 = 0.4 * (i.originalPos.xz * 0.004 + i.worldPos.xz * 0.028) + float2(0.5, 0.4) + seaUvOffset.rb * 0.05;
			float3 voronoi1 = (greaterThan(tex2D(_Voronoi, voronoiUv1), 0.45)) * 0.4 * 2.6;
			float3 voronoi2 = (greaterThan(tex2D(_Voronoi, voronoiUv2), 0.4)) * 0.4 * 2.6;

			float foamAmount = max(0, 0.3 * objectPos.y + 0.4);
			//float foamAmount = 1;
			float texturedFoamAmount = greaterThan(foamAmount * voronoi1 + foamAmount * voronoi2, 1.05) * 0.8;
			o.Albedo = lerp(base, 1.0, texturedFoamAmount);
			//o.Albedo = seaUvOffset;
			//o.Albedo = lerp(base, 1.0, voronoi2);
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

			// emission
			half emission = max(0, _EmissionStart + _EmissionFactor * (1 - s.Normal.y));

			// combine lighting
			half4 waterLight;
			waterLight.rgb = s.Albedo * _LightColor0.rgb * NdotL * atten +
				_LightColor0.rgb * specularIntensity * atten +
				s.Albedo * _LightColor0.rgb * atten * IBack +
				_EmissionColor * _LightColor0.rgb * emission;
			waterLight.a = s.Alpha;

			// mix with foam lighting
			half4 foamLight = foamLighting(s, lightDir, viewDir, atten) +
				half4(s.Albedo * _LightColor0.rgb * emission, 0.0);

			return lerp(waterLight, foamLight, s.foam);
			//return c;
		}

		ENDCG
		//}
	}
	FallBack "Diffuse"
}
