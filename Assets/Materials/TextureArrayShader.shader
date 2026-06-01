Shader "MinecraftEngine/TextureArrayShader"
{
    Properties
    {
        _MainTex ("Texture Array", 2DArray) = "white" {}
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1 
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0 
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1 
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }
        LOD 100

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            // Отключаем влияние Unity-света на этот шейдер, мы используем запеченный воксельный свет
            Lighting Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0; 
                float4 color : COLOR; // ДОБАВЛЕНО: Цвет воксельного света
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);
                
                clip(col.a - 0.1); 

                if (i.uv.z == 12.0) 
                {
                    col.a = 0.7;
                }

                // Умножаем пиксель текстуры на уровень света (и затенение грани)
                col.rgb *= i.color.rgb;

                return col;
            }
            ENDCG
        }
    }
}