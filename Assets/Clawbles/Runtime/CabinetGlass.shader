Shader "Clawbles/CabinetGlass"
{
    Properties { _Tint("Glass tint", Color) = (0.5,0.85,0.9,0.075) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            Varying Vert(Input v){Varying o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o;}
            half4 Frag(Varying i):SV_Target{
                half streak=saturate(1-abs(frac(i.uv.x*2+i.uv.y*.7)-.5)*35);
                return half4(_Tint.rgb+streak*.12,_Tint.a+streak*.025);
            }
            ENDHLSL
        }
    }
}
