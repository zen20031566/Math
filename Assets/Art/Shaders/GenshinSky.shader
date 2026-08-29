Shader "Basics/GenshinSky"
{
    Properties
    {
        [HDR]_SunColor("Sun Color", Color) = (12, 6, 1, 1)
        _SunSize("Sun Size", Float) = 1
        _SunInnerBound("Sun Inner Bound", Float) = 0
        _SunOuterBound("Sun Outer Bound", Float) = 0.8
    	_SunInfScale("Sun Inf Scale", Float) = 1
    	[HDR] _SunInfColor("Sun Inf Color", Color) = (1, 1, 1, 1)
        
        _MoonTexture("Moon Texture", 2D) = "white" {}
        [HDR]_MoonColor("Moon Color", Color) = (4, 5.8, 5.8, 1)
        _MoonSize("Moon Size", Float) = 1
        
        _DayTopColor("Day Top Color", Color) = (0.10, 0.30, 0.75, 1)
        _DayBotColor("Day Bot Color", Color) = (0.30, 0.55, 0.90, 1)
        
        _NightTopColor("Night Top Color", Color) = (0.015, 0.035, 0.12, 1)
        _NightBotColor("Night Bot Color", Color) = (0.08, 0.18, 0.42, 1)
        
        _DayHorizonWidth("Day Horizon Width", Float) = 0.12
        _DayHorizonStrength("Day Horizon Strength", Float) = 0.35
        _DayHorizonColor("Day Horizon Color", Color) = (0.90, 0.92, 1.00, 1)
        
        _NightHorizonWidth("Night Horizon Width", Float) = 0.08
        _NightHorizonStrength("Night Horizon Strength", Float) = 0.15
        _NightHorizonColor("Night Horizon Color", Color) = (0.18, 0.35, 0.65, 1)
        
        _StarTexture("Star Texture", 2D) = "white" {}
        _StarNoiseScale("Star Noise Scale", Float) = 1
        _StarBlinkRate("Star Blink Rate", Float) = 0.12
        
        _GalaxyColor("GalaxyColor", Color) = (1.00, 1.00, 1.00, 1)
        _GalaxyColor1("GalaxyColor1", Color) = (1.00, 1.00, 1.00, 1)
        _GalaxyTexture("Galaxy Texture", 2D) = "white" {}
        _Simplex2DTexture("Simplex 2D Texture", 2D) = "white" {}
        _GalaxyNoiseScale("Galaxy Noise Scale", Float) = 1
        _GalaxyNoiseSpeed("Galaxy Noise Speed", Vector) = (0, 0.67, 0, 0)
        _GalaxyNoiseSpeed2("Galaxy Noise Speed2", Vector) = (0.5, 0.67, 0, 0)
        
        _NumOfInScatterPoints("Number of In Scattering Points", Float) = 8
        _AtmosphereHeight("Atmosphere Height", Float) = 200
        _DensityScaleHeight("Density Scale Height", Float) = 2
        _ScatteringStrength("Scattering Strength", Float) = 10
        _PlanetRadius("Planet Radius", Float) = 6000
    	[HDR] _MieColor("Mie Color", Color) = (1.00, 1.00, 1.00, 1)

    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
        }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SimplexNoise3D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" //access to depth buffer texture

            CBUFFER_START(UnityPerMaterial)
            float4 _SunColor;
            float _SunSize;
            float _SunInnerBound;
            float _SunOuterBound;
            float _SunInfScale;
            float4 _SunInfColor;
            
            float4 _MoonTexture_ST;
            float4 _MoonColor;
            float _MoonSize;
            
            float4 _DayTopColor;
            float4 _DayBotColor;
            
            float4 _NightTopColor;
            float4 _NightBotColor;

            float _DayHorizonWidth;
            float _DayHorizonStrength;
            float4 _DayHorizonColor;
            
            float _NightHorizonWidth;
            float _NightHorizonStrength;
            float4 _NightHorizonColor;
            
            float4 _StarTexture_ST;
            float _StarNoiseScale;
            float _StarBlinkRate;
            
            float4 _GalaxyColor;
            float4 _GalaxyColor1;
            float4 _GalaxyTexture_ST;
            float4 _Simplex2DTexture_ST;
            float _GalaxyNoiseScale;
            float2 _GalaxyNoiseSpeed;
            float2 _GalaxyNoiseSpeed2;
            
            float _NumOfInScatterPoints;
            float _AtmosphereHeight;
            float _DensityScaleHeight;
            float _ScatteringStrength;
            float _PlanetRadius;
            float4 _MieColor;
            CBUFFER_END
            
            static const float _MieBeta = 3.996e-3;   
            static const float _MieBetaExt = 8.396e-3; 
            static const float _MieG = 0.8;
            
            float4x4 _DirLightLToW;
            
            TEXTURE2D(_MoonTexture);
			SAMPLER(sampler_MoonTexture);
            
            TEXTURE2D(_StarTexture);
            SAMPLER(sampler_StarTexture);
            
            TEXTURE2D(_GalaxyTexture);
            SAMPLER(sampler_GalaxyTexture);
            
            TEXTURE2D(_Simplex2DTexture);
            SAMPLER(sampler_Simplex2DTexture);
            
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
            
            float miePhase(float cosAngle)
			{
				float g = _MieG;
				float g2 = g * g;
				float phase = (1.0 / (4.0 * PI)) * ((3.0 * (1.0 - g2)) / (2.0 * (2.0 + g2))) * ((1 + cosAngle * cosAngle) / (pow((1 + g2 - 2 * g*cosAngle), 3.0 / 2.0)));
				return phase;
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
                float miePhaseValue = miePhase(mu);
                
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
            
            float3 ACESFilm(float3 x)
			{
			    float a = 2.51;
			    float b = 0.03;
			    float c = 2.43;
			    float d = 0.59;
			    float e = 0.14;
			    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
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
	        	
                float sceneDist = LinearEyeDepth(rawDepth, _ZBufferParams); // world-space distance to whatever's there (ground, object, or far plane if empty sky)
	        	
                //Sun
                float sunSDF = distance(positionWS, mainLight.direction); //how far pixel from sun 
                float sunArea = 1 - sunSDF / _SunSize; //if pixel closer value higher
                float3 sunMask = smoothstep(_SunInnerBound, _SunOuterBound, sunArea);
                float3 sunFallColor = _SunColor * 0.4;
                
                float sunFallBlend = smoothstep(-0.03, 0.03, mainLight.direction.y); //1 sun overhead 0 sun at horizonline 
                //example if sun y <= -0.03 then full dark else if sun > 0.03 then normal sun color
                float3 sunFinalColor = lerp(sunFallColor, _SunColor.rgb, sunFallBlend) * sunMask;
                
	        	//sunInfluence
			    float sunMask2 = smoothstep(-0.4, 0.4, -mul(viewWS,_DirLightLToW).z) - 0.3; //how alligned the pixels dir to camera are to the sun dir
	        	//matrix mul is same as dot the 2 dir its just u tranform the dir into the sun rotation space?
			    float sunInfScaleMask = smoothstep(-0.01,0.1,mainLight.direction.y) * smoothstep(-0.4,-0.01,-mainLight.direction.y); //only shows when sun at horizon
			    float3 finalSunInfColor = _SunInfColor * sunMask2 * _SunInfScale * sunInfScaleMask;
	        	
                //Moon
                float3 sunUV = mul(positionWS.xyz, _DirLightLToW);
                float2 moonUV = (sunUV.xy / _MoonSize) * 0.5 + 0.5; //scale by moon size and remap from -1 to 1 to 0 to 1
                moonUV = moonUV * _MoonTexture_ST.xy + _MoonTexture_ST.zw; 
                float moonSDF = distance(positionWS, -mainLight.direction); 
                float moonMask = step(moonSDF, _MoonSize); //if sdf is <= moonsize = 1 so if the pixel dist is at or lesser than moon size
                float4 moonTexture = SAMPLE_TEXTURE2D(_MoonTexture, sampler_MoonTexture, moonUV);
                float4 moonFinalColor = moonMask * moonTexture * _MoonColor;
                
                //Day Night
                float dayNightCycle = smoothstep(-0.3, 0.25, mainLight.direction.y); //1 for day 0 for night
	        	
                float3 dayGradient = lerp(_DayBotColor, _DayTopColor, saturate(positionWS.y));
                float3 nightGradient = lerp(_NightBotColor, _NightTopColor, saturate(positionWS.y));
                float3 skyGradients = lerp(nightGradient, dayGradient, dayNightCycle);
                
                float horizonWidth = lerp(_NightHorizonWidth, _DayHorizonWidth, dayNightCycle);
                float horizonStrength = lerp(_NightHorizonStrength, _DayHorizonStrength, dayNightCycle);
                float horizonLineMask = smoothstep(-horizonWidth, 0, positionWS.y) * smoothstep(-horizonWidth, 0, -positionWS.y);
                
                float3 horizonLineColor = lerp(_NightHorizonColor, _DayHorizonColor, dayNightCycle);
                
                float3 finalSkyColor = skyGradients * (1 - horizonLineMask) + horizonLineColor * horizonLineMask * horizonStrength;
                
                //Galaxy
                float2 galaxyNoiseUV = (uv * _Simplex2DTexture_ST.xy + _Simplex2DTexture_ST.zw)
                    + float2(_Time.x * _GalaxyNoiseSpeed.x, _Time.x * _GalaxyNoiseSpeed.y);
                float4 galaxyNoiseTexture = SAMPLE_TEXTURE2D(_Simplex2DTexture, sampler_Simplex2DTexture, galaxyNoiseUV);
                float galaxyNoise = (galaxyNoiseTexture.r - 0.5) * _GalaxyNoiseScale; //remap from 0 to 1 to -0.5 to 0.5
                //float galaxyNoise = SimplexNoise(positionWS * _GalaxyNoiseScale + _Time.x * _GalaxyNoiseSpeed.y);
                float2 galaxyUV = (uv + galaxyNoise) * _GalaxyTexture_ST.xy + _GalaxyTexture_ST.zw;
                float4 galaxyTexture = SAMPLE_TEXTURE2D(_GalaxyTexture, sampler_GalaxyTexture, galaxyUV);
                float3 galaxyColor =  (_GalaxyColor * (-galaxyTexture.r + galaxyTexture.g) 
                    + _GalaxyColor1 * galaxyTexture.r) * smoothstep(0, 0.2, 1 - galaxyTexture.g);
                
                float2 galaxyNoiseUV2 = (uv * _Simplex2DTexture_ST.xy + _Simplex2DTexture_ST.zw) 
                    + float2(_Time.x * _GalaxyNoiseSpeed2.x, _Time.x * _GalaxyNoiseSpeed2.y);
                float4 galaxyNoiseTexture2 = SAMPLE_TEXTURE2D(_Simplex2DTexture, sampler_Simplex2DTexture, galaxyNoiseUV2);
                float galaxyNoise2 = (galaxyNoiseTexture2.r - 0.5) * _GalaxyNoiseScale; 
                float2 galaxyUV2 = (uv + galaxyNoise2) * _GalaxyTexture_ST.xy + _GalaxyTexture_ST.zw;
                float4 galaxyTexture2 = SAMPLE_TEXTURE2D(_GalaxyTexture, sampler_GalaxyTexture, galaxyUV2);
                float3 galaxyColor2 =  (_GalaxyColor * (-galaxyTexture2.r + galaxyTexture2.g) 
                    + _GalaxyColor1 * galaxyTexture2.r) * smoothstep(0, 0.3, 1 - galaxyTexture2.g);
                
                float3 finalGalaxyColor = (galaxyColor + galaxyColor2) * 0.5;
                
                //Stars
                float2 starUV = uv * _StarTexture_ST.xy + _StarTexture_ST.zw;
                float4 starTexture = SAMPLE_TEXTURE2D(_StarTexture, sampler_StarTexture, starUV);
                
                float starNoiseRaw = SimplexNoise(positionWS * _StarNoiseScale + _Time.x * _StarBlinkRate);
                float starNoise = starNoiseRaw * 0.5 + 0.5; //remap from -1 to 1 to -0.5 to 0.5
                
                float starBright = smoothstep(0.613, 0.713, starNoise); //star brightness noise below 0.613 = 0, above 0.713 = 1
                float starPos = smoothstep(0.21, 0.31, starTexture.r); //remove out the lower part smoothly
                float3 starColor = starPos * starBright;
                starColor = starColor * galaxyTexture2.r * 3 + starColor * (1 - galaxyTexture2.r) * 0.3;
                //float starMask = lerp(1 - smoothstep(-0.7, -0.2, -positionWS.y), 0, dayNightCycle);
                float starMask = 1 - smoothstep(-0.7, -0.2, -positionWS.y);
                float3 finalStarColor = (_GalaxyColor + starColor) * starMask;
                
                float3 finalStarGalaxyColor= lerp(finalStarColor + finalGalaxyColor, 0, dayNightCycle);
                
                //Mie Scattering
	            float3 scatteringColor = 0;
	            float3 rayOrigin = float3(0, 1, 0);
                float3 planetCenter = float3(0, -_PlanetRadius, 0);
                float2 hitInfo = raySphere(planetCenter, _PlanetRadius + _AtmosphereHeight, rayOrigin, viewWS);
	            float distToAtmosphere = hitInfo.x;
	            float distThroughAtmosphere =  min(hitInfo.y, hitInfo.y * 100);
	        	
	        	if (distThroughAtmosphere > 0)
	        	
	        	{
	        		 float3 inscattering = calcInScatteringLight(rayOrigin, viewWS, distThroughAtmosphere, mainLight.direction);
					 scatteringColor = _MieColor * ACESFilm(inscattering) * _ScatteringStrength;
	        	}

                //Final 
                float3 finalColor = sunFinalColor + finalSunInfColor + moonFinalColor + finalSkyColor + finalStarGalaxyColor + scatteringColor;
	        	
	        	//float3 test = lerp(0, 1, sunInfScaleMask);
                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}