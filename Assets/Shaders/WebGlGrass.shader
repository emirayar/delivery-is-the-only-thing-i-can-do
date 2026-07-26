Shader "GMTK/WebGL Mesh Grass"
{
    Properties
    {
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.18
        _WindScale ("Wind Scale", Float) = 0.16
        _WindSpeed ("Wind Speed", Float) = 1.4
        _GrassInteractionRadius ("Interaction Radius", Float) = 2.2
        _GrassInteractionStrength ("Interaction Strength", Float) = 1.15
        _FadeStart ("Fade Start", Float) = 75
        _FadeEnd ("Fade End", Float) = 115
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WindStrength;
                float _WindScale;
                float _WindSpeed;
                float3 _GrassInteractorPosition;
                float _GrassInteractionRadius;
                float _GrassInteractionStrength;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                half visibility : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float heightMask = input.uv.y;

                float windPhase = (
                    positionWS.x * 0.91
                    + positionWS.z * 0.63
                    + _Time.y * _WindSpeed) * _WindScale;
                float windWave = sin(windPhase) * 0.65
                               + sin(windPhase * 1.83) * 0.35;
                positionWS.xz += normalize(float2(0.8, 0.35))
                               * windWave
                               * _WindStrength
                               * heightMask;

                float2 away = positionWS.xz - _GrassInteractorPosition.xz;
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

                float cameraDistance = distance(positionWS, _WorldSpaceCameraPos);
                float density = 1.0 - saturate(
                    (cameraDistance - _FadeStart)
                    / max(_FadeEnd - _FadeStart, 0.001));

                Light mainLight = GetMainLight();
                half diffuse = saturate(mainLight.direction.y) * 0.3h + 0.7h;

                Varyings output;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.color = half4(
                    input.color.rgb * diffuse * mainLight.color,
                    1.0h);
                output.visibility = step(
                    Hash21(positionWS.xz),
                    density);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.visibility - 0.5h);
                return half4(MixFog(input.color.rgb, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }
    }
}
