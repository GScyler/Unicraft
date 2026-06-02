Shader "MinecraftEngine/ThickLines"
{
    Properties
    {
        _Color     ("Line Color", Color) = (0, 0, 0, 0.4)
        _Thickness ("Line Thickness (Pixels)", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "ThickLines"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct VertToGeo
            {
                float4 positionHCS : SV_POSITION;
            };

            struct GeoToFrag
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Thickness;
            CBUFFER_END

            VertToGeo vert(Attributes IN)
            {
                VertToGeo OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            [maxvertexcount(4)]
            void geom(line VertToGeo input[2], inout TriangleStream<GeoToFrag> outStream)
            {
                float2 p0 = input[0].positionHCS.xy / input[0].positionHCS.w;
                float2 p1 = input[1].positionHCS.xy / input[1].positionHCS.w;

                float2 dir = p1 - p0;
                float len = length(dir);
                if (len < 0.0001) return;
                dir /= len;

                float2 normal = float2(-dir.y, dir.x);
                normal.x *= _ScreenParams.y / _ScreenParams.x;

                float2 offset = normal * (_Thickness / _ScreenParams.y);

                GeoToFrag o;

                o.positionHCS = input[0].positionHCS;
                o.positionHCS.xy += offset * o.positionHCS.w;
                outStream.Append(o);

                o.positionHCS = input[0].positionHCS;
                o.positionHCS.xy -= offset * o.positionHCS.w;
                outStream.Append(o);

                o.positionHCS = input[1].positionHCS;
                o.positionHCS.xy += offset * o.positionHCS.w;
                outStream.Append(o);

                o.positionHCS = input[1].positionHCS;
                o.positionHCS.xy -= offset * o.positionHCS.w;
                outStream.Append(o);

                outStream.RestartStrip();
            }

            half4 frag(GeoToFrag IN) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
