Shader "Outpost/PSXParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Float) = 0.0
        _CutoutThreshold ("Cutout Threshold", Range(0, 1)) = 0.0
        _AffineAmount ("Affine Amount", Range(0, 1)) = 0.0
        _FogWeight ("Fog Weight", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PSXParticleForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(my_point_clamp_sampler);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _EmissionColor;
                half _EmissionStrength;
                half _FogWeight;
            CBUFFER_END

            float _PSX_Enabled;
            float _PSX_VertexSnapRes;
            float4 _PSX_FogColor;
            float _PSX_FogDensity;

            float4 ApplyVertexSnap(float4 clipPos)
            {
                if (_PSX_Enabled < 0.5 || _PSX_VertexSnapRes < 1.0)
                    return clipPos;

                float w = abs(clipPos.w) > 0.0001 ? clipPos.w : 0.0001;
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 grid = float2(_PSX_VertexSnapRes * aspect, _PSX_VertexSnapRes) * 0.5;
                float2 ndc = clipPos.xy / w;
                ndc = floor(ndc * grid + 0.5) / grid;
                clipPos.xy = ndc * w;
                return clipPos;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float4 posCS = TransformWorldToHClip(posWS);
                OUT.positionCS = ApplyVertexSnap(posCS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = half4(IN.color) * _Color;

                float distCam = length(_WorldSpaceCameraPos.xyz - posWS);
                float fogExp = exp(-pow(distCam * _PSX_FogDensity, 2.0));
                OUT.fogFactor = saturate((1.0 - fogExp) * _FogWeight);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, my_point_clamp_sampler, IN.uv);
                half3 col = tex.rgb * IN.color.rgb + _EmissionColor.rgb * _EmissionStrength;
                col = lerp(col, half3(_PSX_FogColor.rgb), half(IN.fogFactor));
                return half4(col, tex.a * IN.color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
