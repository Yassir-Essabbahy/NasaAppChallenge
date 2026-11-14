Shader "Outpost/PSXUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Float) = 1.0
        _CutoutThreshold ("Cutout Threshold", Range(0, 1)) = 0.0
        _AffineAmount ("Affine Amount", Range(0, 1)) = 0.5
        _FogWeight ("Fog Weight", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PSXUnlitForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
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
                float2 uvPersp    : TEXCOORD0;
                float3 uvAffineW  : TEXCOORD1;
                half4  vertColor  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            TEXTURE2D(_EmissionMap);
            SAMPLER(my_point_repeat_sampler);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _EmissionMap_ST;
                half4 _Color;
                half4 _EmissionColor;
                half _EmissionStrength;
                half _CutoutThreshold;
                half _AffineAmount;
                half _FogWeight;
            CBUFFER_END

            float _PSX_Enabled;
            float _PSX_VertexSnapRes;
            float _PSX_AffineAmount;
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
                posCS = ApplyVertexSnap(posCS);
                OUT.positionCS = posCS;

                float2 uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uvPersp = uv;
                float w = max(abs(posCS.w), 0.001);
                OUT.uvAffineW = float3(uv * w, w);
                OUT.vertColor = half4(IN.color) * _Color;

                float distCam = length(_WorldSpaceCameraPos.xyz - posWS);
                float fogExp = exp(-pow(distCam * _PSX_FogDensity, 2.0));
                OUT.fogFactor = saturate((1.0 - fogExp) * _FogWeight);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 affineUV = IN.uvAffineW.xy / max(IN.uvAffineW.z, 0.001);
                float affineWeight = saturate(_AffineAmount * _PSX_AffineAmount * _PSX_Enabled);
                float2 uv = lerp(IN.uvPersp, affineUV, affineWeight);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, my_point_repeat_sampler, uv);
                half alpha = tex.a * IN.vertColor.a;
                if (_CutoutThreshold > 0.001h)
                    clip(alpha - _CutoutThreshold);

                half3 emisTex = SAMPLE_TEXTURE2D(_EmissionMap, my_point_repeat_sampler, uv).rgb;
                half3 col = tex.rgb * IN.vertColor.rgb + emisTex * _EmissionColor.rgb * _EmissionStrength;
                col = lerp(col, half3(_PSX_FogColor.rgb), half(IN.fogFactor));
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
