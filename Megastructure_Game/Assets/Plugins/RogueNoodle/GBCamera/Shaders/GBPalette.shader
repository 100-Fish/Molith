Shader "GBCamera/GBPalette" {
    Properties {
        [PerRendererData]_MainTex ("MainTex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Palette ("Palette", 2D) = "white" {}
        _PaletteSize ("Palette Size", Float) = 32
        _PaletteShift ("Palette Shift", Range(-31, 31)) = 0
        _PixelSize ("Pixel Size", Float) = 2
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
            uniform float _PaletteSize;
            uniform float _PaletteShift;
            uniform float _PixelSize;
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
            
            float colorDistanceSq(float3 c1, float3 c2) {
                float3 diff = c1 - c2;
                return dot(diff, diff);
            }
            
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
                
                float4 _MainTex_var = tex2D(_MainTex, TRANSFORM_TEX(i.uv0, _MainTex));
                float3 sourceColor = _MainTex_var.rgb * _Color.rgb * i.vertexColor.rgb;
                
                float bestDistSq = 1000000.0;
                int bestIndex = 0;
                
                for (int j = 0; j < _PaletteSize; j++) {
                    float2 paletteUV = float2((j + 0.5) / _PaletteSize, 0.5);
                    float3 paletteColor = tex2D(_Palette, paletteUV).rgb;
                    
                    float distSq = colorDistanceSq(sourceColor, paletteColor);
                    
                    if (distSq < bestDistSq) {
                        bestDistSq = distSq;
                        bestIndex = j;
                    }
                }
                
                int shiftedIndex = bestIndex + _PaletteShift;
                
                if (shiftedIndex >= _PaletteSize) shiftedIndex = shiftedIndex % int(_PaletteSize);
                if (shiftedIndex < 0) shiftedIndex = (_PaletteSize + shiftedIndex % int(_PaletteSize)) % int(_PaletteSize);
                
                shiftedIndex = clamp(shiftedIndex, 0, _PaletteSize - 1);
                
                float2 shiftedUV = float2((shiftedIndex + 0.5) / _PaletteSize, 0.5);
                float3 finalColor = tex2D(_Palette, shiftedUV).rgb;
                
                return fixed4(finalColor, _MainTex_var.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
