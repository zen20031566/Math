Shader "Basics/Cloud2"
{
    Properties
    {
        _DayColor("Day Color", Color) = (1, 1, 1, 1)
        _DayShadowColor("Day Shadow Color", Color) = (0.5, 0.5, 0.7, 1)
        _NightColor("Night Color", Color) = (1, 1, 1, 1)
        _NightShadowColor("Night Shadow Color", Color) = (0.5, 0.5, 0.7, 1)
        [NoScaleOffset] _BotCloudTexture("Bottom Cloud Texture", 2D) = "white" {}
        [NoScaleOffset] _MidCloudTexture("Mid Cloud Texture", 2D) = "white" {}
        [NoScaleOffset] _TopCloudTexture("Top Cloud Texture", 2D) = "white" {}
        _FogFactor("Fog Factor", Float) = 0.5
        _RimRadius("Rim Radius", Float) = 0.3
        [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _NoiseTexture("Noise Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline" 
			"RenderType" = "Transparent" 
			"Queue" = "Transparent" 
        }
        
        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CULL OFF
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _DayColor;
            float4 _DayShadowColor;
            float4 _NightColor;
            float4 _NightShadowColor;
            float _FogFactor;
            float _RimRadius;
            float4 _RimColor;
            CBUFFER_END
            
            TEXTURE2D(_BotCloudTexture);
            TEXTURE2D(_MidCloudTexture);
            TEXTURE2D(_TopCloudTexture);
            SAMPLER(sampler_linear_repeat);
            
            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float3 viewWS : TEXCOORD4;
                float3 normalWS : TEXCOORD5;
                float4 tangentWS : TEXCOORD6;
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.uv = v.uv;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
                o.viewWS = GetWorldSpaceViewDir(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                
                return o;
            }
            
            float4 frag(v2f i) : SV_TARGET
            {
                float3 viewWS = normalize(i.viewWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
                float3 bitangentWS = cross(normalWS.xyz, i.tangentWS.xyz) * i.tangentWS.w * unity_WorldTransformParams.w;
                float3x3 TBN = float3x3(i.tangentWS.xyz, bitangentWS.xyz, normalWS.xyz); //TBN matrix
                
                float3 tangentLightDir = normalize(mul(TBN, mainLight.direction)); 
                
                float4 botCloudTexture = SAMPLE_TEXTURE2D(_BotCloudTexture, sampler_linear_repeat, i.uv); 
                float4 midCloudTexture = SAMPLE_TEXTURE2D(_MidCloudTexture, sampler_linear_repeat, i.uv1); 
                float4 topCloudTexture = SAMPLE_TEXTURE2D(_TopCloudTexture, sampler_linear_repeat, i.uv2); 
                
                //Color
                float dayNightCycle = smoothstep(-0.3, 0.25, mainLight.direction.y); //1 for day 0 for night
                float4 baseColor =lerp(_NightColor, _DayColor, dayNightCycle);
                float4 shadowColor = lerp(_NightShadowColor, _DayShadowColor, dayNightCycle);
                
                float3 botCloudColor = lerp(shadowColor, baseColor, botCloudTexture.r);
                float3 midCloudColor = lerp(shadowColor, baseColor, midCloudTexture.r);
                float3 topCloudColor = lerp(shadowColor, baseColor, topCloudTexture.r);
                
                float3 finalCloudColor = botCloudColor;
                finalCloudColor = lerp(finalCloudColor, midCloudColor, midCloudTexture.a);
                finalCloudColor = lerp(finalCloudColor, topCloudColor, topCloudTexture.a);
                
                float finalAlpha = max(botCloudTexture.a, midCloudTexture.a);
                finalAlpha = saturate(max(finalAlpha, topCloudTexture.a));
                finalAlpha = (finalAlpha >= 0.95) ? 1.0 : finalAlpha;
                
                //Rim
                float finalRimMask = botCloudTexture.g;
                finalRimMask = lerp(finalRimMask, midCloudTexture.g, midCloudTexture.a);
                finalRimMask = lerp(finalRimMask, topCloudTexture.g, topCloudTexture.a);
                
                float NdotL = dot(viewWS, -mainLight.direction);
                float rimMask = smoothstep(_RimRadius, 1, NdotL);
                float3 finalRimColor = rimMask * finalRimMask * _RimColor;
                    
                float3 finalColor = finalRimColor + finalCloudColor.r;
                
                finalColor = MixFog(finalColor,  _FogFactor);
                
                return float4(finalColor, finalAlpha);
            }

            ENDHLSL
        }
    }
}