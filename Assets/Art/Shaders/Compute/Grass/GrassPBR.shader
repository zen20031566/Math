//Shader "Basics/Grass"
//{
//    Properties
//    {
//        _TopColor("Top Color", Color) = (0.25, 0.55, 0.12, 1)
//        _BottomColor("Bottom Color", Color) = (0.08, 0.25, 0.04, 1)
//        _GradientThreshold("GradientThreshold", Float) = 1
//        _Glossiness("Glossiness", Float) = 1
//        _WindStrength("Wind Strength", Float) = 0.5
//        _WindSpeed("Wind Speed", Float) = 1
//        _FresnelPower("Fresnel Power", Range(1.0, 20.0)) = 4.0
//        _FresnelStrength("Fresnel Strength", Range(0.0, 1.0)) = 0.15
//        
//    }
//    SubShader
//    {
//        Tags
//        {
//            "RenderPipeline" = "UniversalPipeline" 
//			"RenderType" = "Opaque"
//            "Queue" = "Geometry"
//        }
//        
//        //Universal Forward
//        Pass
//        {
//            Tags
//            {
//                "LightMode" = "UniversalForward"
//            }
//            
//            Cull Off
//            Zwrite On
//            
//            HLSLPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//            #pragma target 4.5
//            #pragma multi_compile_fog
//            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
//            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
//            #pragma multi_compile_fragment _ _LIGHT_COOKIES
//            #pragma multi_compile _ _ADDITIONAL_LIGHTS
//			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
//            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP // Use _CLUSTER_LIGHT_LOOP in Unity 6.1 and above.
//
//            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
//            #include "GrassCommon.hlsl"
//            
//            CBUFFER_START(UnityPerMaterial)
//            float4 _TopColor;
//            float4 _BottomColor;
//            float _GradientThreshold;
//            float _Glossiness;
//            float _FresnelPower;
//            float _FresnelStrength;
//            CBUFFER_END
//            
//            struct appdata
//            {
//                float4 positionOS : POSITION;
//                float2 uv : TEXCOORD0;
//                float3 normalOS : NORMAL;
//                float4 tangentOS : TANGENT;
//                float2 dynamicLightmapUV : TEXCOORD2;
//                float4 color : COLOR; //vertex color
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//            };
//
//            struct v2f
//            {
//                float4 positionCS : SV_POSITION;
//                float2 uv : TEXCOORD0;
//                float3 normalWS : TEXCOORD1;
//                float3 positionWS : TEXCOORD2;
//                float3 viewWS : TEXCOORD3;
//                float2 dynamicLightmapUV : TEXCOORD5;
//                float edgeMask : TEXCOORD6;
//            };
//            
//            v2f vert(appdata v, uint instanceID : SV_INSTANCEID)
//            {
//                v2f o = (v2f)0;
//                o.uv = v.uv;
//                
//                o.positionWS = GetGrassPosition(v.positionOS, o.uv, instanceID);
//                o.positionCS = TransformWorldToHClip(o.positionWS);
//       
//                o.normalWS = normalize(_GrassDataBuffer[instanceID].up);
//                //o.normalWS = TransformObjectToWorldNormal(v.normalOS);
//                //o.normalWS = RotateAroundYInDegrees(float4(o.normalWS, 0), _GrassDataBuffer[instanceID].yawAngle);
//                
//                o.viewWS = GetWorldSpaceViewDir(o.positionWS);
//                o.dynamicLightmapUV = v.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
//                
//                o.edgeMask = v.color.r; 
//                
//                return o;
//            }
//            
//            float4 frag(v2f i) : SV_TARGET
//            {
//                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
//                
//                float3 viewWS = normalize(i.viewWS);
//                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
//                float4 shadowMask = SAMPLE_SHADOWMASK(i.dynamicLightmapUV);
//                
//                // Calculate lighting from main light.
//                Light mainLight = GetMainLight(shadowCoord);
//                float3 lightColor = mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLight.color;
//
//                float3 ambient = SampleSH(normalWS);
//
//                float3 diffuse = saturate(dot(normalWS, mainLight.direction)) * lightColor;
//                
//                //Blinn-Phong
//                // float3 halfwayDir = normalize(mainLight.direction + viewWS);
//                // float specular = saturate(dot(normalWS, halfwayDir));
//                // specular = pow(specular, _Glossiness);
//                // specular = smoothstep(0, 1, specular); //this fixes HDR overexposure
//                // specular *= i.uv.y;
//                
//                //Phong 
//                float3 reflectedVector = reflect(-mainLight.direction, normalWS);
//                float specular = saturate(dot(reflectedVector, viewWS));
//                specular = pow(specular, _Glossiness);
//                specular = smoothstep(0, 1, specular);
//                
//                // Calculate lighting from additional lights.
//#ifdef _ADDITIONAL_LIGHTS
//
//                InputData inputData = (InputData)0;
//                inputData.positionWS = i.positionWS;
//                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
//
//                uint lightCount = GetAdditionalLightsCount();
//
//                LIGHT_LOOP_BEGIN(lightCount)
//
//                    Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
//                    lightColor = light.distanceAttenuation * light.shadowAttenuation * light.color;
//
//                    diffuse += saturate(dot(normalWS, light.direction)) * lightColor;
//                    
//                    // halfwayDir = normalize(mainLight.direction + viewWS);
//                    // float specular2 = saturate(dot(normalWS, halfwayDir));
//                    // specular2 *= i.uv.y;
//                    // specular2 = pow(specular2, _Glossiness);
//                    // specular2 = smoothstep(0, 1, specular2); 
//                    // specular += specular2;
//                
//                    reflectedVector = reflect(-light.direction, normalWS);
//                    float specular2 = saturate(dot(reflectedVector, viewWS));
//                    specular2 = pow(specular2, _Glossiness);
//                    specular2 = smoothstep(0, 1, specular2);
//                    specular += specular2;
//                
//                LIGHT_LOOP_END
//#endif
//                // Combine Base Color with lighting.
//                float t = saturate(i.uv.y + _GradientThreshold);
//                float3 grassGradient = lerp(_BottomColor.rgb , _TopColor.rgb, t);
//                
//                // float colorNoise = SimplexNoise(i.positionWS.xz * 0.05) * 0.5 + 0.5;
//                // float3 finalGrassColor = lerp(grassGradient, grassGradient * 0.5, colorNoise);
//                
//                float3 viewRight = cross(viewWS, float3(0, 1, 0));
//                float LdotVR = dot(mainLight.direction, viewRight); //check if light is left or right of view, 1 if right 0 is left
//                
//                float3 fresnel = pow(1.0f - saturate(dot(normalWS, viewWS)), _FresnelPower) * _FresnelStrength;
//                float3 tipColor = (specular  + fresnel) * i.uv.y * lightColor;
//                float3 finalColor = (ambient + diffuse) * grassGradient + tipColor;
//                return float4(finalColor, 1);
//                
//                //return float4(normalWS * 0.5 + 0.5, 1);
//            }
//            ENDHLSL
//        }
//
////        //Shadow Caster
////        Pass
////        {
////            Tags
////            {
////                "LightMode" = "ShadowCaster"
////            }
////
////            ZWrite On
////            ColorMask 0
////
////            HLSLPROGRAM
////            #pragma vertex shadowPassVert
////            #pragma fragment shadowPassFrag
////
////            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
////            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
////            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
////            #include "GrassCommon.hlsl"
////
////            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
////
////            float3 _LightDirection;
////            float3 _LightPosition;
////
////            struct appdata
////            {
////                float4 positionOS : POSITION;
////                float3 normalOS : NORMAL;
////                float2 uv : TEXCOORD0;
////                UNITY_VERTEX_INPUT_INSTANCE_ID
////            };
////
////            struct v2f
////            {
////                float4 positionCS : SV_POSITION;
////            };
////
////            float4 GetShadowPositionHClip(float3 positionOS, float2 uv, float3 normalOS, float instanceID)
////            {
////
////                float3 positionWS = GetGrassPosition(positionOS, uv, instanceID);
////                float3 normalWS = normalize(_GrassDataBuffer[instanceID].up);
////                
////#if _CASTING_PUNCTUAL_LIGHT_SHADOW
////                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
////#else
////                float3 lightDirectionWS = _LightDirection;
////#endif
////
////                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
////                positionCS = ApplyShadowClamping(positionCS);
////
////                return positionCS;
////            }
////
////            v2f shadowPassVert(appdata v, uint instanceID : SV_INSTANCEID)
////            {
////                v2f o = (v2f)0;
////
////                o.positionCS = GetShadowPositionHClip(v.positionOS.xyz, v.uv, v.normalOS, instanceID);
////
////                return o;
////            }
////
////            float4 shadowPassFrag(v2f i) : SV_TARGET
////            {
////                return 0;
////            }
////
////            ENDHLSL
////        }
//        
//
//        //Depth Only
//        Pass
//        {
//            Tags
//            {
//                "LightMode" = "DepthOnly"
//            }
//
//            Cull Off
//            ZWrite On
//            ColorMask R
//
//            HLSLPROGRAM
//            #pragma vertex depthOnlyVert
//            #pragma fragment depthOnlyFrag
//
//            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//            #include "GrassCommon.hlsl"
//            
//            struct appdata
//            {
//                float4 positionOS : POSITION;
//                float2 uv : TEXCOORD0;
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//            };
//
//            struct v2f
//            {
//                float4 positionCS : SV_POSITION;
//            };
//
//            v2f depthOnlyVert(appdata v, uint instanceID : SV_INSTANCEID)
//            {
//                v2f o = (v2f)0;
//                
//                float3 positionWS = GetGrassPosition(v.positionOS, v.uv, instanceID);
//                o.positionCS = TransformWorldToHClip(positionWS);
//
//                return o;
//            }
//
//            float depthOnlyFrag(v2f i) : SV_TARGET
//            {
//                return i.positionCS.z;
//            }
//
//            ENDHLSL
//        }
//
//        //Depth Normals
//        Pass
//        {
//            Tags
//            {
//                "LightMode" = "DepthNormals"
//            }
//            
//            ZWrite On
//            Cull Off
//
//            HLSLPROGRAM
//            #pragma vertex depthNormalsVert
//            #pragma fragment depthNormalsFrag
//
//            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//            #include "GrassCommon.hlsl"
//            
//            struct appdata
//            {
//                float4 positionOS : POSITION;
//                float2 uv : TEXCOORD0;
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//            };
//
//            struct v2f
//            {
//                float4 positionCS : SV_POSITION;
//                float3 normalWS : TEXCOORD0;
//            };
//
//            v2f depthNormalsVert(appdata v, uint instanceID : SV_INSTANCEID)
//            {
//                v2f o = (v2f)0;
//                
//                float3 positionWS = GetGrassPosition(v.positionOS, v.uv, instanceID);
//                o.positionCS = TransformWorldToHClip(positionWS);
//       
//                o.normalWS = normalize(_GrassDataBuffer[instanceID].up);
//                
//                return o;
//            }
//
//            float4 depthNormalsFrag(v2f i) : SV_TARGET
//            {
//                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
//                
//                return float4(normalWS, 0.0f);
//            }
//
//            ENDHLSL
//        }
//    }
//}
