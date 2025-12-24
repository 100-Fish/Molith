Shader "GBCamera/GBPalette" {
    Properties {
        [PerRendererData]_MainTex ("MainTex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Palette ("Palette", 2D) = "white" {}
        _DarkPalette ("Dark Palette", 2D) = "white" {}
        _PaletteSize ("Palette Size", Float) = 32
        _PaletteShift ("Palette Shift", Range(-31, 31)) = 0
        _Darkness ("Darkness", Range(0, 1)) = 0
        _PixelSize ("Pixel Size", Float) = 2
        [Toggle] _UseDarkPalette ("Use Dark Palette", Float) = 0
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }
        Pass {
            Name "FORWARD"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"
            #pragma multi_compile_fwdbase
            #pragma exclude_renderers metal d3d11_9x xbox360 xboxone ps3 ps4 psp2 
            #pragma target 3.0
            uniform sampler2D _MainTex; uniform float4 _MainTex_ST;
            uniform float4 _Color;
            uniform sampler2D _Palette; uniform float4 _Palette_ST;
            uniform sampler2D _DarkPalette; uniform float4 _DarkPalette_ST;
            uniform float _PaletteSize;
            uniform float _PaletteShift;
            uniform float _Darkness;
            uniform float _PixelSize;
            uniform float _UseDarkPalette;
            struct VertexInput {
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float4 vertexColor : COLOR;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 vertexColor : COLOR;
                float4 screenPos : TEXCOORD1;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.vertexColor = v.vertexColor;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                #ifdef PIXELSNAP_ON
                    o.pos = UnityPixelSnap(o.pos);
                #endif
                return o;
            }
            
            float getLuminance(float3 color) {
                return dot(color, float3(0.299, 0.587, 0.114));
            }
            
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
                
                float2 screenPos = i.screenPos.xy / i.screenPos.w;
                float2 cameraRes = float2(320.0, 180.0);
                float2 pixelBlockSize = float2(_PixelSize, _PixelSize);
                float2 cameraCoord = floor(screenPos * cameraRes / pixelBlockSize);
                
                const float ditherMatrix[16] = {
                    0.0/16.0, 8.0/16.0, 2.0/16.0, 10.0/16.0,
                    12.0/16.0, 4.0/16.0, 14.0/16.0, 6.0/16.0,
                    3.0/16.0, 11.0/16.0, 1.0/16.0, 9.0/16.0,
                    15.0/16.0, 7.0/16.0, 13.0/16.0, 5.0/16.0
                };
                
                int x = int(cameraCoord.x) % 4;
                int y = int(cameraCoord.y) % 4;
                float threshold = ditherMatrix[y * 4 + x];
                
                float4 _MainTex_var = tex2D(_MainTex, TRANSFORM_TEX(i.uv0, _MainTex));
                float3 sourceColor = _MainTex_var.rgb * _Color.rgb * i.vertexColor.rgb;
                
                float sourceLuminance = getLuminance(sourceColor);
                
                bool applyDarkness = false;
                if (_UseDarkPalette < 0.5 && _Darkness > 0.0) {
                    applyDarkness = _Darkness > threshold;
                    
                    if (applyDarkness) {
                        sourceLuminance = max(0, sourceLuminance * 0.5);
                    }
                }
                
                float bestDiff = 1.0;
                int bestIndex = 0;
                
                for (int j = 0; j < _PaletteSize; j++) {
                    float2 paletteUV = float2((j + 0.5) / _PaletteSize, 0.5);
                    float3 paletteColor = tex2D(_Palette, paletteUV).rgb;
                    
                    float paletteLuminance = getLuminance(paletteColor);
                    
                    float diff = abs(sourceLuminance - paletteLuminance);
                    
                    if (diff < bestDiff) {
                        bestDiff = diff;
                        bestIndex = j;
                    }
                }
                
                int shiftedIndex = bestIndex + _PaletteShift;
                
                if (_UseDarkPalette < 0.5 && applyDarkness) {
                    int darknessShift = 4;
                    shiftedIndex = max(0, shiftedIndex - darknessShift);
                }
                
                if (shiftedIndex >= _PaletteSize) shiftedIndex = shiftedIndex % int(_PaletteSize);
                if (shiftedIndex < 0) shiftedIndex = (_PaletteSize + shiftedIndex % int(_PaletteSize)) % int(_PaletteSize);
                
                shiftedIndex = clamp(shiftedIndex, 0, _PaletteSize - 1);
                
                float2 shiftedUV = float2((shiftedIndex + 0.5) / _PaletteSize, 0.5);
                float3 finalColor;
                
                if (_UseDarkPalette > 0.5) {
                    finalColor = tex2D(_DarkPalette, shiftedUV).rgb;
                } else {
                    finalColor = tex2D(_Palette, shiftedUV).rgb;
                }
                
                return fixed4(finalColor, _MainTex_var.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}