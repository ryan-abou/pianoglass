// Liquid Glass: the one material every menu and panel is drawn on.
//
// A single quad per panel (GlassPanel builds it). uv0 is the fragment's
// position from the panel centre in metres; _HalfSize and _Radius describe
// the rounded rectangle, which is a signed-distance function rather than
// geometry, as in HighwayGrid.shader -- so corners are exact at any size
// and the edge is anti-aliased from fwidth.
//
// Layers, back to front:
//   tint      translucent body, a little brighter towards the top (_TopGlow)
//   rim       bright band just inside the edge, strongest along the top
//             and fading to _RimBottom of that at the bottom
//   sheen     Fresnel: brighter the more edge-on the panel is seen
//
// Per-panel values (_HalfSize, _Radius) and the GlassTokens values are set
// through a MaterialPropertyBlock by GlassPanel, so the token asset is the
// one place to tune them. Drawn just before the Transparent queue so the
// Canvas text in front of it always lands on top.
Shader "PianoGlass/LiquidGlass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.07, 0.09, 0.12, 0.62)
        _TopGlow ("Top glow", Float) = 0.06
        _RimColor ("Rim colour", Color) = (1, 1, 1, 0.85)
        _RimWidth ("Rim width (m)", Float) = 0.0022
        _RimBottom ("Rim at bottom (fraction of top)", Float) = 0.25
        _SheenColor ("Sheen colour", Color) = (1, 1, 1, 1)
        _SheenStrength ("Sheen strength", Float) = 0.16
        _SheenPower ("Sheen power", Float) = 3
        _HalfSize ("Half size (m)", Vector) = (0.3, 0.3, 0, 0)
        _Radius ("Corner radius (m)", Float) = 0.022
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _TopGlow;
                float4 _RimColor;
                float _RimWidth;
                float _RimBottom;
                float4 _SheenColor;
                float _SheenStrength;
                float _SheenPower;
                float4 _HalfSize;
                float _Radius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 local      : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local      : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                // A Canvas reads with its +z away from the viewer, so the
                // face the player sees has normal -z.
                o.normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));
                o.local = v.local;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 halfSize = _HalfSize.xy;
                float2 px = max(fwidth(i.local), 1e-6);
                float aa = max(px.x, px.y);

                float r = min(_Radius, min(halfSize.x, halfSize.y));
                float2 q = abs(i.local) - halfSize + r;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;   // < 0 inside
                float inside = saturate(0.5 - d / aa);

                // 0 at the bottom edge, 1 at the top.
                float y01 = saturate(i.local.y / max(halfSize.y, 1e-5) * 0.5 + 0.5);

                float rimWidth = max(_RimWidth, aa);
                float rim = saturate(1.0 + d / rimWidth) * inside;          // 1 at the edge, 0 rimWidth in
                rim *= lerp(_RimBottom, 1.0, y01);

                float ndv = abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS)));
                float sheen = pow(saturate(1.0 - ndv), _SheenPower) * _SheenStrength;

                float3 col = _Tint.rgb + _TopGlow * y01;
                float alpha = _Tint.a;

                float rimA = rim * _RimColor.a;
                col = lerp(col, _RimColor.rgb, rimA);
                alpha = max(alpha, rimA);

                col += _SheenColor.rgb * sheen;
                alpha = saturate(alpha + sheen * 0.5);

                return half4(col, alpha * inside);
            }
            ENDHLSL
        }
    }
}
