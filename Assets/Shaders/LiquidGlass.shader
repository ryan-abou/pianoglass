// Liquid Glass v2: the one material every menu, row and pill is drawn on.
//
// Reference: Ryan's images of 2026-11-03 -- Apple's WWDC25 glass (thick lens
// edge, bright rims both sides, rainbow fringe), iOS 26 grouped settings
// (nested frosted rows), ElementsKit's card (bright hairline border).
//
// The Quest 2 cannot do the one thing those images rely on most: blurring
// and bending what is behind. Passthrough is composited by the system under
// the app and cannot be read. So the glass is made of everything else, all
// arithmetic, no textures:
//
//   body      frosted tint, lighter towards the top
//   bevel     a thick-glass edge: a lens profile from the SDF, lit from
//             _LightDir, with a fainter inner reflection on the far side
//   hairline  a crisp bright line just inside the edge
//   sheen     a soft diagonal highlight, plus Fresnel at grazing angles;
//             both slide with the view direction, so the glass reads as a
//             physical object when the head moves
//   chroma    a faint rainbow fringe along the bottom edge
//
// Output is PREMULTIPLIED (Blend One OneMinusSrcAlpha): the Quest
// compositor shows   rgb + passthrough * (1 - alpha).   The body darkens the
// room by its alpha; highlights are added on top as light, the way a
// reflection on real glass brightens what is behind it. (v1 blended alpha
// with SrcAlpha too, so a 0.62 body wrote 0.38 alpha and the room showed
// through far more than intended.)
//
// uv0 is the fragment's position from the shape's centre in metres;
// _HalfSize and _Radius describe the rounded rectangle (a pill when the
// radius is half the height). Set per surface by GlassPanel through a
// MaterialPropertyBlock, from GlassTokens.
Shader "PianoGlass/LiquidGlass"
{
    Properties
    {
        _Tint ("Tint (alpha = coverage)", Color) = (0.10, 0.12, 0.15, 0.56)
        _TopGlow ("Top glow", Float) = 0.07
        _BevelWidth ("Bevel width (m)", Float) = 0.009
        _BevelLight ("Bevel light", Float) = 1.0
        _BackLight ("Back light", Float) = 0.35
        _LightDir ("Light direction (xy)", Vector) = (-0.41, 0.91, 0, 0)
        _Parallax ("Parallax", Float) = 0.8
        _HairlineColor ("Hairline", Color) = (1, 1, 1, 0.55)
        _HairlineWidth ("Hairline width (m)", Float) = 0.0006
        _SheenStrength ("Sheen", Float) = 0.10
        _FresnelStrength ("Fresnel", Float) = 0.14
        _ChromaStrength ("Chroma fringe", Float) = 0.35
        _HalfSize ("Half size (m)", Vector) = (0.3, 0.3, 0, 0)
        _Radius ("Corner radius (m)", Float) = 0.028
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Unlit"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _TopGlow;
                float _BevelWidth;
                half _BevelLight;
                half _BackLight;
                float4 _LightDir;
                half _Parallax;
                half4 _HairlineColor;
                float _HairlineWidth;
                half _SheenStrength;
                half _FresnelStrength;
                half _ChromaStrength;
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
                // xy: view direction in the panel's plane (right, up);
                // z: |N.V|, how face-on the panel is seen.
                float3 view       : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.local = v.local;

                // From the surface towards the eye, in object space. A Canvas
                // reads with +z away from the viewer, so the visible face's
                // normal is -z, and object x/y are the panel's right/up.
                float3 toEye = normalize(TransformWorldToObjectDir(GetWorldSpaceViewDir(positionWS)));
                o.view = float3(toEye.xy, abs(toEye.z));
                return o;
            }

            // Cheap rainbow: three phase-shifted cosines.
            half3 Spectrum(half x)
            {
                return saturate(half3(0.5, 0.5, 0.5) + half3(0.5, 0.5, 0.5) *
                                cos(6.28318 * (x + half3(0.0, 0.33, 0.67))));
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 p = i.local;
                float2 halfSize = _HalfSize.xy;
                float2 px = max(fwidth(p), 1e-6);
                float aa = max(px.x, px.y);

                // Rounded-rectangle SDF (< 0 inside) and its outward normal.
                float r = min(_Radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - halfSize + r;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float2 s = float2(p.x < 0 ? -1.0 : 1.0, p.y < 0 ? -1.0 : 1.0);
                float2 n = (q.x > 0.0 && q.y > 0.0) ? normalize(q) * s
                         : (q.x > q.y ? float2(s.x, 0.0) : float2(0.0, s.y));

                half coverage = saturate(0.5 - d / aa);
                if (coverage <= 0.0) discard;

                float inward = -d;                                    // metres in from the edge
                half y01 = saturate(p.y / max(halfSize.y, 1e-5) * 0.5 + 0.5);

                // The light slides against the head's movement: parallax.
                float2 light = normalize(_LightDir.xy - i.view.xy * _Parallax);

                // Thick-glass edge: 1 at the rim falling to 0 one bevel in,
                // curved like a lens. Lit where the edge faces the light,
                // with a fainter inner reflection on the opposite side.
                half e = saturate(1.0 - inward / max(_BevelWidth, aa));
                half lens = e * e * e;
                half facing = dot(n, light);
                half bevel = lens * (saturate(facing) + _BackLight * saturate(-facing)) * _BevelLight;

                // Hairline just inside the edge, never thinner than a pixel.
                float hw = max(_HairlineWidth, aa);
                half hair = saturate(inward / aa + 0.5) * saturate((hw - inward) / aa + 0.5);

                // Soft diagonal highlight across the body, sliding with the view.
                float2 diag = normalize(float2(1.0, 0.6));
                float along = dot(p / max(halfSize, 1e-4), diag) - dot(i.view.xy, diag) * _Parallax * 1.5;
                half sheen = exp(-along * along * 6.0) * _SheenStrength * (1.0 - e);
                half fresnel = pow(saturate(1.0 - i.view.z), 3.0) * _FresnelStrength;

                // Rainbow fringe on the lower lens edge, hue running along x.
                half lower = saturate(-n.y);
                half chroma = lens * lower * lower * _ChromaStrength;
                half3 fringe = Spectrum(p.x / max(halfSize.x, 1e-4) * 0.5 + 0.5) * chroma;

                // Body: premultiplied tint, lighter towards the top.
                half a = _Tint.a;
                half3 body = (_Tint.rgb + _TopGlow * y01) * a;

                // Light added on top (premultiplied: brightens what is behind).
                half3 light3 = (bevel + sheen + fresnel) * half3(1, 1, 1)
                             + _HairlineColor.rgb * (hair * _HairlineColor.a)
                             + fringe;
                half alpha = saturate(a + hair * _HairlineColor.a * 0.5 + bevel * 0.25);

                return half4((body + light3) * coverage, alpha * coverage);
            }
            ENDHLSL
        }
    }
}
