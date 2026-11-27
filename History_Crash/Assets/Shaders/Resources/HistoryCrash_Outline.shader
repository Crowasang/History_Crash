// 메시를 법선 방향으로 조금 부풀려 뒷면만 그리는 아웃라인(URP). 원래 머티리얼 뒤에 머티리얼 칸을 하나 더 붙여 쓴다.
// 색은 HDR로 넣으면 블룸으로 살짝 빛난다. 미라 속성 표시(Mummy.cs)에서 사용.
Shader "HistoryCrash/Outline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (m)", Float) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes _in)
            {
                Varyings _out;
                float3 _posWS = TransformObjectToWorld(_in.positionOS.xyz);
                float3 _normalWS = normalize(TransformObjectToWorldNormal(_in.normalOS));
                _out.positionHCS = TransformWorldToHClip(_posWS + _normalWS * _OutlineWidth);
                return _out;
            }

            half4 Frag(Varyings _in) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
