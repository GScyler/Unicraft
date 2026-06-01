Shader "MinecraftEngine/BlockCracks"
{
    Properties
    {
        _MainTex ("Crack Texture Array", 2DArray) = "white" {}
        // Индекс стадии разрушения (0-9)
        _Stage ("Crack Stage", Float) = 0
    }
    SubShader
    {
        // Очередь Transparent, чтобы рисовать трещины поверх геометрии чанков
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            // Offset позволяет рисовать трещины ЧУТЬ БЛИЖЕ к камере, 
            // чтобы они не конфликтовали (Z-Fighting) с поверхностью самого блока
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            float _Stage;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Формируем 3D-координату для массива текстур (UV + Индекс стадии)
                float3 uv3 = float3(i.uv.x, i.uv.y, _Stage);
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, uv3);
                
                // Трещины в Minecraft черные, прозрачность берем из текстуры.
                // В архиве текстур разрушения (destroy_stage) прозрачный фон и серые пиксели трещин.
                clip(col.a - 0.1); 

                // Возвращаем пиксель текстуры (с его цветом)
                return col;
            }
            ENDCG
        }
    }
}