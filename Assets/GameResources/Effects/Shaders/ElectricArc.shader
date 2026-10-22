Shader "TX2D/Effects/ElectricArc"
{
    Properties
    {
        _MainTex ("Electric Arc Atlas (4 x 2)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Elapsed ("Effect Time", Float) = 0
        _Fade ("Opacity", Range(0,1)) = 1
        _Seed ("Instance Seed", Float) = 1
        _BodySize ("Body Size", Vector) = (1.3,1.75,0,0)
        _ArcSize ("Arc Size / Body Height", Float) = 0.34
        _Interval ("Flash Interval", Float) = 0.25
        _FlashSeconds ("Flash Duration", Float) = 0.1
        _Facing ("Facing", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;
            float _Elapsed, _Fade, _Seed, _ArcSize, _Interval, _FlashSeconds, _Facing;
            float4 _BodySize;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 localPoint = (i.uv - 0.5) * _BodySize.xy;
                localPoint.x *= _Facing;
                float3 light = 0;
                float interval = max(0.04, _Interval);
                [unroll]
                for (int slot = 0; slot < 3; slot++)
                {
                    float t = _Elapsed + slot * interval * 0.29 + hash(_Seed) * interval;
                    float tick = floor(t / interval);
                    float phase = t - tick * interval;
                    float visible = max(1 - step(min(_FlashSeconds, interval * 0.8), phase), 1 - step(0.12, _Elapsed));
                    float n = _Seed + slot * 43 + tick * 19;
                    // Distribute arcs over upper body, torso and legs rather than bunching them together.
                    float2 anchor = float2((hash(n + 1) - 0.5) * 0.52, (slot - 1) * 0.25 + (hash(n + 2) - 0.5) * 0.09);
                    anchor *= _BodySize.xy;
                    float angle = (hash(n + 3) - 0.5) * 0.8;
                    float s = sin(angle), c = cos(angle);
                    float size = max(0.01, _BodySize.y * _ArcSize * lerp(0.8, 1.15, hash(n + 4)));
                    float rotatedExtent = abs(c) + abs(s);
                    size = min(size, min(_BodySize.x, _BodySize.y) / rotatedExtent);
                    float2 anchorLimit = max(0, _BodySize.xy * 0.5 - size * rotatedExtent * 0.5);
                    anchor = clamp(anchor, -anchorLimit, anchorLimit);
                    float2 delta = localPoint - anchor;
                    float2 uv = float2(c * delta.x - s * delta.y, s * delta.x + c * delta.y);
                    uv /= size;
                    uv.x *= hash(n + 5) < 0.5 ? -1 : 1;
                    uv += 0.5;
                    float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                    float tile = floor(hash(n + 6) * 8);
                    // Unity UVs start at the bottom; the source grid is numbered from the top.
                    float2 cell = float2(fmod(tile, 4), 1 - floor(tile / 4));
                    fixed4 arc = tex2D(_MainTex, (cell + clamp(uv, 0.002, 0.998)) / float2(4, 2));
                    light += arc.rgb * arc.a * inside * visible;
                }
                return fixed4(light * _Tint.rgb * _Tint.a * _Fade, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
