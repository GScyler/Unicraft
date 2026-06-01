Shader "MinecraftEngine/UI_TextureArrayShader"
{
    Properties
    {
        _MainTex ("Texture Array", 2DArray) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "LightMode"="ForwardBase" }
        LOD 100

        ZWrite On
        Cull Back
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc" // ДОБАВЛЕНО ДЛЯ ОСВЕЩЕНИЯ

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL; // Принимаем нормали от Unity
                float3 uv : TEXCOORD0; 
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;
                float3 diffuse : COLOR; // Передаем рассчитанный свет во фрагментный шейдер
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                
                // --- РАСЧЕТ ОСВЕЩЕНИЯ (Lambert) ---
                // Получаем мировую нормаль
                half3 worldNormal = UnityObjectToWorldNormal(v.normal);
                
                // _WorldSpaceLightPos0 - это вектор направления нашего источника света (UILight)
                half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
                
                // Базовый Ambient цвет (чтобы в тени куб не был абсолютно черным)
                o.diffuse = float3(0.4, 0.4, 0.4) + _LightColor0.rgb * nl * 0.8;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);
                clip(col.a - 0.1); 
                
                // Умножаем текстуру на рассчитанный свет
                col.rgb *= i.diffuse;
                
                return col;
            }
            ENDCG
        }
    }
}