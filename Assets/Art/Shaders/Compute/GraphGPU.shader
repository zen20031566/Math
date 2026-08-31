Shader "Basics/GraphGPU"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
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
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
            
            CBUFFER_START(UnityPerMaterial)
            float _Smoothness;
            CBUFFER_END
            float _GraphTime;
            float _Step;
            
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<float3> _Positions;
            #endif
            
            void ConfigureProcedural ()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    float3 position = _Positions[unity_InstanceID];

                    unity_ObjectToWorld = 0.0;
                    unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
                    unity_ObjectToWorld._m00_m11_m22 = _Step;
                #endif
            }
            
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

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                ConfigureProcedural();
                
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
                
              
                
                return float4(i.uv, 1, 1);
            }
            ENDHLSL
        }
    }
}
