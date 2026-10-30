// The highway grid: lane lines, beat and bar lines, the hit line and the
// bar-number tag backgrounds, all drawn by HighwayGrid as quads in one mesh.
//
// Every quad is a rounded rectangle described by a signed-distance function,
// not by its geometry: uv0 is the fragment's position relative to the
// rectangle's centre (metres), uv1 is (half width, half height, corner
// radius). The quad itself is a little larger than the rectangle so the
// anti-aliased edge has room to fade out.
//
// Thin lines: a line thinner than about a pixel shimmers and breaks up on a
// Quest 2 as the head moves. So each half-extent is widened to at least
// MinPixels worth of screen space (from fwidth), and the alpha is scaled
// down by the same ratio -- the line keeps a stable on-screen width and
// fades rather than flickering when it is far away.
Shader "PianoGlass/HighwayGrid"
{
    Properties
    {
        _MinPixels ("Minimum half-width (pixels)", Float) = 0.75
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

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
                float _MinPixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 local      : TEXCOORD0;
                float3 rect       : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 local      : TEXCOORD0;
                float3 rect       : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.local = v.local;
                o.rect = v.rect;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 px = max(fwidth(i.local), 1e-6);            // metres per pixel, per axis
                float2 half_ = i.rect.xy;
                float2 widened = max(half_, px * _MinPixels);
                float coverage = (half_.x / widened.x) * (half_.y / widened.y);

                float r = min(i.rect.z, min(widened.x, widened.y));
                float2 q = abs(i.local) - widened + r;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;

                float aa = max(px.x, px.y);
                float alpha = saturate(0.5 - d / aa) * coverage;
                return half4(i.color.rgb, i.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
