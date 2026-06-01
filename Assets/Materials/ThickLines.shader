Shader "MinecraftEngine/ThickLines"
{
    Properties
    {
        _Color ("Line Color", Color) = (0, 0, 0, 0.4)
        _Thickness ("Line Thickness (Pixels)", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
            };

            float4 _Color;
            float _Thickness;

            v2g vert (appdata v)
            {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // Geometry Shader: Превращает линию (2 точки) в прямоугольник (4 точки) 
            // с толщиной ровно _Thickness пикселей экрана.
            [maxvertexcount(4)]
            void geom(line v2g input[2], inout TriangleStream<g2f> outStream)
            {
                float2 p0 = input[0].pos.xy / input[0].pos.w;
                float2 p1 = input[1].pos.xy / input[1].pos.w;

                float2 dir = p1 - p0;
                float len = length(dir);
                if (len < 0.0001) return;

                dir /= len;

                // Перпендикуляр к линии в пространстве экрана
                float2 normal = float2(-dir.y, dir.x);

                // Корректируем по соотношению сторон экрана (_ScreenParams.xy)
                normal.x *= _ScreenParams.y / _ScreenParams.x;

                // Сдвиг на половину толщины (в координатах нормализованного экрана [-1, 1])
                float2 offset = normal * (_Thickness / _ScreenParams.y);

                g2f o;

                // Вершина 1
                o.pos = input[0].pos;
                o.pos.xy += offset * o.pos.w;
                outStream.Append(o);

                // Вершина 2
                o.pos = input[0].pos;
                o.pos.xy -= offset * o.pos.w;
                outStream.Append(o);

                // Вершина 3
                o.pos = input[1].pos;
                o.pos.xy += offset * o.pos.w;
                outStream.Append(o);

                // Вершина 4
                o.pos = input[1].pos;
                o.pos.xy -= offset * o.pos.w;
                outStream.Append(o);

                outStream.RestartStrip();
            }

            fixed4 frag (g2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}