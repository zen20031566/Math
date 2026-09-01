Shader "Basics/Grass"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            
            struct GrassData 
            {
                float4 position;
                float2 uv;
                float displacement;
            };
            StructuredBuffer<GrassData> _GrassDataBuffer;
            
            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD3;
                float3 viewWS : TEXCOORD4;
                float3 normalWS : TEXCOORD5;
                float4 tangentWS : TEXCOORD6;
            };

            v2f vert(appdata v, uint instanceID : SV_INSTANCEID)
            {
                v2f o = (v2f)0;
                float4 grassPosition = _GrassDataBuffer[instanceID].position;
                o.positionWS = grassPosition + v.positionOS.xyz;
                o.positionCS = TransformWorldToHClip(o.positionWS);
           
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
                
              
                
                return float4(_BaseColor);
            }
            ENDHLSL
        }
    }
}
