#ifndef _INCLUDE_LIGHTINGCOMMON
#define _INCLUDE_LIGHTINGCOMMON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"

float BlinnPhong(float3 normal, float3 viewDir, float3 lightDir, float glossiness)
{
    float NdotL = saturate(dot(normal, lightDir));
                
    float3 halfVector = normalize(lightDir + viewDir);
    float specular = saturate(dot(normal, halfVector));
    specular = pow(specular, glossiness);
    specular *= NdotL;  //prevent highlights when the light is behind the grass

    return specular;
}

float Phong(float3 normal, float3 lightDir, float viewDir, float glosiness)
{
     float3 reflectedVector = reflect(lightDir, normal);
     float specular = saturate(dot(reflectedVector, viewDir));
     specular = pow(specular, glosiness);
    return specular;
}

float GGX_DistanceFade(float3 normal, float3 viewDir, float3 lightDir,float roughness,float distanceFade)
{
    float3 halfVector = normalize(lightDir + viewDir);
    
    float NdotH = saturate(dot(normal, halfVector));
    float NdotV = saturate(dot(normal, viewDir));
    float NdotL = saturate(dot(normal, lightDir));
    
    float  D = D_GGX(NdotH, roughness); //normal distrubution function
    float3 F = F_Schlick(float3(0.04, 0.04, 0.04), NdotV); //fresnel term  
    //float G = V_SmithJointGGX(NdotL, NdotV, roughness);
    
    return D * F * distanceFade; //Kill G for more natural looking
}

#endif
