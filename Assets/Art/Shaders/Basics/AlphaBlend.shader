Shader "Basics/AlphaBlend" //In shader tab in materials this will show up so FileName/ShaderName
{
	Properties //your uniforms
	{
		_BaseColor("Base Color", Color) = (1, 1, 1, 1)
		_BaseTexture("Base Texture", 2D) = "white" {} //bump for normal mapping
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Source Blend Mode", Integer) = 5
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Destination Blend Mode", Integer) = 10
	}
	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline" //restrict to pipeline
			"RenderType" = "Transparent" //opaque or transparent
			"Queue" = "Transparent" //when in render loop obj gets rendererd
		}

		Pass //draw entire obj once to the screen //HLSL written here
		{
			Blend [_SrcBlend] [_DstBlend]
			

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
	}
}