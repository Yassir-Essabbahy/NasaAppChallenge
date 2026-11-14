Shader "Outpost/PSXTerrain"
{
    Properties
    {
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        _Splat0 ("Sand (R)", 2D) = "white" {}
        _Splat1 ("Rock (G)", 2D) = "white" {}
        _Splat2 ("Dust (B)", 2D) = "white" {}
        _Splat3 ("Extra (A)", 2D) = "black" {}
        _Color ("Global Tint", Color) = (1, 1, 1, 1)
        _AffineAmount ("Affine Amount", Range(0, 1)) = 0.35
        _FogWeight ("Fog Weight", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-100"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PSXTerrainForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvControl  : TEXCOORD0;
                float2 uvSplat0   : TEXCOORD1;
                float2 uvSplat1   : TEXCOORD2;
                float2 uvSplat2   : TEXCOORD3;
                float3 vertexLit  : TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
            };

            TEXTURE2D(_Control);
            TEXTURE2D(_Splat0);
            TEXTURE2D(_Splat1);
            TEXTURE2D(_Splat2);
            SAMPLER(my_point_repeat_sampler);
            SAMPLER(sampler_Control);

            CBUFFER_START(UnityPerMaterial)
                float4 _Control_ST;
                float4 _Splat0_ST;
                float4 _Splat1_ST;
                float4 _Splat2_ST;
                half4 _Color;
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
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS = TransformWorldToHClip(posWS);
                posCS = ApplyVertexSnap(posCS);
                OUT.positionCS = posCS;

                OUT.uvControl = TRANSFORM_TEX(IN.uv, _Control);
                OUT.uvSplat0 = TRANSFORM_TEX(IN.uv, _Splat0);
                OUT.uvSplat1 = TRANSFORM_TEX(IN.uv, _Splat1);
                OUT.uvSplat2 = TRANSFORM_TEX(IN.uv, _Splat2);

                half3 lit = max(half3(unity_AmbientSky.rgb), half3(0.16h, 0.10h, 0.08h));
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                lit += half3(mainLight.color) * (ndotl * 0.72h + 0.14h);
                OUT.vertexLit = lit * _Color.rgb;

                float distCam = length(_WorldSpaceCameraPos.xyz - posWS);
                float activeDensity = _PSX_FogDensity > 0.0001 ? _PSX_FogDensity : (unity_FogParams.x * 0.8325546);
                float fogExp = exp(-pow(distCam * activeDensity, 2.0));
                OUT.fogFactor = saturate((1.0 - fogExp) * _FogWeight);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 ctrl = SAMPLE_TEXTURE2D(_Control, sampler_Control, IN.uvControl);
                half weightSum = ctrl.r + ctrl.g + ctrl.b;
                half3 weights = weightSum > 0.001h ? (ctrl.rgb / weightSum) : half3(1.0h, 0.0h, 0.0h);

                half3 col0 = SAMPLE_TEXTURE2D(_Splat0, my_point_repeat_sampler, IN.uvSplat0).rgb;
                half3 col1 = SAMPLE_TEXTURE2D(_Splat1, my_point_repeat_sampler, IN.uvSplat1).rgb;
                half3 col2 = SAMPLE_TEXTURE2D(_Splat2, my_point_repeat_sampler, IN.uvSplat2).rgb;

                half3 albedo = col0 * weights.r + col1 * weights.g + col2 * weights.b;
                half3 col = albedo * IN.vertexLit;

                half3 fogCol = _PSX_FogDensity > 0.0001 ? half3(_PSX_FogColor.rgb) : half3(unity_FogColor.rgb);
                col = lerp(col, fogCol, half(IN.fogFactor));
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
