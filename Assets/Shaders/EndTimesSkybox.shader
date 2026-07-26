Shader "GMTK/End Times Procedural Sky"
{
    Properties
    {
        [HDR] _ZenithColor ("Zenith Color", Color) = (0.30, 0.20, 0.48, 1)
        [HDR] _MiddleColor ("Middle Color", Color) = (0.58, 0.25, 0.45, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.82, 0.31, 0.30, 1)
        _HorizonSoftness ("Horizon Softness", Range(0.1, 1.5)) = 0.65

        [HDR] _SunColor ("Sun Color", Color) = (1.65, 0.85, 0.48, 1)
        _SunDirection ("Sun Direction", Vector) = (0, 0.35, 0.94, 0)
        _SunSize ("Sun Size", Range(0.001, 0.08)) = 0.018
        _SunGlow ("Sun Glow", Range(0, 1.5)) = 0.55
        _SunGlowFalloff ("Sun Glow Falloff", Range(1, 32)) = 8

        [HDR] _CloudColor ("Cloud Color", Color) = (0.30, 0.17, 0.38, 1)
        [HDR] _CloudLightColor ("Cloud Light Color", Color) = (0.74, 0.39, 0.51, 1)
        _CloudScale ("Cloud Scale", Range(1, 12)) = 4.4
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.48
        _CloudSoftness ("Cloud Softness", Range(0.03, 0.4)) = 0.15
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.62
        _CloudSpeed ("Cloud Speed", Range(0, 1)) = 0.035
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirection : TEXCOORD0;
            };

            float4 _ZenithColor;
            float4 _MiddleColor;
            float4 _HorizonColor;
            float _HorizonSoftness;
            float4 _SunColor;
            float4 _SunDirection;
            float _SunSize;
            float _SunGlow;
            float _SunGlowFalloff;
            float4 _CloudColor;
            float4 _CloudLightColor;
            float _CloudScale;
            float _CloudCoverage;
            float _CloudSoftness;
            float _CloudOpacity;
            float _CloudSpeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.viewDirection = mul((float3x3)unity_ObjectToWorld, input.vertex.xyz);
                return output;
            }

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise(float3 value)
            {
                float3 cell = floor(value);
                float3 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float lower = lerp(lerp(n000, n100, local.x), lerp(n010, n110, local.x), local.y);
                float upper = lerp(lerp(n001, n101, local.x), lerp(n011, n111, local.x), local.y);
                return lerp(lower, upper, local.z);
            }

            float CloudNoise(float3 value)
            {
                float noise = 0.0;
                noise += ValueNoise(value) * 0.55;
                value = value * 2.03 + 11.7;
                noise += ValueNoise(value) * 0.28;
                value = value * 2.07 + 19.1;
                noise += ValueNoise(value) * 0.17;
                return noise;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.viewDirection);
                float vertical = direction.y;

                float horizonToMiddle = smoothstep(
                    -0.18,
                    max(0.12, _HorizonSoftness),
                    vertical);
                float middleToZenith = smoothstep(0.18, 0.92, vertical);
                float3 sky = lerp(_HorizonColor.rgb, _MiddleColor.rgb, horizonToMiddle);
                sky = lerp(sky, _ZenithColor.rgb, middleToZenith);

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunAlignment = saturate(dot(direction, sunDirection));
                float distanceFromSun = 1.0 - sunAlignment;
                float sunDisc = 1.0 - smoothstep(_SunSize * 0.3, _SunSize, distanceFromSun);
                float sunGlow = pow(sunAlignment, _SunGlowFalloff) * _SunGlow;
                sky += _SunColor.rgb * (sunDisc + sunGlow);

                float timeOffset = _Time.y * _CloudSpeed;
                float3 cloudPosition = direction * _CloudScale;
                cloudPosition += float3(timeOffset, timeOffset * 0.23, -timeOffset * 0.72);
                float cloudNoise = CloudNoise(cloudPosition);
                float cloudThreshold = 1.0 - _CloudCoverage;
                float clouds = smoothstep(
                    cloudThreshold - _CloudSoftness,
                    cloudThreshold + _CloudSoftness,
                    cloudNoise);
                clouds *= smoothstep(-0.12, 0.16, vertical) * _CloudOpacity;

                float cloudSunLight = pow(sunAlignment, 5.0) * clouds;
                float3 cloudColor = lerp(_CloudColor.rgb, _CloudLightColor.rgb, cloudSunLight);
                sky = lerp(sky, cloudColor, clouds);

                float horizonHaze = exp(-abs(vertical) * 8.0) * 0.13;
                sky += _HorizonColor.rgb * horizonHaze;

                float dither = Hash31(float3(input.positionCS.xy, _Time.y)) - 0.5;
                sky += dither / 255.0;
                return half4(max(sky, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
}
