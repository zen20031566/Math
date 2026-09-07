Shader "Basics/MieScatter"
{
    Properties
    {
        _NumOfInScatterPoints("Number of In Scattering Points", Float) = 2
        _GroundHeight("Ground Height", Float) = 0
        _AtmosphereHeight("Atmosphere Height", Float) = 100
        _DensityScaleHeight("Density Scale Height", Float) = 16
        _ScatteringStrength("Scattering Strength", Float) = 2
        _PlanetRadius("Planet Radius", Float) = 100
    	[HDR] _MieColor("Mie Color", Color) = (1.00, 1.00, 1.00, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "PreviewType" = "Opaque"
        }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" //access to depth buffer texture
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _NumOfInScatterPoints;
            float _GroundHeight;
            float _AtmosphereHeight;
            float _DensityScaleHeight;
            float _ScatteringStrength;
            float _PlanetRadius;
            float4 _MieColor;
            CBUFFER_END
            
            static const float _MieBeta = 3.996e-6; //0.003 – 0.004
            static const float _MieBetaExt = 8.396e-6; // 0.003 * 1.11
            static const float _MieG = 0.8; //0.76 – 0.8
            
            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float4 positionSS : TEXCOORD2; //screen space pos
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.viewWS = GetWorldSpaceViewDir(o.positionWS);
                o.positionSS = ComputeScreenPos(o.positionCS);
                
                return o;
            }
            
            //refs 
            //https://zhuanlan.zhihu.com/p/540692272 
            //https://www.youtube.com/watch?v=DxfEbulyFcY
            //https://blog.maximeheckel.com/posts/on-rendering-the-sky-sunsets-and-planets/
            
            //Atmospheric Scattering Calculations
            
            static const float maxFloat = 3.402823466e+38;
            // Returns vector (dstToSphere, dstThroughSphere)
	        // If ray origin is inside sphere, dstToSphere = 0
	        // If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
	        float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) {
		        float3 offset = rayOrigin - sphereCentre;
		        float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
		        float b = 2 * dot(offset, rayDir);
		        float c = dot (offset, offset) - sphereRadius * sphereRadius;
		        float d = b * b - 4 * a * c; // Discriminant from quadratic formula

		        // Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
		        if (d > 0) {
			        float s = sqrt(d);
			        float dstToSphereNear = max(0, (-b - s) / (2 * a));
			        float dstToSphereFar = (-b + s) / (2 * a);

			        // Ignore intersections that occur behind the ray
			        if (dstToSphereFar >= 0) {
				        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
			        }
		        }
		        // Ray did not intersect sphere
		        return float2(maxFloat, 0);
	        }
            
            float miePhase(float mu, float mieG)
            {
                float gg = mieG * mieG;
                float num = 3.0 * (1.0 - gg) * (1.0 + mu * mu);
                float den = 8.0 * PI * (2.0 + gg) * pow(max(1.0 + gg - 2.0 * mieG * mu, 1e-4), 1.5);
                return num / den;
            }
            
            float calcDensity(float3 position)
            {
                float3 planetCenter = float3(0,-_PlanetRadius,0);
	            float height = distance(position,planetCenter) - _PlanetRadius;
	            return exp(-(height/_DensityScaleHeight));
            }
            
            float3 calcInScatteringLight(float3 rayOrigin, float3 rayDir, float rayLength, float3 sunDir) 
            {
                float3 inScatterPoint = rayOrigin;
                float stepSize = rayLength / (_NumOfInScatterPoints - 1); 
                
                //I have no fking idea what his modifications do or why compared to tranditional atmospheric scattering papers
                float mu = dot(rayDir, sunDir);
                float miePhaseValue = miePhase(mu, _MieG);
                
                float mieScatterSum = 0;  
                float sunMieOpticalDepth = 0;
                float viewMieOpticalDepth = 0;
                
                float localMieDensity = 0;
                float prevLocalMieDensity = 0;
                float prevTransmittance = 0;
                
                localMieDensity = calcDensity(rayOrigin);
                viewMieOpticalDepth = localMieDensity * stepSize;
                prevLocalMieDensity = localMieDensity;
                
                float3 transmittance = exp(-(sunMieOpticalDepth + viewMieOpticalDepth) * _MieBetaExt) * localMieDensity;
                prevTransmittance = transmittance;
                
                for (int i = 0; i < _NumOfInScatterPoints; i++)
                {
                    localMieDensity = calcDensity(inScatterPoint);
                    viewMieOpticalDepth += (prevLocalMieDensity + localMieDensity) * stepSize / 2;
                    
                    transmittance = exp(-(sunMieOpticalDepth + viewMieOpticalDepth) * _MieBetaExt) * localMieDensity;
                    
                    mieScatterSum += (prevTransmittance + transmittance) * stepSize / 2;
                    
                    prevTransmittance = transmittance;
		            prevLocalMieDensity = localMieDensity;
                    
                    inScatterPoint += rayDir * stepSize; //move the point towards rayDir by stepSize
                }
                
                float3 inScatteredLight = miePhaseValue * _MieBeta * mieScatterSum;
                
                return inScatteredLight;
            }
            
            float4 frag(v2f i) : SV_TARGET
            {
                float3 positionWS = normalize(i.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                //float2 uv = float2(atan2(positionWS.x, positionWS.z) / TWO_PI, asin(positionWS.y) / HALF_PI); //equirectangular projection
                float2 uv = float2(positionWS.x, positionWS.z) / acos(-positionWS.y); //move distortion below horizon
                float3 viewWS = -normalize(i.viewWS);
                float3 cameraWS = GetCameraPositionWS();
                
                Light mainLight = GetMainLight(shadowCoord);
                //float3 lightColor = mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLight.color;
                
	        	float2 screenUV = i.positionSS.xy / i.positionSS.w;
				float rawDepth = SampleSceneDepth(screenUV); //1 is as far as posible 0 is as near as possible

				float linearDepth = Linear01Depth(rawDepth, _ZBufferParams); // Converts the raw, non-linear depth value (rawDepth) into a linear 0-1 range
																				// By default, the depth buffer stores values non-linearly (most precision is
	        	float sceneColor = SampleSceneColor(screenUV);
	        	
	        	
                //Mie Scattering
	            float3 scatteringColor = 0;
	            float3 rayOrigin = float3(0, 1, 0);
                float3 planetCenter = float3(0, -_PlanetRadius, 0);
                float2 hitInfo = raySphere(planetCenter, _PlanetRadius + _AtmosphereHeight, rayOrigin, -viewWS);
	            float distToAtmosphere = hitInfo.x;
	            float distThroughAtmosphere = hitInfo.y;
	        	
	          
	        	if (distThroughAtmosphere > 0)
	        	{
	        		 float3 inscattering = calcInScatteringLight(rayOrigin, viewWS, distThroughAtmosphere, mainLight.direction);
					 scatteringColor = _MieColor * inscattering * _ScatteringStrength;
	        	}

                //Final 
                float3 finalColor = scatteringColor + sceneColor;
	        	
                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}