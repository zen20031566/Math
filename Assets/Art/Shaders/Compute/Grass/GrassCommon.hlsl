#ifndef _INCLUDE_GRASSCOMMON
#define _INCLUDE_GRASSCOMMON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/Art/Shaders/Random.cginc"
#include "Assets/Art/Shaders/SimplexNoise2D.hlsl"
#include "Assets/Art/Shaders/Quarternion.hlsl"

struct GrassData 
{
    float4 position;
    float3 up;
    float3 forward;
    float2 scale;
};

StructuredBuffer<GrassData> _GrassDataBuffer;

CBUFFER_START(UnityPerMaterial)
float2 _WindDirection;
float _WindStrength;
float _WindSpeed;
float _GrassBend;
float4 _WindTexture_ST;
CBUFFER_END

TEXTURE2D(_WindTexture);
SAMPLER(sampler_WindTexture);

float3 GetGrassPosition(float3 positionOS,float2 uv, uint instanceID)
{
    //position from buffer
    GrassData grass = _GrassDataBuffer[instanceID];
    
    //forward direction is randomized and grass alligned to surface normal
    float4 facingRot = from_to_rotation(float3(0, 0, 1), grass.forward);
    float4 upRot = from_to_rotation(float3(0, 1, 0), grass.up);
    float4 grassRot = qmul(upRot, facingRot);
    float3 localPosition = rotate_vector(positionOS, grassRot);
    
    //scale
    localPosition.xz *= grass.scale.x;
    localPosition.y  *= grass.scale.y;
    
    float2 xzOffset = (localPosition.y * localPosition.y) * 
        float2(
            randValue(grass.position.x + instanceID) * 2.0 - 1.0, 
            randValue(grass.position.z + instanceID * 1.37 + 91.0) * 2.0 - 1.0
            );
    
        xzOffset += (localPosition.y * localPosition.y) *
        float2(
        SimplexNoise(grass.position.xz * 2),
        SimplexNoise(grass.position.xz * 2 + 67)
        );
    
    xzOffset *= _GrassBend;
    
    float2 windUV = grass.position.xz * _WindTexture_ST.xy + _WindTexture_ST.zw;
    windUV = windUV + _WindDirection * _Time.y * _WindSpeed;
    float4 windTex = SAMPLE_TEXTURE2D_LOD(_WindTexture, sampler_WindTexture,windUV, 0);
    float2 wind = (windTex.rg * 2 - 1) * _WindStrength * uv.y;
    xzOffset += wind;
    
    localPosition.xz += xzOffset;  
    float xzOffsetLen = length(xzOffset);
    
    float originalY = localPosition.y; 
    localPosition.y = sqrt(max(originalY * originalY - xzOffsetLen * xzOffsetLen, 0.0));
    
    float3 positionWS = grass.position.xyz + localPosition.xyz; //final 

    return positionWS;
}

#endif
