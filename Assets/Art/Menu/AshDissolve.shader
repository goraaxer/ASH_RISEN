Shader "Ashrisen/UI Ash Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Image", 2D) = "white" {}
        _NoiseTex ("Ash noise", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _EmberColor ("Ember edge", Color) = (0.88,0.32,0.08,1)
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Burn width", Range(0.001,0.3)) = 0.085
        _NoiseScale ("Noise scale", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmberColor;
            float _Dissolve;
            float _EdgeWidth;
            float _NoiseScale;
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * _NoiseScale).r;
                half threshold = saturate(_Dissolve);
                half visible = smoothstep(threshold - 0.025, threshold + 0.025, noise);
                half ember = smoothstep(0, _EdgeWidth, noise - threshold) *
                    (1 - smoothstep(_EdgeWidth, _EdgeWidth * 1.8, noise - threshold));
                ember *= smoothstep(0.02, 0.12, threshold) *
                    (1 - smoothstep(0.88, 0.98, threshold));
                baseColor.rgb = lerp(baseColor.rgb, _EmberColor.rgb, ember * 0.55)
                    + _EmberColor.rgb * ember * 0.65;
                baseColor.a *= visible;
                return baseColor;
            }
            ENDHLSL
        }
    }
}
