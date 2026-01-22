Shader "Custom/FadeDistanceFromCamera" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _FadeStart ("Fade Start Distance", Float) = 0
        _FadeDis ("Fade End Distance", Float) = 10
        _DitherSize ("Dither Size", Float) = 1
    }

    SubShader {
        Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
        LOD 200

        CGPROGRAM

        #pragma surface surf Lambert addshadow
        #pragma target 3.0

        struct Input {
            float2 uv_MainTex;
            float3 worldPos;
            float4 screenPos;
        };

        sampler2D _MainTex;
        half _FadeStart;
        half _FadeDis;
        half _DitherSize;

        // 4x4 Bayer dither matrix
        static const float4x4 ditherMatrix = float4x4(
             0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
            12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
             3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
            15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
        );

        void surf (Input IN, inout SurfaceOutput o) {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;

            // Calculate per-pixel distance from camera
            float dist = distance(_WorldSpaceCameraPos, IN.worldPos);
            float fade = saturate(1.0 - ((dist - _FadeStart) / (_FadeDis - _FadeStart)));

            // Get screen position for dither pattern
            float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
            float2 ditherCoord = screenUV * _ScreenParams.xy / _DitherSize;
            int2 ditherIndex = int2(fmod(ditherCoord.x, 4), fmod(ditherCoord.y, 4));
            float ditherThreshold = ditherMatrix[ditherIndex.x][ditherIndex.y];

            // Discard pixel if below dither threshold
            clip(fade - ditherThreshold);

            o.Alpha = 1.0;
        }

        ENDCG
    }
    Fallback "Diffuse"
}
