Shader "QuestCameraKit/CameraMapping/StereoPassthroughWavyPortal"
{
    Properties
    {
        _LeftTex("Left Texture", 2D) = "black" {}
        _RightTex("Right Texture", 2D) = "black" {}
        _Tint("Tint", Color) = (0.85,0.95,1,1)
        _TintStrength("Tint Strength", Range(0, 1)) = 0.15
        _LeftUvOffset("Left UV Offset", Vector) = (0,0,0,0)
        _RightUvOffset("Right UV Offset", Vector) = (0,0,0,0)
        _WaveAmplitude("Wave Amplitude", Range(0, 0.05)) = 0.012
        _WaveFrequency("Wave Frequency", Range(1, 40)) = 16
        _WaveSpeed("Wave Speed", Range(0, 8)) = 1.8
        _SwirlAmplitude("Swirl Amplitude", Range(0, 0.05)) = 0.008
        _EdgeFeather("Edge Feather", Range(0.001, 0.2)) = 0.03
        _PreviewEye("Preview Eye (0 Left, 1 Right)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "StereoPassthroughWavyPortal"
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

            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _SwirlAmplitude;
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
                float2 uvOffset = eyeIndex == 0 ? _LeftUvOffset : _RightUvOffset;
                uv += uvOffset;

                float2 center = uv - 0.5;
                float radius = length(center);
                float2 radialDir = radius > 0.0001 ? center / radius : float2(0.0, 0.0);
                float2 tangentDir = float2(-radialDir.y, radialDir.x);

                float time = _Time.y * _WaveSpeed;
                float ripple = sin(radius * _WaveFrequency * 12.0 - time * 6.0);
                float swirl = sin((center.x + center.y) * _WaveFrequency * 8.0 + time * 4.0);

                uv += radialDir * (ripple * _WaveAmplitude);
                uv += tangentDir * (swirl * _SwirlAmplitude);

                if (any(uv < 0.0) || any(uv > 1.0))
                {
                    discard;
                }

                half4 color = eyeIndex == 0
                    ? SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, uv)
                    : SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, uv);

                float edgeMask = smoothstep(0.0, _EdgeFeather, min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)));
                half3 tintedRgb = lerp(color.rgb, color.rgb * _Tint.rgb, _TintStrength);
                return half4(tintedRgb * edgeMask, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
