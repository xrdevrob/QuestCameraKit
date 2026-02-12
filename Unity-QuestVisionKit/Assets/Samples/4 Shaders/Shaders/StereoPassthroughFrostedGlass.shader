Shader "QuestCameraKit/CameraMapping/StereoPassthroughFrostedGlass"
{
    Properties
    {
        _LeftTex("Left Texture", 2D) = "black" {}
        _RightTex("Right Texture", 2D) = "black" {}
        _Tint("Tint", Color) = (0.92,0.96,1,1)
        _TintStrength("Tint Strength", Range(0, 1)) = 0.22
        _LeftUvOffset("Left UV Offset", Vector) = (0,0,0,0)
        _RightUvOffset("Right UV Offset", Vector) = (0,0,0,0)
        _BlurRadius("Blur Radius", Range(0.0, 0.02)) = 0.006
        _RefractionNoiseScale("Refraction Noise Scale", Range(10, 250)) = 120
        _RefractionNoiseAmount("Refraction Noise Amount", Range(0.0, 0.01)) = 0.0025
        _EdgeFeather("Edge Feather", Range(0.001, 0.2)) = 0.03
        _PreviewEye("Preview Eye (0 Left, 1 Right)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "StereoPassthroughFrostedGlass"
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LeftTex);
            SAMPLER(sampler_LeftTex);
            TEXTURE2D(_RightTex);
            SAMPLER(sampler_RightTex);

            float4 _Tint;
            float _TintStrength;
            float _PreviewEye;
            float _BlurRadius;
            float _RefractionNoiseScale;
            float _RefractionNoiseAmount;
            float _EdgeFeather;

            float3 _LeftCameraPos;
            float3 _RightCameraPos;
            float4x4 _LeftCameraRotationMatrix;
            float4x4 _RightCameraRotationMatrix;

            float2 _LeftFocalLength;
            float2 _RightFocalLength;
            float2 _LeftPrincipalPoint;
            float2 _RightPrincipalPoint;
            float2 _LeftSensorResolution;
            float2 _RightSensorResolution;
            float2 _LeftCurrentResolution;
            float2 _RightCurrentResolution;
            float2 _LeftUvOffset;
            float2 _RightUvOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            float4 ProjectToViewport(
                float3 worldPos,
                float3 cameraPos,
                float4x4 inverseCameraRotation,
                float2 focalLength,
                float2 principalPoint,
                float2 sensorResolution,
                float2 currentResolution)
            {
                float3 localPos = mul(inverseCameraRotation, float4(worldPos - cameraPos, 1.0)).xyz;
                if (localPos.z <= 0.0001)
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                float2 sensorPoint = float2(
                    (localPos.x / localPos.z) * focalLength.x + principalPoint.x,
                    (localPos.y / localPos.z) * focalLength.y + principalPoint.y);

                float2 scaleFactor = currentResolution / sensorResolution;
                scaleFactor /= max(scaleFactor.x, scaleFactor.y);

                float2 cropMin = sensorResolution * (1.0 - scaleFactor) * 0.5;
                float2 cropSize = sensorResolution * scaleFactor;
                float2 uv = (sensorPoint - cropMin) / cropSize;

                return float4(uv, localPos.z, 1.0);
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 SampleEye(int eyeIndex, float2 uv)
            {
                return eyeIndex == 0
                    ? SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, uv)
                    : SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, uv);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                int eyeIndex = 0;
                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) || defined(UNITY_SINGLE_PASS_STEREO)
                    eyeIndex = unity_StereoEyeIndex;
                #else
                    eyeIndex = (int)round(saturate(_PreviewEye));
                #endif

                float4 projected = eyeIndex == 0
                    ? ProjectToViewport(
                        IN.worldPos,
                        _LeftCameraPos,
                        _LeftCameraRotationMatrix,
                        _LeftFocalLength,
                        _LeftPrincipalPoint,
                        _LeftSensorResolution,
                        _LeftCurrentResolution)
                    : ProjectToViewport(
                        IN.worldPos,
                        _RightCameraPos,
                        _RightCameraRotationMatrix,
                        _RightFocalLength,
                        _RightPrincipalPoint,
                        _RightSensorResolution,
                        _RightCurrentResolution);

                if (projected.w < 0.5)
                {
                    discard;
                }

                float2 uv = projected.xy;
                uv += (eyeIndex == 0) ? _LeftUvOffset : _RightUvOffset;

                if (any(uv < 0.0) || any(uv > 1.0))
                {
                    discard;
                }

                float t = _Time.y * 0.45;
                float noiseA = hash12(uv * _RefractionNoiseScale + t);
                float noiseB = hash12((uv.yx + 1.73) * (_RefractionNoiseScale * 0.77) - t);
                float2 jitter = (float2(noiseA, noiseB) - 0.5) * (2.0 * _RefractionNoiseAmount);

                static const float2 kOffsets[9] = {
                    float2(0.0, 0.0),
                    float2(1.0, 0.0), float2(-1.0, 0.0),
                    float2(0.0, 1.0), float2(0.0, -1.0),
                    float2(0.7, 0.7), float2(-0.7, 0.7),
                    float2(0.7, -0.7), float2(-0.7, -0.7)
                };

                half3 accum = half3(0, 0, 0);
                float totalWeight = 0.0;

                [unroll]
                for (int i = 0; i < 9; i++)
                {
                    float w = (i == 0) ? 2.0 : 1.0;
                    float2 sampleUv = uv + kOffsets[i] * _BlurRadius + jitter;

                    if (all(sampleUv >= 0.0) && all(sampleUv <= 1.0))
                    {
                        accum += SampleEye(eyeIndex, sampleUv).rgb * w;
                        totalWeight += w;
                    }
                }

                if (totalWeight <= 0.0)
                {
                    discard;
                }

                half3 blurred = accum / totalWeight;
                half3 frosted = lerp(blurred, blurred * _Tint.rgb, _TintStrength);

                float edgeDistance = min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y));
                float edgeMask = smoothstep(0.0, _EdgeFeather, edgeDistance);

                return half4(frosted * edgeMask, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
