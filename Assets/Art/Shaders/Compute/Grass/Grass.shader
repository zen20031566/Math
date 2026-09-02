Shader "Basics/Grass"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.25, 0.55, 0.12, 1)
        _BottomColor("Bottom Color", Color) = (0.08, 0.25, 0.04, 1)
        _GradientThreshold("GradientThreshold", Float) = 1
        _Glossiness("Glossiness", Float) = 1
        _WindStrength("Wind Strength", Float) = 0.5
        _WindSpeed("Wind Speed", Float) = 1
        
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
            
            Cull Off
            Zwrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            #include "Assets/Art/Shaders/Random.cginc"
            #include "Assets/Art/Shaders/SimplexNoise2D.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _BottomColor;
            float _GradientThreshold;
            float _Glossiness;
            float _WindStrength;
            float  _WindSpeed;
            CBUFFER_END
            
            struct GrassData 
            {
                float4 position;
                float3 up;
                float yawAngle;
                float2 scale;
            };

            StructuredBuffer<GrassData> _GrassDataBuffer;
            
            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 viewWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                float2 dynamicLightmapUV : TEXCOORD5;
            };
            
            float4 RotateAroundYInDegrees (float4 vertex, float degrees) {
                float alpha = degrees * PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float4(mul(m, vertex.xz), vertex.yw).xzyw;
            }
            
            v2f vert(appdata v, uint instanceID : SV_INSTANCEID)
            {
                v2f o = (v2f)0;
                //position from buffer
                float4 grassPosition = _GrassDataBuffer[instanceID].position;
                
                //randomize rotation angle and rotate around y axis
                float angle = _GrassDataBuffer[instanceID].yawAngle;
                float4 localPosition = RotateAroundYInDegrees(v.positionOS, angle);
                
                //randomize scale
                float2 scale = _GrassDataBuffer[instanceID].scale;
                localPosition.xz *= scale.x;
                localPosition.y  *= scale.y;
                
                float3 positionWS = grassPosition.xyz + localPosition.xyz;
                
                //wind
                float sway = sin(_Time.y * _WindSpeed) + SimplexNoise(positionWS.xz);
                positionWS.x += sway * _WindStrength * v.uv.y;
                
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(o.positionWS);
                
                o.uv = v.uv;
                
                // float3 normalOS = RotateAroundYInDegrees(float4(v.normalOS, 0), angle).xyz;
                // o.normalWS = TransformObjectToWorldNormal(normalOS);
                o.normalWS = normalize(_GrassDataBuffer[instanceID].up);
                
                o.viewWS = GetWorldSpaceViewDir(o.positionWS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                o.dynamicLightmapUV = v.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                
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

                float3 diffuse= saturate(dot(normalWS, mainLight.direction)) * lightColor;
                
                //Blinn-Phong
                float3 halfwayDir = normalize(mainLight.direction + viewWS);
                float3 specular = saturate(dot(normalWS, halfwayDir));
                specular = saturate(lerp(0, specular, i.uv.y)); // clamp again, in case uv.y is out of [0,1]
                specular = pow(specular, _Glossiness);
                specular = smoothstep(0, 1, specular) * lightColor; //this fixes HDR overexposure
                
                float t = saturate(i.uv.y + _GradientThreshold);
                float3 gradient = lerp(_BottomColor.rgb , _TopColor.rgb, t);
                float3 finalColor = (ambient + diffuse) * gradient + specular;
                
                return float4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
