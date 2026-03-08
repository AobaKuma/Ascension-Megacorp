Shader "USAC/SewageSpray"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.35, 0.45, 0.25, 0.9)
        _Speed ("Flow Speed", Float) = 2.0
        _NoiseScale ("Noise Scale", Float) = 3.0
        _Distortion ("Distortion Strength", Range(0, 1)) = 0.2
        _Alpha ("Alpha Multiplier", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma only_renderers d3d11 glcore vulkan metal
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Speed;
            float _NoiseScale;
            float _Distortion;
            float _Alpha;

            float hash(float2 p) {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float res = lerp(lerp(hash(i), hash(i + float2(1.0, 0.0)), f.x),
                               lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), f.x), f.y);
                return res;
            }

            v2f vert (appdata v)
            {
                v2f o;
                float taper = lerp(0.4, 1.2, pow(v.uv.y, 0.6));
                float4 pos = v.vertex;
                float centeredX = (v.uv.x - 0.5) * 2.0;
                pos.x = centeredX * taper * 0.5;
                o.vertex = UnityObjectToClipPos(pos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 flowUV = i.uv;
                flowUV.y -= _Time.y * _Speed * 2.0;
                float n1 = noise(flowUV * _NoiseScale);
                float n2 = noise(flowUV * _NoiseScale * 2.0 + float2(10.0, 5.0));
                float n3 = noise(flowUV * _NoiseScale * 0.5 - float2(3.0, 7.0));
                float liquid = (n1 * 0.5 + n2 * 0.3 + n3 * 0.2);
                float distFromCenter = abs(i.uv.x - 0.5) * 2.0;
                float coreDensity = 1.0 - pow(distFromCenter, 1.5);
                float heightFactor = 1.0 - pow(i.uv.y, 1.2);
                float edgeSpray = liquid * (1.0 - heightFactor) * (1.0 - coreDensity * 0.5);
                float baseAlpha = coreDensity * heightFactor;
                float sprayAlpha = edgeSpray * 0.8;
                float finalAlpha = saturate(baseAlpha + sprayAlpha);
                float tipFade = smoothstep(0.0, 0.05, i.uv.y) * smoothstep(1.0, 0.85, i.uv.y);
                float edgeFade = smoothstep(0.5, 0.3, distFromCenter);
                fixed4 col = _Color * i.color;
                col.rgb = lerp(col.rgb, col.rgb * 1.3 + 0.15, i.uv.y * 0.5);
                col.rgb += liquid * 0.15;
                col.a *= finalAlpha * tipFade * edgeFade * _Alpha;
                return col;
            }
            ENDCG
        }
    }
}
