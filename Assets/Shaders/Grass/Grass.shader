Shader "Basics/Grass"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.25, 0.55, 0.12, 1)
        _BottomColor("Bottom Color", Color) = (0.08, 0.25, 0.04, 1)
        _HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _TipHighlightColor("Tip Highlight Color", Color) = (1, 1, 1, 1)
        _TipHighlightPower("Tip Highlight Power", Float) = 1
        _Roughness("Roughness", Float) = 1
        _SpecularFade("SpecularFade", Float) = 1
        
        _FresnelPower("Fresnel Power", Range(1.0, 20.0)) = 4.0
        _FresnelStrength("Fresnel Strength", Range(0.0, 1.0)) = 0.15
        
        _WindDirection("Wind Direction", Vector) = (1, 0 ,0)
        _WindStrength("Wind Strength", Float) = 0.5
        _WindSpeed("Wind Speed", Float) = 1
        _WindTexture("Wind Texture", 2D) = "white" {}
        _GrassBend("Grass Bend", Float) = 0.5
        
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline" 
			"RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        
        //Universal Forward
        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            
            Cull Off
            Zwrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Required: StructuredBuffer<GrassData> below needs DX11+ feature level.
            // Target 2.5 (Unity's default) doesn't support StructuredBuffer in vert/frag shaders.
            // Do not lower this unless you remove the _GrassDataBuffer usage.
            #pragma target 4.5 
            
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP // Use _CLUSTER_LIGHT_LOOP in Unity 6.1 and above.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassCommon.hlsl"
            #include "LightingCommon.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _BottomColor;
            float4 _HighlightColor;
            float4 _TipHighlightColor;
            float _TipHighlightPower;
            float _Roughness;
            float _SpecularFade;
            
            float _FresnelPower;
            float _FresnelStrength;
            CBUFFER_END
            
            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 dynamicLightmapUV : TEXCOORD2;
                float4 color : COLOR; //vertex color
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float2 dynamicLightmapUV : TEXCOORD5;
                float edgeMask : TEXCOORD6;
            };
            
            v2f vert(appdata v, uint instanceID : SV_INSTANCEID)
            {
                v2f o = (v2f)0;
                o.uv = v.uv;
                
                o.positionWS = GetGrassPosition(v.positionOS, o.uv, instanceID);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                
                o.normalWS = TransformObjectToWorldNormal(_GrassDataBuffer[instanceID].up);
                
                o.viewWS = GetWorldSpaceViewDir(o.positionWS);
                o.dynamicLightmapUV = v.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                
                o.edgeMask = v.color.r; 
                
                return o;
            }
            
            float4 frag(v2f i) : SV_TARGET
            {
                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
                float3 viewWS = normalize(i.viewWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                float4 shadowMask = SAMPLE_SHADOWMASK(i.dynamicLightmapUV);
                
                // Calculate lighting from main light.
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightColor = mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLight.color;

                float3 ambient = SampleSH(normalWS);

                float3 diffuse = saturate(dot(normalWS, mainLight.direction)) * lightColor;
                
                float distanceToCamera = distance(i.positionWS, _WorldSpaceCameraPos);
                float distanceFade = 1.0 - exp(-distanceToCamera * _SpecularFade);
                float3 specular = GGX_DistanceFade(normalWS, viewWS, mainLight.direction, _Roughness, distanceFade) * _HighlightColor * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                float3 specularTip = GGX_DistanceFade(normalWS, viewWS, mainLight.direction,_Roughness, distanceFade) * _TipHighlightColor * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                
#ifdef _ADDITIONAL_LIGHTS

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                uint lightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(lightCount)

                    Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    lightColor = light.distanceAttenuation * light.shadowAttenuation * light.color;

                    diffuse += saturate(dot(normalWS, light.direction)) * lightColor;
                    
                    float3 specular2 =  GGX_DistanceFade(normalWS, viewWS, light.direction,_Roughness, distanceFade) * lightColor;
                    float3 specularTip2 =  GGX_DistanceFade(normalWS, viewWS, light.direction,_Roughness, distanceFade) * lightColor;
                    specular += specular2;
                    specularTip += specularTip2;
                
                LIGHT_LOOP_END
#endif
                
                // Combine Base Color with lighting.
                float3 grassGradient = lerp(_BottomColor.rgb , _TopColor.rgb, i.uv.y);
                
                float3 fresnel = pow(1.0f - saturate(dot(normalWS, viewWS)), _FresnelPower) * _FresnelStrength * _TipHighlightColor;
                
                float heightMask = pow(i.uv.y, _TipHighlightPower);
                float3 highLights = (specularTip + fresnel) * heightMask;
                highLights += specular;
                highLights = smoothstep(0, 1, highLights); //this fixes HDR overexposure
                
                 float  colorNoise = SimplexNoise(i.positionWS.xz * 0.05);
                colorNoise = colorNoise * 0.5 + 0.5;
                grassGradient *= lerp(0.88, 1, colorNoise);
                float3 finalColor = (ambient + diffuse) * grassGradient + highLights;
      
                return float4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
