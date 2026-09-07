Shader "Basics/Test" //In shader tab in materials this will show up so FileName/ShaderName
{
	Properties //your uniforms
	{
		_BaseColor("Base Color", Color) = (1, 1, 1, 1)
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
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			float4 _BaseColor;

			struct appdata
			{
				float4 positionOS : POSITION; //object space //vertex position data

			};

			struct v2f //vertex to frag
			{
				float4 positionCS : SV_Position; //clip space
			};

			v2f vert(appdata v) //vertex 
			{
				v2f o = (v2f)0; //output 0 is default value get used to doing it
				
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

				return o;
			}

			float4 frag(v2f i) : SV_Target //fragment
			{
				return _BaseColor;
			}

			ENDHLSL
		}
	}
}