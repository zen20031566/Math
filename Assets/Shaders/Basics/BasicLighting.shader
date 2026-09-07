Shader "Basics/BasicLighting"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _BaseTexture("Base Texture", 2D) = "white" {}
		_NormalTexture("Normal Texture", 2D) = "bump" {}
		_NormalStrength("Normal Strength", Range(0.0, 2.0)) = 1.0
        _AmbientLighting("Ambient Lighting", Color) = (0.2, 0.2, 0.2, 1)
        _Glossiness("Glossiness", Float) = 1
		_SpecularStrength("Specular Strength", Range(0.0, 1.0)) = 1.0
        _FresnelPower("Fresnel Power", Range(0.0, 20.0)) = 4.0
        _FresnelStrength("Fresnel Strength", Range(0.0, 1.0)) = 0.15
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
                #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

                CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseTexture_ST;
				float _NormalStrength;
                float3 _AmbientLighting;
                float _Glossiness;
				float _SpecularStrength;
                float _FresnelPower;
                float _FresnelStrength;
                CBUFFER_END

                TEXTURE2D(_BaseTexture);
                SAMPLER(sampler_BaseTexture);
				
				TEXTURE2D(_NormalTexture);
				SAMPLER(sampler_NormalTexture);

                struct appdata
                {
                    float4 positionOS : POSITION;
                    float2 uv : TEXCOORD0;
                    float3 normalOS : NORMAL;
					float4 tangentOS : TANGENT;

                };

                struct v2f
                {
                    float4 positionCS : SV_Position;
                    float2 uv : TEXCOORD0;
                    float3 normalWS : TEXCOORD1;
                    float3 positionWS : TEXCOORD2;
                    float3 viewWS : TEXCOORD3;
					float4 tangentWS : TEXCOORD4;
                };

                v2f vert(appdata v)
                {
                    v2f o = (v2f)0;

                    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                    o.uv = TRANSFORM_TEX(v.uv, _BaseTexture);
                    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                    o.viewWS = GetWorldSpaceViewDir(o.positionWS);
					o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);

                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
                    float3 viewWS = normalize(i.viewWS);
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);

					//normal map sampling
					float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, i.uv), _NormalStrength);
					float3 bitangentWS = cross(normalWS, i.tangentWS.xyz) * i.tangentWS.w * unity_WorldTransformParams.w;

					//tbn
					normalWS = normalize(
						normalTS.x * i.tangentWS.xyz +
						normalTS.y * bitangentWS +
						normalTS.z * normalWS);

                    Light mainLight = GetMainLight(shadowCoord);
                    float3 mainLightColor = mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLight.color;

                    float3 ambientLighting = SampleSH(normalWS);

                    float3 diffuseLighting = saturate(dot(normalWS, mainLight.direction)) * mainLightColor;

                    float3 reflectedVector = reflect(-mainLight.direction, normalWS);
                    float3 specularLighting = pow(saturate(dot(reflectedVector, viewWS)), _Glossiness) * _SpecularStrength * mainLightColor;

                    float3 fresnelLighting = pow(1.0f - saturate(dot(normalWS, viewWS)), _FresnelPower) * _FresnelStrength;

                    float4 baseColor = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, i.uv) * _BaseColor;
                    float3 finalColor = (ambientLighting + diffuseLighting) * baseColor.rgb + specularLighting + fresnelLighting;
                    return float4(finalColor, baseColor.a);
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
            ColorMask R

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

				CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseTexture_ST;
				float _NormalStrength;
                float3 _AmbientLighting;
                float _Glossiness;
				float _SpecularStrength;
                float _FresnelPower;
                float _FresnelStrength;
                CBUFFER_END

				TEXTURE2D(_NormalTexture);
				SAMPLER(sampler_NormalTexture);

                struct appdata
                {
                    float4 positionOS : POSITION;
					float2 uv : TEXCOORD0;
                    float3 normalOS : NORMAL;
					float4 tangentOS : TANGENT;
                };

                struct v2f
                {
                    float4 positionCS : SV_Position;
					float2 uv : TEXCOORD0;
                    float3 normalWS : TEXCOORD1;
					float4 tangentWS : TEXCOORD2;
                };

                v2f depthNormalVert(appdata v)
                {
                    v2f o = (v2f)0;

                    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
					o.uv = TRANSFORM_TEX(v.uv, _BaseTexture);
                    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                    o.normalWS = NormalizeNormalPerVertex(normalWS);
					o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);

                    return o;
                }

                float4 depthNormalFrag(v2f i) : SV_Target
                {
                    float3 normalWS = NormalizeNormalPerPixel(i.normalWS);

					float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, i.uv), _NormalStrength);
					float3 bitangentWS = cross(normalWS, i.tangentWS.xyz) * i.tangentWS.w * unity_WorldTransformParams.w;

					normalWS = normalize(
						normalTS.x * i.tangentWS.xyz +
						normalTS.y * bitangentWS +
						normalTS.z * normalWS);

                    return float4(normalWS, 0.0f);
                }

            ENDHLSL
        }
    }
}