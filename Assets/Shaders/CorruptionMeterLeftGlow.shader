Shader "CyberVeil/UI/Corruption Meter Left Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Glow Intensity", Range(0, 4)) = 1.35
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 1.25
        _PulseAmount ("Pulse Amount", Range(0, 0.75)) = 0.12
        _CoreSize ("Core Size", Range(0.05, 1)) = 0.38
        _RayStrength ("Ray Strength", Range(0, 2)) = 0.72

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "CorruptionMeterLeftGlow"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _Intensity;
            float _PulseSpeed;
            float _PulseAmount;
            float _CoreSize;
            float _RayStrength;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 p = (input.texcoord - 0.5) * 2.0;
                float diamond = abs(p.x) * 0.58 + abs(p.y);
                float aura = pow(saturate(1.0 - diamond), 2.0);
                float horizontalRay = exp2(-abs(p.y) * 20.0) * exp2(-abs(p.x) * 2.4);
                float verticalRay = exp2(-abs(p.x) * 30.0) * exp2(-abs(p.y) * 5.0);
                float core = smoothstep(_CoreSize, _CoreSize * 0.24, diamond);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                float energy = (aura + _RayStrength * (horizontalRay + verticalRay * 0.55) + core * 1.8)
                    * _Intensity * pulse;

                fixed4 color = fixed4(input.color.rgb * energy, saturate(energy) * input.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
