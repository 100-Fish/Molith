Shader "FX/Hologram"
{
    Properties
    {
        _Color("Color", Color) = (0, 1, 1, 1)
        _MainTex("Base (RGB)", 2D) = "white" {}
        _AlphaTexture ("Alpha Mask (R)", 2D) = "white" {}
        _Scale ("Alpha Tiling", Float) = 3
        _ScrollSpeedV("Alpha Scroll Speed", Range(0, 5.0)) = 1.0
        _GlowIntensity ("Glow Intensity", Range(0.01, 1.0)) = 0.5
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.8
        _FrostAmount ("Frost Amount", Range(0.0, 1.0)) = 0.5
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.03
        _OutlineOpacity ("Outline Opacity", Range(0.0, 1.0)) = 0.9
    }

    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        // Outline Pass (draw first, behind main object)
        Pass
        {
            Name "OUTLINE"
            Lighting Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front

            CGPROGRAM
                #pragma vertex vertOutline
                #pragma fragment fragOutline
                #include "UnityCG.cginc"

                fixed4 _Color;
                half _OutlineWidth;
                half _OutlineOpacity;

                struct appdata {
                    float4 vertex : POSITION;
                    float3 normal : NORMAL;
                };

                struct v2f {
                    float4 position : SV_POSITION;
                };

                v2f vertOutline(appdata v) {
                    v2f o;
                    // Expand vertices along their normals
                    float3 norm = normalize(v.normal);
                    float3 expanded = v.vertex.xyz + norm * _OutlineWidth;
                    o.position = UnityObjectToClipPos(float4(expanded, 1.0));
                    return o;
                }

                fixed4 fragOutline(v2f i) : SV_Target {
                    fixed4 col = _Color;
                    col.a = _OutlineOpacity;
                    return col;
                }
            ENDCG
        }

        // Main hologram Pass
        Pass
        {
            Lighting Off
            ZWrite Off
            Blend SrcAlpha One
            Cull Back

            CGPROGRAM

                #pragma vertex vertexFunc
                #pragma fragment fragmentFunc

                #include "UnityCG.cginc"

                struct appdata{
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float3 normal : NORMAL;
                };

                struct v2f{
                    float4 position : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float3 grabPos : TEXCOORD1;
                    float3 viewDir : TEXCOORD2;
                    float3 worldNormal : NORMAL;
                };

                fixed4 _Color, _MainTex_ST;
                sampler2D _MainTex, _AlphaTexture;
                half _Scale, _ScrollSpeedV, _GlowIntensity, _Opacity, _FrostAmount;

                v2f vertexFunc(appdata IN){
                    v2f OUT;

                    OUT.position = UnityObjectToClipPos(IN.vertex);
                    OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                    // Alpha mask coordinates
                    OUT.grabPos = UnityObjectToViewPos(IN.vertex);

                    // Scroll Alpha mask uv
                    OUT.grabPos.y += _Time * _ScrollSpeedV;

                    OUT.worldNormal = UnityObjectToWorldNormal(IN.normal);
                    OUT.viewDir = normalize(UnityWorldSpaceViewDir(OUT.grabPos.xyz));

                    return OUT;
                }

                fixed4 fragmentFunc(v2f IN) : SV_Target{

                    fixed4 alphaColor = tex2D(_AlphaTexture,  IN.grabPos.xy * _Scale);
                    fixed4 pixelColor = tex2D (_MainTex, IN.uv);

                    // Rim Light for frosted glass edge effect
                    half rim = 1.0 - saturate(dot(IN.viewDir, IN.worldNormal));
                    half rimEffect = pow(rim, 2.0) * _FrostAmount;

                    // Combine base color with rim and glow
                    fixed4 finalColor = _Color * (1.0 + rimEffect + _GlowIntensity);

                    // Apply scrolling alpha pattern with base opacity
                    finalColor.a = alphaColor.a * _Opacity;

                    return finalColor;
                }
            ENDCG
        }
    }
}
