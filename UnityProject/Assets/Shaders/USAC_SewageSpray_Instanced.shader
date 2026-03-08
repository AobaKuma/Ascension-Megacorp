Shader "USAC/SewageSpray_Instanced"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
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
            #pragma target 4.5
            #pragma only_renderers d3d11 glcore vulkan metal
            
            #include "UnityCG.cginc"

            struct Particle
            {
                float3 position;
                float3 velocity;
                float life;
                float maxLife;
                float size;
                float3 color;
                float mass;
            };

            #if SHADER_TARGET >= 45
            StructuredBuffer<Particle> particleBuffer;
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float mass : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                
                #if SHADER_TARGET >= 45
                Particle p = particleBuffer[v.instanceID];
                
                if (p.life <= 0)
                {
                    o.vertex = float4(0, 0, 0, 0);
                    o.uv = float2(0, 0);
                    o.color = float4(0, 0, 0, 0);
                    o.mass = 0;
                    return o;
                }
                
                float3 worldPos = p.position;
                float3 velocity = p.velocity;
                float speed = length(velocity.xz);
                float mass = p.mass;
                
                float stretchFactor = lerp(1.0, 1.5, mass);
                float stretch = 1.0 + speed * 0.06 * stretchFactor;
                
                float width = p.size;
                float lengthScale = p.size * stretch;
                
                float2 dir = (speed > 0.01) ? normalize(velocity.xz) : float2(0, 1);
                
                float noiseAngle = (frac(sin(v.instanceID * 123.456) * 43758.5453) * 2.0 - 1.0) * 0.1;
                float cosA = cos(noiseAngle);
                float sinA = sin(noiseAngle);
                dir = float2(dir.x * cosA - dir.y * sinA, dir.x * sinA + dir.y * cosA);
                
                float2 localPos = v.vertex.xy;
                float x = localPos.x * width;
                float y = localPos.y * lengthScale;
                
                float2 right = float2(dir.y, -dir.x);
                float2 rotatedPos = x * right + y * dir;
                
                worldPos.x += rotatedPos.x;
                worldPos.z += rotatedPos.y;
                worldPos.y += lerp(0.3, -0.1, mass);
                
                float4 pos = float4(worldPos, 1.0);
                o.vertex = mul(UNITY_MATRIX_VP, pos);
                
                float lifeRatio = 1.0 - (p.life / p.maxLife);
                float fadeIn = smoothstep(0.0, 0.015, lifeRatio);
                float fadeOut = pow(saturate(1.0 - lifeRatio), 1.3); 
                
                float massAlpha = (mass > 0.8) ? 0.8 : lerp(0.01, 0.6, pow(saturate(mass / 0.8), 1.1));
                float alpha = fadeIn * fadeOut * massAlpha;
                
                o.color = float4(p.color, alpha * _Color.a);
                o.mass = mass;
                
                #else
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = float4(1,0,0,1);
                o.mass = 0;
                #endif

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (i.color.a < 0.01) discard;
                float dist = length(i.uv - 0.5);
                float shadow = lerp(0.7, 1.0, smoothstep(0.0, 0.45, dist));
                float3 shadedColor = i.color.rgb * shadow;
                fixed4 col = tex2D(_MainTex, i.uv) * float4(shadedColor, i.color.a);
                float edgeSoftness = lerp(0.48, 0.2, i.mass);
                float circle = smoothstep(0.5, edgeSoftness, dist);
                col.a *= circle;
                return col;
            }
            ENDCG
        }
    }
}
