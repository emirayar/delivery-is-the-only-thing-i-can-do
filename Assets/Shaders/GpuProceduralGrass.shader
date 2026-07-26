Shader "GMTK/GPU Procedural Grass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.055, 0.19, 0.025, 1)
        _TipColor ("Tip Color", Color) = (0.39, 0.69, 0.12, 1)
        _DryColor ("Dry Variation", Color) = (0.68, 0.53, 0.12, 1)
        _MinBladeHeight ("Minimum Height", Float) = 0.55
        _MaxBladeHeight ("Maximum Height", Float) = 1.15
        _MinBladeWidth ("Minimum Width", Float) = 0.045
        _MaxBladeWidth ("Maximum Width", Float) = 0.085
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.18
        _WindScale ("Wind Scale", Float) = 0.16
        _WindSpeed ("Wind Speed", Float) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardGrass"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct GrassInstance
            {
                float4 positionRandom;
                float4 parameters;
            };

            StructuredBuffer<GrassInstance> _GrassInstances;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _DryColor;
                float _MinBladeHeight;
                float _MaxBladeHeight;
                float _MinBladeWidth;
                float _MaxBladeWidth;
                float _WindStrength;
                float _WindScale;
                float _WindSpeed;
                float _RoadHalfWidth;
                float _RoadClearance;
                float _RoadEdgeFade;
                float3 _GrassInteractorPosition;
                float _GrassInteractionRadius;
                float _GrassInteractionStrength;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR0;
                half visibility : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453123);
            }

            float2 Rotate2D(float2 value, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float2(
                    value.x * cosine - value.y * sine,
                    value.x * sine + value.y * cosine);
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                GrassInstance instance = _GrassInstances[instanceID];
                float3 root = instance.positionRandom.xyz;
                float randomValue = instance.positionRandom.w;
                float lateralDistance = abs(instance.parameters.x);
                float dryVariation = instance.parameters.y;
                float lean = instance.parameters.z;

                static const uint cornerLookup[6] = { 0, 2, 1, 1, 2, 3 };
                uint plane = vertexID / 6;
                uint corner = cornerLookup[vertexID % 6];
                float heightMask = corner >= 2 ? 1.0 : 0.0;
                float sideSign = (corner == 0 || corner == 2) ? -1.0 : 1.0;

                float angle = randomValue * 6.2831853 + plane * 1.5707963;
                float2 side = Rotate2D(float2(1.0, 0.0), angle);
                float bladeHeight = lerp(
                    _MinBladeHeight,
                    _MaxBladeHeight,
                    Hash11(randomValue + 2.17));
                float bladeWidth = lerp(
                    _MinBladeWidth,
                    _MaxBladeWidth,
                    Hash11(randomValue + 7.31));

                float roadEdge = _RoadHalfWidth + _RoadClearance;
                float roadDensity = saturate(
                    (lateralDistance - roadEdge) / max(_RoadEdgeFade, 0.001));
                float roadVisibility = step(randomValue, roadDensity);

                float cameraDistance = distance(root, _WorldSpaceCameraPos);
                float distanceDensity = 1.0 - saturate(
                    (cameraDistance - _FadeStart)
                    / max(_FadeEnd - _FadeStart, 0.001));
                float distanceVisibility = step(
                    Hash11(randomValue + 13.73),
                    distanceDensity);
                float visibility = roadVisibility * distanceVisibility;

                float taper = lerp(1.0, 0.18, heightMask);
                float3 positionWS = root;
                positionWS.xz += side * sideSign * bladeWidth * taper;
                positionWS.y += bladeHeight * heightMask;

                float windPhase = (
                    root.x * 0.91
                    + root.z * 0.63
                    + _Time.y * _WindSpeed) * _WindScale;
                float2 windDirection = normalize(float2(0.8, 0.35));
                float windWave = sin(windPhase) * 0.65
                               + sin(windPhase * 1.83 + randomValue * 5.0) * 0.35;
                positionWS.xz += windDirection
                              * windWave
                              * _WindStrength
                              * heightMask;
                positionWS.xz += side * lean * 0.12 * heightMask;

                float2 away = root.xz - _GrassInteractorPosition.xz;
                float interactorDistance = length(away);
                float interaction = 1.0 - smoothstep(
                    0.0,
                    max(_GrassInteractionRadius, 0.001),
                    interactorDistance);
                float2 interactionDirection = interactorDistance > 0.001
                    ? away / interactorDistance
                    : float2(0.0, 0.0);
                positionWS.xz += interactionDirection
                              * interaction
                              * _GrassInteractionStrength
                              * heightMask;

                Light mainLight = GetMainLight();
                float3 fakeNormal = normalize(float3(side.x, 0.65, side.y));
                half diffuse = saturate(dot(fakeNormal, mainLight.direction))
                             * 0.35h + 0.65h;
                half3 healthyColor = lerp(
                    _BaseColor.rgb,
                    _TipColor.rgb,
                    heightMask);
                half3 bladeColor = lerp(
                    healthyColor,
                    _DryColor.rgb,
                    dryVariation);

                Varyings output;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.color = half4(
                    bladeColor * diffuse * mainLight.color,
                    1.0h);
                output.visibility = visibility;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.visibility - 0.5h);
                half3 color = MixFog(input.color.rgb, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
