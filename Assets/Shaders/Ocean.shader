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

		_NormalMap("Normal Map", 2D) = "" {}
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

		// Initialized via script
		static const float WAVES_CAPACITY = 8;
		int _WavesLength = 0;
		float4 _WavesData[WAVES_CAPACITY]; // steepness, amplitude, frequency, speed
		float4 _WavesDirection[WAVES_CAPACITY]; // directionXYZ, phase constant

		sampler2D _NormalMap;

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
			}

			if (_WavesLength == 0)
			{
				pos = v.vertex;
				normal = v.normal;
			}

			float4 normalUv = 0.05 * float4(xz.x, 0, xz.z, 0);
			float3 normalMap = tex2Dlod(_NormalMap, normalUv);
			pos.y += normalMap.x;

			v.vertex = pos;
			v.normal = normal;

			UNITY_INITIALIZE_OUTPUT(Input, toSurf);
			toSurf.originalPos = xz;
			toSurf.worldRefl = worldRefl;
		}

		void surf(Input i, inout SurfaceOutput o)
		{
			//o.Albedo = tex2D(_NormalMap, float2(i.worldPos.x, i.worldPos.z));
			//o.Albedo = _BaseColor;

			//float2 normalUv = float2(0.25 * i.originalPos.x + _Time[1] * 0.05, 0.25 * i.originalPos.z - _Time[1] * 0.02);
			float2 normalUv = 0.25 * i.originalPos.xz;
			normalUv.x += i.worldPos.y * 0.2;
			
			float3 geomNormal = WorldNormalVector(i, o.Normal);
			float3 normalMap = tex2D(_NormalMap, normalUv) - 0*float3(0.5, 0, 0.5);
			float3 normal = geomNormal + normalMap * 0.0; // note: for some reason normalizing this makes it weird
			o.Normal = normal;

			half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, i.worldRefl);
			half3 skyColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

			o.Albedo = (skyColor * 1 + _BaseColor * 2) / 3;
		}

		half4 LightingSubsurf(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
		{
			float delta = 0.8; // can be anywhere between 0 - 1

			/*float3 normalMap = tex2D(_NormalMap, float2(i.worldPos.x, i.worldPos.z));
			float3 normal = normalize(s.Normal + normalMap * 0.25);*/
			float3 normal = s.Normal;

			// lambert lighting
			half NdotL = dot(normal, lightDir);

			// specular lighting
			float specularity = 256.0;
			float3 H = normalize(lightDir + viewDir);
			float specularIntensity = pow(saturate(dot(normal, H)), specularity) * 0.2;

			// subsurface lighting
			half VdotL = dot(viewDir, -(lightDir + normal * delta));
			half IBack = pow(saturate(VdotL), 2) * 1.0;

			half4 c;
			c.rgb = s.Albedo * _LightColor0.rgb * (NdotL * atten) +
				_LightColor0.rgb * specularIntensity * atten +
				s.Albedo * _LightColor0.rgb * atten * IBack;
			c.a = s.Alpha;

			return c * 0.75;

//			return 0;
		}

		// This is UNUSED right now
		fixed4 frag(v2f i) : SV_TARGET
		{
			half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, i.worldRefl);
			half3 skyColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

			fixed4 col = 0;
			col.rgb = (skyColor * 3+ _BaseColor * 0) / 3;

			return col;
		}
		ENDCG
		//}
	}
	FallBack "Diffuse"
}
