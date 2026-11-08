// The other hand's notes: a slab drawn only as its edge, with nothing inside.
//
// Drawn on the same unit cube as a solid note. The edge width is in metres, so
// a long note's edge is as thick as a short one's however the cube is stretched:
// the object's scale is read from its matrix, and the position in the face is
// turned into metres from that. Only the face toward the player is drawn.
Shader "PianoGlass/NoteOutline"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _EdgeWidth ("Edge width (metres)", Float) = 0.0028
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _EdgeWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 metres     : TEXCOORD0;
                float2 halfSize   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4x4 m = GetObjectToWorldMatrix();
                float2 size = float2(length(float3(m[0][0], m[1][0], m[2][0])),
                                     length(float3(m[0][1], m[1][1], m[2][1])));

                o.metres = v.positionOS.xy * size;
                o.halfSize = size * 0.5;

                // The cube's thin side faces would only double the edge.
                o.positionCS = v.normalOS.z > 0.5 ? TransformObjectToHClip(v.positionOS.xyz) : float4(0, 0, 0, 0);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 fromEdge = i.halfSize - abs(i.metres);
                float d = min(fromEdge.x, fromEdge.y);
                float aa = max(fwidth(d), 1e-6);

                // Never thinner than about a pixel, so a far note doesn't shimmer.
                float width = max(_EdgeWidth, aa * 1.5);
                float alpha = saturate(d / aa) * saturate((width - d) / aa);
                if (alpha <= 0.0) discard;
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
