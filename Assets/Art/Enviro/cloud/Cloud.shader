Shader "Basics/Cloud"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor("Shadow Color", Color) = (0.5, 0.5, 0.7, 1)
        [NoScaleOffset] _FrontMap("Front Map", 2D) = "white" {}
        [NoScaleOffset] _BackMap("Back Map", 2D) = "white" {}
        _Density("Density", Range(1, 20)) = 1
        _FogFactor("Fog Factor", Float) = 0.5
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
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadowColor;
            float _Density;
            float _FogFactor;
            CBUFFER_END
            
            TEXTURE2D(_FrontMap);
            TEXTURE2D(_BackMap);
            SAMPLER(sampler_linear_repeat);
            
            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.uv = v.uv;
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
                
                //if TBN on left side then its world to tangent?
                //normally if u sample normal map u do mul(normal, TBN) which changes the normal map normal from tangent to world this is opposite
                //note the light direction should be normalized but in unity docs it says it is so yay?
                //another note glsl its flipped so mul(v, TBN) for tangent to world because mat3 TBN is column and float3x3 TBN is rows???????
                float3 tangentLightDir = normalize(mul(TBN, mainLight.direction)); 
                
                float4 RTBk = SAMPLE_TEXTURE2D(_FrontMap, sampler_linear_repeat, i.uv); //Right Top Back
                float4 LBtF = SAMPLE_TEXTURE2D(_BackMap, sampler_linear_repeat, i.uv); //Left Bottom Front
                
                float3 weight;
                
                //Equal to below
                // float rightAmount  = dot(mainLight.direction, i.tangentWS.xyz);
                // float topAmount    = dot(mainLight.direction, bitangentWS.xyz);
                // float frontAmount  = dot(mainLight.direction, normalWS.xyz);
                //
                // weight.x = (rightAmount > 0) ? RTBk.x : LBtF.x;
                // weight.y = (topAmount  > 0) ? RTBk.y : LBtF.y;
                // weight.z = (frontAmount  < 0) ? RTBk.z : LBtF.z;
                //
                weight.x = (tangentLightDir.x > 0) ? RTBk.x : LBtF.x; // right vs left
                weight.y = (tangentLightDir.y > 0) ? RTBk.y : LBtF.y; // top vs bottom
                weight.z = (tangentLightDir.z < 0) ? RTBk.z : LBtF.z; // back vs front //idk why its flipped i fked up somewhere but this fixes?
                
                //pythogaras length
                //length = sqrt(x^2 + y^2 + z^2)
                //tangentLightDir is normalized so
                //length = sqrt(x^2 + y^2 + z^2) = 1
                //remove sqrt length^2 = (x^2 + y^2 + z^2) = 1
                //essentially all 3 add up to 1 more specifically a weighted average of the 3 components 
                //so we multiply each component by the weight components to get a singular weight
                //multiplying each component is the dot product formula!
                
                float3 sqrDir = tangentLightDir * tangentLightDir;
                float transmission = dot(sqrDir, weight); //how much light on each pixel
                
                float3 finalColor = exp(-1.0 * (1 - transmission) * _Density) * mainLight.color;
                
                // float3 finalColor = lerp(_ShadowColor, _BaseColor, transmission);
                finalColor = MixFog(finalColor,  _FogFactor);
                return float4(finalColor, RTBk.a);
            
            }

            ENDHLSL
        }
    }
}