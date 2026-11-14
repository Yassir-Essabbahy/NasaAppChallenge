Shader "Outpost/PSXSkyGradient"
{
    Properties
    {
        _HorizonColor ("Horizon Color", Color) = (0.64, 0.36, 0.25, 1)
        _MidColor ("Mid Sky Color", Color) = (0.76, 0.44, 0.34, 1)
        _ZenithColor ("Zenith Color", Color) = (0.38, 0.23, 0.28, 1)
        _GroundColor ("Ground Color", Color) = (0.48, 0.25, 0.17, 1)
        _SunColor ("Sun Color", Color) = (1.0, 0.88, 0.72, 1)
        _SunDir ("Sun Direction", Vector) = (0.58, 0.34, 0.74, 0)
        _SunSize ("Sun Disc Radius", Range(0.004, 0.08)) = 0.014
        _SunGlow ("Sun Glow Softness", Range(0.01, 0.35)) = 0.09
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "PSXSkyboxPass"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _MidColor;
                half4 _ZenithColor;
                half4 _GroundColor;
                half4 _SunColor;
                float4 _SunDir;
                half _SunSize;
                half _SunGlow;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.viewDirWS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.viewDirWS);
                float h = dir.y;

                half3 col;
                if (h >= 0.0)
                {
                    half tMid = saturate(h / 0.26);
                    half tTop = saturate((h - 0.18) / 0.72);
                    col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0h, 1.0h, tMid));
                    col = lerp(col, _ZenithColor.rgb, smoothstep(0.0h, 1.0h, tTop));
                }
                else
                {
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-h * 4.0));
                }

                float3 sunDir = normalize(_SunDir.xyz);
                float sunDot = dot(dir, sunDir);
                float sunAngle = acos(clamp(sunDot, -1.0, 1.0));
                float disc = 1.0 - smoothstep(_SunSize * 0.75, _SunSize, sunAngle);
                float glow = exp(-max(sunAngle, 0.0) / max(_SunGlow, 0.01)) * 0.45;
                col += _SunColor.rgb * half(disc * 1.15 + glow);

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
