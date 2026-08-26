Shader "Basics/BasicTextures" //In shader tab in materials this will show up so FileName/ShaderName
{
	Properties //your uniforms
	{
		_BaseColor("Base Color", Color) = (1, 1, 1, 1)
		_BaseTexture("Base Texture", 2D) = "white" {} //bump for normal mapping
	}
	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline" //restrict to pipeline
			"RenderType" = "Opaque" //opaque or transparent
			"Queue" = "Geometry" //when in render loop obj gets rendererd
		}

		Pass //draw entire obj once to the screen //HLSL written here
		{
			Tags
			{
				"LightMode" = "SRPDefaultUnlit"
			}

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
			float4 _BaseColor;
			float4 _BaseTexture_ST; //scaling and translation
			CBUFFER_END

			TEXTURE2D(_BaseTexture);
			SAMPLER(sampler_BaseTexture);

			struct appdata //input to vertex shader
			{
				float4 positionOS : POSITION; //object space //vertex position data
				float2 uv : TEXCOORD0;

			};

			struct v2f //vertex to frag
			{
				float4 positionCS : SV_Position; //clip space
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata v) //vertex 
			{
				v2f o = (v2f)0; //output 0 is default value get used to doing it
				
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.uv = TRANSFORM_TEX(v.uv, _BaseTexture); //applys the scaling and offset

				return o;
			}

			float4 frag(v2f i) : SV_Target //fragment
			{
				float4 textureColor = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, i.uv);
				return textureColor * _BaseColor;
			}

			ENDHLSL
		}

		Pass
		{
			Tags
			{
				"LightMode" = "DepthOnly"
			}

			ZWrite On
			ColorMask R //only renders red

			HLSLPROGRAM
			#pragma vertex depthOnlyVert
			#pragma fragment depthOnlyFrag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata
			{
				float4 positionOS : POSITION;
			};

			struct v2f
			{
				float4 positionCS : SV_Position;
			};

			v2f depthOnlyVert(appdata v)
			{
				v2f o = (v2f)0;

				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

				return o;
			}

			float depthOnlyFrag(v2f i) : SV_Target
			{
				return i.positionCS.z;
			}

			ENDHLSL
		}

		Pass
		{
            Tags
			{
				"LightMode" = "DepthNormals"
			}

			ZWrite On

			HLSLPROGRAM

			#pragma vertex depthNormalVert
			#pragma fragment depthNormalFrag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
			};

			struct v2f
			{
				float4 positionCS : SV_Position;
				float3 normalWS : TEXCOORD0;
			};

			v2f depthNormalVert(appdata v)
			{
				v2f o = (v2f)0;

				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
				o.normalWS = NormalizeNormalPerVertex(normalWS);

				return o;
			}

			float4 depthNormalFrag(v2f i) : SV_Target
			{
				float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
				return float4(normalWS, 0.0f);
			}

			ENDHLSL
		}
	}
}