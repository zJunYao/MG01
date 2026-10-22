Shader "TX2D/Effects/DeathShatter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Death Progress", Range(0,1)) = 0
        _Particle ("Particle Layer", Float) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _EdgeColor ("Crack Light", Color) = (0.65,0.24,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Progress, _Particle;
            fixed4 _Tint, _EdgeColor;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 centerSeed : TEXCOORD1;
                float4 cell : TEXCOORD2;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 cell : TEXCOORD1;
                float age : TEXCOORD2;
                float seed : TEXCOORD3;
            };
            v2f vert(appdata v)
            {
                v2f o;
                float seed = v.centerSeed.z;
                float age = saturate((_Progress - 0.20 - seed * 0.08) / 0.72);
                float2 center = v.centerSeed.xy;
                float2 direction = normalize(center - float2(0,0.78) + float2(0.013,0.03));
                float speed = lerp(0.7, 1.6, v.centerSeed.w) * lerp(1,1.15,_Particle);
                float2 offset = direction * speed * age;
                offset.y += 0.48 * age - 0.65 * age * age;
                float angle = (seed - 0.5) * 4.0 * age;
                float s = sin(angle), c = cos(angle);
                float2 delta = v.vertex.xy - center;
                delta = float2(c*delta.x-s*delta.y,s*delta.x+c*delta.y);
                delta *= 1 - smoothstep(0.35,1,age) * 0.92;
                v.vertex.xy = center + delta + offset;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cell = v.cell;
                o.age = age;
                o.seed = seed;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Tint;
                float crack = smoothstep(0.055,0.19,_Progress);
                float2 distanceToEdge = min(i.cell.xy, 1-i.cell.xy) * i.cell.zw;
                float edgeDistance = min(distanceToEdge.x,distanceToEdge.y);
                float edge = 1-step(1.25,edgeDistance);
                float flash = (1-smoothstep(0.015,0.11,_Progress))*0.80;
                col.rgb = lerp(col.rgb,float3(0.88,0.72,1),flash * (1-_Particle));
                col.rgb = lerp(col.rgb,_EdgeColor.rgb,edge*crack*(1-i.age)*(1-_Particle));
                // One source pixel of separation along each cell edge.
                col.a *= lerp(1,step(crack*0.8,edgeDistance),1-_Particle);
                float fade = 1-smoothstep(0.35 + i.seed*0.2,1,i.age);
                col.a *= fade * (1-step(0.999,_Progress));
                col.a *= lerp(1,smoothstep(0.14,0.28,_Progress),_Particle);
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
}
