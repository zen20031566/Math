Shader "Basics/GenshinSky"
{
    Properties
    {
        [HDR]_SunColor("Sun Color", Color) = (12, 6, 1, 1)
        _SunSize("Sun Size", Float) = 1
        _SunInnerBound("Sun Inner Bound", Float) = 0
        _SunOuterBound("Sun Outer Bound", Float) = 0.8
        
        _MoonTexture("Moon Texture", 2D) = "white" {}
        [HDR]_MoonColor("Moon Color", Color) = (4, 5.8, 5.8, 1)
        _MoonSize("Moon Size", Float) = 1
        
        _DayTopColor("Day Top Color", Color) = (0.10, 0.30, 0.75, 1)
        _DayMidColor("Day Mid Color", Color) = (0.30, 0.55, 0.90, 1)
        _DayBotColor("Day Bot Color", Color) = (0.65, 0.80, 1.00, 1)
        
        _NightTopColor("Night Top Color", Color) = (0.015, 0.035, 0.12, 1)
        _NightMidColor("Night Mid Color", Color) = (0.025, 0.070, 0.22, 1)
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
        
        _NumOfInScatterPoints("Number of In Scattering Points", Float) = 1
        _RayLength("Ray Length", Float) = 10
        _SunRayLength("Sun Ray Length", Float) = 15
        _PlanetRadius("PlanetRadius", Float) = 50
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

            CBUFFER_START(UnityPerMaterial)
            float4 _SunColor;
            float _SunSize;
            float _SunInnerBound;
            float _SunOuterBound;
            
            float4 _MoonTexture_ST;
            float4 _MoonColor;
            float _MoonSize;
            
            float4 _DayTopColor;
            float4 _DayMidColor;
            float4 _DayBotColor;
            
            float4 _NightTopColor;
            float4 _NightMidColor;
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
            float _RayLength;
            float _SunRayLength;
            float _PlanetRadius;
            float _DensityScaleHeight;

            CBUFFER_END
            
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
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.viewWS = GetWorldSpaceViewDir(o.positionWS);
                
                return o;
            }
            
            //refs 
            //Genshin Sky Recreation: https://zhuanlan.zhihu.com/p/540692272 
            //Rayleigh Scattering: https://www.youtube.com/watch?v=DxfEbulyFcY
            
            //Atmospheric Scattering Calculations
            //ray origin and ray dir is your camera
            //ray length shoots to outside atmosphere usualy if it doesnt hit anything
            float calcScattering(float3 rayOrigin, float3 rayDir, float rayLength) 
            {
                float3 inScatterPoint = rayOrigin;
                float stepSize = rayLength / (_NumOfInScatterPoints - 1); //-1 cause u calc the distance between them not the points 
                                                                          //this comment is here because i am stupid
                float inScatteredLight = 0;                               
                for (int i = 0; i < _NumOfInScatterPoints; i++)
                {
                    float sunRayLength = _SunRayLength; //usualy we need to check distance through the whole atmosphere but since we dont have real planets it not needed 
                    
                    float sunRayOpticalDepth = opticalDepth(inScatterPoint, dirToSun, _SunRayLength); //density along ray also known as optical depth
                                                                                                      //how much air did the ray pass through?? some sort of other density idk
                    
                    float viewRayOpticalDepth = opticalDepth(inScatterPoint, -rayDir, stepSize * i); //as the light scatters some goes to view as well
                    
                    float transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth)); //when optical depth is 0 transimittance is 1 cause all light reaches 
                                                                                            //as optical depth increase more light scattering
                    
                    float localDensity = densityAtPoint(inScatterPoint); //greater density more scattering
                    
                    inScatteredLight += localDensity * transmittance * stepSize;
                    inScatterPoint += rayDir * stepSize; //move the point towards rayDir by stepSize
                }
                
                return inScatteredLight;
            }
            
            float densityAtPoint(float3 densitySamplePoint) //less dense high up in sky more dense near ground
            {
                float3 planetCentre = float3(0, -_PlanetRadius, 0); //Just use a random planet radius cause we dont have a actual planet
                float heightAboveSurface = distance(densitySamplePoint, planetCentre) - _PlanetRadius;
                float localDensity = exp(heightAboveSurface / _DensityScaleHeight);
                
                                                    
            }
            
            float4 frag(v2f i) : SV_TARGET
            {
                float3 positionWS = normalize(i.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                //float2 uv = float2(atan2(positionWS.x, positionWS.z) / TWO_PI, asin(positionWS.y) / HALF_PI); //equirectangular projection
                float2 uv = float2(positionWS.x, positionWS.z) / acos(-positionWS.y); //move distortion below horizon
                
                Light mainLight = GetMainLight(shadowCoord);
                //float3 lightColor = mainLight.distanceAttenuation * mainLight.shadowAttenuation * mainLight.color;
                
                //Sun
                float sunSDF = distance(positionWS, mainLight.direction); //how far pixel from sun 
                float sunArea = 1 - sunSDF / _SunSize; //if pixel closer value higher
                float3 sunMask = smoothstep(_SunInnerBound, _SunOuterBound, sunArea);
                float3 sunFallColor = _SunColor * 0.4;
                
                float sunFallBlend = smoothstep(-0.03, 0.03, mainLight.direction.y); //1 sun overhead 0 sun at horizonline 
                //example if sun y <= -0.03 then full dark else if sun > 0.03 then normal sun color
                float3 sunFinalColor = lerp(sunFallColor, _SunColor.rgb, sunFallBlend) * sunMask;
                
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
                
                float3 dayBotPart = lerp(_DayBotColor, _DayMidColor, saturate(positionWS.y)) * step(0, -positionWS.y);
                float3 dayTopPart = lerp(_DayMidColor, _DayTopColor, saturate(positionWS.y)) * step(0, positionWS.y);
                float3 dayGradient = dayBotPart + dayTopPart;  
                
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
                
                float3 finalColor = sunFinalColor + moonFinalColor + finalSkyColor + finalStarGalaxyColor;
                //finalColor = float4(galaxyTexture.rgb, 1.0);
                return float4(positionWS* 0.5 + 0.5, 1);
                //return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}