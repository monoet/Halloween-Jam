Shader "Halloween/SpriteGlow"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor("Glow Color", Color) = (1,1,1,1)
        _GlowIntensity("Glow Intensity", Range(0,5)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        AlphaToMask Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float4 _Color;
            float4 _GlowColor;
            float _GlowIntensity;

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 baseColor = sampled * input.color * _Color;

                half3 glow = _GlowColor.rgb * _GlowIntensity * baseColor.a;
                half3 rgb = baseColor.rgb + glow;
                half alpha = baseColor.a;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
