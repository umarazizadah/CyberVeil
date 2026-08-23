Shader "CyberVeil/UI/Veil Card Energy"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EnergyColor ("Energy Color", Color) = (0.05,0.82,1,1)
        _SecondaryColor ("Secondary Color", Color) = (0.23,0.02,0.55,1)
        _Mode ("Mode", Float) = 0
        _FocusAmount ("Focus", Range(0,1)) = 0
        _ConfirmationAmount ("Confirmation", Range(0,2)) = 0
        _EffectIntensity ("Effect Intensity", Range(0,2)) = 1
        _EdgeIntensity ("Edge Intensity", Range(0,3)) = 1.15
        _SweepIntensity ("Sweep Intensity", Range(0,2)) = 0.65
        _ParticleIntensity ("Particle Intensity", Range(0,2)) = 0.45
        _PurifyBoost ("Purify Emission Boost", Range(1,6)) = 5
        _PulseSpeed ("Pulse Speed", Range(0,8)) = 2.4
        _Distortion ("Distortion", Range(0,1)) = 0.35
        _UnscaledTime ("Unscaled Time", Float) = 0

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "VeilCardEnergy"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _EnergyColor;
            fixed4 _SecondaryColor;
            float4 _ClipRect;
            float _Mode;
            float _FocusAmount;
            float _ConfirmationAmount;
            float _EffectIntensity;
            float _EdgeIntensity;
            float _SweepIntensity;
            float _ParticleIntensity;
            float _PurifyBoost;
            float _PulseSpeed;
            float _Distortion;
            float _UnscaledTime;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float corrupt = step(0.5, _Mode);
                float focus = saturate(_FocusAmount);
                float confirmation = max(0.0, _ConfirmationAmount);
                float time = _UnscaledTime;

                float irregular = sin(time * (_PulseSpeed * 0.83 + 0.2)) * 0.52
                    + sin(time * (_PulseSpeed * 1.67 + 0.35) + 1.7) * 0.31
                    + sin(time * (_PulseSpeed * 2.31 + 0.5) + 4.1) * 0.17;
                irregular = irregular * 0.5 + 0.5;

                float distortionWave = sin(uv.y * 47.0 + time * 5.1)
                    * sin(uv.x * 31.0 - time * 3.7);
                float2 distortedUv = uv;
                distortedUv.x += corrupt * focus * _Distortion * distortionWave * 0.0035;

                fixed4 sprite = tex2D(_MainTex, distortedUv) + _TextureSampleAdd;
                float alpha = sprite.a;
                float2 texel = _MainTex_TexelSize.xy * 1.6;

                float alphaRight = tex2D(_MainTex, distortedUv + float2(texel.x, 0)).a;
                float alphaLeft = tex2D(_MainTex, distortedUv - float2(texel.x, 0)).a;
                float alphaUp = tex2D(_MainTex, distortedUv + float2(0, texel.y)).a;
                float alphaDown = tex2D(_MainTex, distortedUv - float2(0, texel.y)).a;
                float neighbourAlpha = min(min(alphaRight, alphaLeft), min(alphaUp, alphaDown));
                float neighbourMax = max(max(alphaRight, alphaLeft), max(alphaUp, alphaDown));
                float silhouetteEdge = saturate((alpha - neighbourAlpha) * 6.0);
                float outerEdge = saturate((neighbourMax - alpha) * 4.0);

                float luminance = dot(sprite.rgb, float3(0.299, 0.587, 0.114));
                float brightFrame = smoothstep(0.42, 0.92, luminance) * alpha * 0.32;
                float edgeMask = max(silhouetteEdge, brightFrame);

                float edgeTravel = 0.42 + 0.58 * sin(
                    (uv.x * 1.25 + uv.y * 2.1) * 13.0 - time * (2.2 + _PulseSpeed));
                edgeTravel = smoothstep(0.08, 0.92, edgeTravel);
                float focusEnergy = 0.18 + focus * 0.82;
                float corruptPulse = lerp(1.0, lerp(0.62, 1.45, irregular), corrupt);
                float edgeEnergy = edgeMask * edgeTravel * focusEnergy * corruptPulse * _EdgeIntensity;

                float sweepPhase = frac(uv.x * 0.72 + uv.y * 0.28 - time * 0.12);
                float cleanSweep = pow(saturate(1.0 - abs(sweepPhase - 0.5) * 18.0), 3.0);
                cleanSweep *= (1.0 - corrupt) * focus * alpha * _SweepIntensity;

                float veinA = abs(sin(uv.x * 55.0 + sin(uv.y * 21.0 + time * 0.35) * 4.2));
                float veinB = abs(sin(uv.y * 63.0 - sin(uv.x * 17.0 - time * 0.27) * 3.7));
                float veins = smoothstep(0.965, 0.998, veinA * veinB);
                veins *= corrupt * alpha * (0.18 + focus * 0.62 + confirmation * 0.8);

                float topCrystal = smoothstep(0.58, 0.94, uv.y)
                    * (1.0 - smoothstep(0.07, 0.30, abs(uv.x - 0.5)));
                float crawlingWisp = smoothstep(0.76, 0.99,
                    sin(uv.y * 36.0 + uv.x * 17.0 - time * (3.0 + _PulseSpeed)) * 0.5 + 0.5);
                float leak = topCrystal * crawlingWisp * corrupt * alpha
                    * (0.25 + focus * 0.55 + confirmation * 0.85);
                float outsideLeak = topCrystal * crawlingWisp * corrupt * (1.0 - alpha)
                    * (focus * 0.32 + confirmation * 0.48);

                float2 particleGrid = uv * float2(13.0, 23.0);
                float2 particleCell = floor(particleGrid);
                float2 particleLocal = frac(particleGrid) - 0.5;
                float randomValue = Hash21(particleCell);
                float twinkle = sin(time * (4.0 + randomValue * 5.0) + randomValue * 9.0) * 0.5 + 0.5;
                float particles = smoothstep(0.11, 0.015, length(particleLocal))
                    * step(0.87, randomValue) * twinkle * alpha * focus * _ParticleIntensity;

                float chromaAmount = corrupt * focus * _Distortion * (0.22 + irregular * 0.38);
                fixed shiftedRed = tex2D(_MainTex, distortedUv + float2(texel.x * 1.8, 0)).r;
                fixed shiftedBlue = tex2D(_MainTex, distortedUv - float2(texel.x * 1.8, 0)).b;
                sprite.r = lerp(sprite.r, shiftedRed, chromaAmount);
                sprite.b = lerp(sprite.b, shiftedBlue, chromaAmount);

                fixed3 energyColor = lerp(_EnergyColor.rgb, _SecondaryColor.rgb,
                    corrupt * saturate(uv.y * 0.7 + irregular * 0.45));
                float surge = 1.0 + confirmation * (0.7 + irregular * 0.8);
                float visibilityBoost = lerp(_PurifyBoost, 1.0, corrupt);
                fixed3 effect = energyColor * (edgeEnergy + veins + leak + particles) * surge;
                effect += _EnergyColor.rgb * cleanSweep;
                float outerGlow = outerEdge * focus * _EdgeIntensity * (0.12 + corrupt * irregular * 0.18);
                effect = effect * (_EffectIntensity * alpha * visibilityBoost)
                    + energyColor * (outerGlow + outsideLeak) * _EffectIntensity * visibilityBoost;

                fixed4 color;
                color.rgb = (sprite.rgb + effect) * input.color.rgb;
                color.a = max(alpha, saturate(outerGlow + outsideLeak)) * input.color.a;

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
