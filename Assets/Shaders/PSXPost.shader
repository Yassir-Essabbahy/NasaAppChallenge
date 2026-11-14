Shader "Outpost/PSXPost"
{
    Properties
    {
        [PerRendererData] _MainTex ("World RenderTexture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "PSXPostPass"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(my_point_clamp_sampler);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                half4 _Color;
            CBUFFER_END

            float _PSX_Enabled;
            float _PSX_ColorLevels;
            float _PSX_DitherStrength;
            float _PSX_GrainAmount;
            float _PSX_ReduceFlicker;

            float Bayer4x4(int ix, int iy)
            {
                int x = ix & 3;
                int y = iy & 3;
                int v = (((x ^ y) & 1) << 3)
                      | ((y & 1) << 2)
                      | ((((x >> 1) ^ (y >> 1)) & 1) << 1)
                      | ((y >> 1) & 1);
                return (float(v) / 16.0) - 0.5;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = half4(IN.color) * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 src = SAMPLE_TEXTURE2D(_MainTex, my_point_clamp_sampler, IN.uv) * IN.color;
                if (_PSX_Enabled < 0.5)
                    return src;

                float2 rtDims = _MainTex_TexelSize.zw;
                if (rtDims.x < 16.0 || rtDims.y < 16.0)
                    rtDims = float2(480.0, 270.0);

                float2 px = floor(IN.uv * rtDims);
                int ix = (int)px.x;
                int iy = (int)px.y;

                float grain = 0.0;
                if (_PSX_GrainAmount > 0.0001 && _PSX_ReduceFlicker < 0.5)
                {
                    float frameTick = floor(_Time.y * 20.0);
                    float n = frac(sin(dot(px + frameTick * 17.3, float2(12.9898, 78.233))) * 43758.5453);
                    grain = (n - 0.5) * _PSX_GrainAmount;
                }

                float levels = max(4.0, _PSX_ColorLevels);
                float dither = Bayer4x4(ix, iy) * (_PSX_DitherStrength / levels);
                half3 col = src.rgb + half(grain) + half(dither);
                col = saturate(floor(col * half(levels - 1.0) + 0.5h) / half(levels - 1.0));

                return half4(col, src.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
