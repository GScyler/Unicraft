Shader "MinecraftEngine/UI_TextureArrayShader"
{
    Properties
    {
        _MainTex ("Texture Array", 2DArray) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "UIBlockLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float3 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 uv          : TEXCOORD0;
                float3 diffuse     : TEXCOORD1;
            };

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.uv = IN.uv;

                VertexNormalInputs vni = GetVertexNormalInputs(IN.normalOS);
                float3 worldNormal = vni.normalWS;

                Light mainLight = GetMainLight();

                half nl = saturate(dot(worldNormal, mainLight.direction));

                OUT.diffuse = float3(0.4, 0.4, 0.4) + mainLight.color.rgb * nl * 0.8;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, IN.uv.xy, IN.uv.z);

                clip(col.a - 0.1);

                col.rgb *= IN.diffuse;

                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 uv          : TEXCOORD0;
            };

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            CBUFFER_END

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, IN.uv.xy, IN.uv.z);
                clip(col.a - 0.1);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
